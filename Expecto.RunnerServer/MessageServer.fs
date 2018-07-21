namespace Expecto.RunnerServer
open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading
open MBrace.FsPickler
open MBrace.FsPickler.Json

[<AutoOpen>]
module Ext =
    type StreamReader with
        // Copied from the .NET source
        static member DefaultBufferSize = 1024

        /// <summary>
        /// Reads the specified number of bytes from the underlying stream (as opposed to <see cref="StreamReader.ReadAsync"/>,
        /// which reads UP TO the specified number of bytes).
        /// <exception cref="EndOfStreamException">Thrown when the end of the stream is reached before all of the bytes could be read.</exception>
        /// </summary>
        member this.ReadBytesAsync count = async {
            let mutable offset = 0
            let buffer = Array.zeroCreate count
            while offset < count do
                let! read = Async.AwaitTask <| this.ReadAsync (buffer, offset, count)
                if read > 0 then
                    offset <- offset + read
                else
                    raise (new EndOfStreamException())
            return buffer
        }

[<AutoOpen>]
module HelperFunctions =
    let inline acquire (semaphore: SemaphoreSlim) action =
        try
            semaphore.WaitAsync().RunSynchronously()
            action ()
        finally
            semaphore.Release() |> ignore

    let inline acquireAsync (semaphore: SemaphoreSlim) action = async {
        try
            do! Async.AwaitTask (semaphore.WaitAsync ())
            return! action ()
        finally 
            semaphore.Release () |> ignore
    }

type Message<'a> = { channelId: string; payload: 'a }

module Message =
    let serializer = new JsonSerializer()

    // Messages are composed of a 32-bit int indicating the payload length in bytes, followed by a newline,
    // followed by the message payload

    /// Returns a StreamReader for the given stream that won't close the underlying stream
    let makeTemporaryReader (stream: Stream) = new StreamReader(stream, Encoding.UTF8, true, StreamReader.DefaultBufferSize, true)

    /// Attempts to read a message header, which solely consists of the payload length in bytes
    let readHeader (reader: StreamReader) = async {
        let! payloadLength = Async.AwaitTask <| reader.ReadLineAsync ()
        if payloadLength = null then
            raise (new EndOfStreamException())
        let payloadLength = payloadLength.TrimEnd [|'\r'; '\n'|]
        return int payloadLength
    }

    /// Attempts to read and deserialize a message from a stream. Not thread-safe.
    let read<'a> (stream: Stream) : Async<Message<'a>> = async {
        // TODO: catch exceptions and report protocol errors back to the client
        let! payload = async {
            use reader = makeTemporaryReader stream
            let! payloadLength = readHeader reader
            let! payload = reader.ReadBytesAsync payloadLength
            return new string(payload)
        }

        use strReader = new StringReader(payload)
        return serializer.Deserialize<Message<'a>> strReader
    }

    /// Returns a StreamWriter for the given stream that won't close the underlying stream
    let makeTemporaryWriter (stream: Stream) = new StreamWriter(stream, Encoding.UTF8, StreamReader.DefaultBufferSize, true)

    /// Writes a message header, which solely consists of the payload length in bytes
    let writeHeader (writer: StreamWriter) payloadLength =
        Async.AwaitTask (writer.WriteLineAsync (string payloadLength))

    /// Serializes and writes a message to a stream. Not thread-safe.
    let write (stream: Stream) (message: Message<'a>) = async {
        let payload =
            use payloadWriter = new StringWriter()
            serializer.Serialize (payloadWriter, message)
            payloadWriter.ToString ()

        use writer = makeTemporaryWriter stream
        
        do! writeHeader writer (Encoding.UTF8.GetByteCount payload)
        do! Async.AwaitTask (writer.WriteAsync payload)
        do! Async.AwaitTask (writer.FlushAsync ())
    }

// TODO: implement IDisposable
type MessageServer<'TRequest, 'TResponse>(listener: TcpListener, messageHandler: MessageServer<_,_> -> 'TRequest -> Async<'TResponse>) =
    let readLock = new SemaphoreSlim(1, 1)
    let writeLock = new SemaphoreSlim(1, 1)

    new(address, port, messageHandler) = MessageServer(new TcpListener(address, port), messageHandler)
    new(port: int, messageHandler) = MessageServer(IPAddress.Loopback, port, messageHandler)
    new(messageHandler) = MessageServer(IPAddress.Loopback, 0, messageHandler)

    member this.LocalEndpoint = listener.LocalEndpoint

    // TODO: is this always safe?
    member this.Port = (this.LocalEndpoint :?> IPEndPoint).Port

    member private this.HandleClient (client: TcpClient) = async {
        let clientStream = client.GetStream ()
        try
            while true do
                // The read lock may not be strictly necessary
                let! request = acquireAsync readLock (fun () -> Message.read<'TRequest> clientStream)
                Async.Start <| async {
                    let! response = messageHandler this request.payload
                    let response = { channelId = request.channelId; payload = response }
                    do! acquireAsync writeLock (fun () -> Message.write clientStream response)
                }
        with :? EndOfStreamException -> ()
    }

    /// Starts the message server.
    member this.StartAsync () =
        listener.Start ()
        async {
            while true do
                let! client = Async.AwaitTask <| listener.AcceptTcpClientAsync ()
                Async.Start <| this.HandleClient client
        }

    /// Starts the message server in a separate thread.
    member this.Start () = Async.Start (this.StartAsync ())

    member this.Stop () = listener.Stop ()

module MessageServer =
    /// Creates and starts a message server in a separate thread.
    let Start (address, port, messageHandler) =
        let server = new MessageServer<_, _>(address, port, messageHandler)
        server.Start ()
        server

type MessageClient<'TRequest, 'TResponse>(tcpClient: TcpClient) =
    let mutable disposed = false
    let responseLoopCanceller = new CancellationTokenSource()

    let writeLock = new SemaphoreSlim(1, 1)
    let readLock = new SemaphoreSlim(1, 1)

    // TODO: consider single-case DU for identifier
    /// A dictionary storing the client's open request IDs along with their corresponding continuations
    /// to be called when its response is recieved.
    let responseContinuations = new ConcurrentDictionary<string, Message<'TResponse> -> unit>()

    let onInvalidResponse = new Event<_>()
    let onInvalidResponse_Published = onInvalidResponse.Publish

    do
        onInvalidResponse_Published.Add (fun message ->
            printfn "WARNING: server replied with an invalid response ID. Message was: (%A)" message)

    /// An event that triggers when the server sends an invalid response; that is, when the response doesn't match
    /// any of this client's requests.
    member this.OnInvalidResponse = onInvalidResponse_Published

    new() = new MessageClient<_,_>(new TcpClient())

    member private this.HandleResponseLoop (stream: Stream) = async {
        try
            while true do
                // listen for a response, then route it to the correct continuation
                    let! response = acquireAsync readLock (fun () -> Message.read stream)
                    match responseContinuations.TryGetValue response.channelId with
                    | true, continuation ->
                        responseContinuations.TryRemove response.channelId |> ignore
                        continuation response
                    | false, _ -> onInvalidResponse.Trigger response
        with 
        | :? EndOfStreamException -> ()
        | e ->
            printf "Exception caught while reading/handling response: %A\n" e
    }

    /// Connects to a MessageServer listening at the given address and port.
    member this.ConnectAsync (address: IPAddress, port) = async {
        do! Async.AwaitTask (tcpClient.ConnectAsync (address, port))
        let stream = tcpClient.GetStream ()
        Async.Start (this.HandleResponseLoop stream, responseLoopCanceller.Token)
    }

    member this.Close () = (this :> IDisposable).Dispose ()

    interface IDisposable with
        member this.Dispose () =
            if not disposed then
                responseLoopCanceller.Cancel ()
                tcpClient.Dispose ()
                disposed <- true

    /// Asynchronously waits until a request's response is recieved.
    member private this.AwaitResponseAsync request = async {
        let! response =
            // Build an async from a callback, and add it to the responseContinuations dictionary. This async will call back when the function in
            // the dictionary is invoked (in HandleResponseLoop)
            Async.FromContinuations (fun (cont, contError, contCancelled) ->
                if not (responseContinuations.TryAdd (request.channelId, cont)) then
                    // If this happens, it's probably a bug in this method -- something is probably causing the same response's
                    // continuation to be added multiple times
                    invalidOp ("Response has already been registered. This is probably a MessageClient bug -- please report this to the author.")
            )
        
        // sanity check, just in case
        if response.channelId <> request.channelId then
            invalidOp (sprintf "Response channelId didn't match the request (request: %A, response: %A). This is a MessageClient bug -- please report this to the author." response request)
        
        return response
    }

    /// Asynchronously sends a request to the server and awaits the response
    member this.GetResponseAsync (request: 'TRequest) : Async<'TResponse> = async {
        let stream = tcpClient.GetStream ()
        let request = { channelId = Guid.NewGuid().ToString(); payload = request }
        do! acquireAsync writeLock (fun () -> Message.write stream request)
        let! response = this.AwaitResponseAsync request
        return response.payload
    }

module MessageClient =
    let ConnectAsync<'TRequest, 'TResponse> (address, port) = async {
        let client = new MessageClient<'TRequest, 'TResponse>()
        do! client.ConnectAsync (address, port)
        return client
    }