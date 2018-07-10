namespace Expecto.RunnerServer
open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open MBrace.FsPickler
open MBrace.FsPickler.Json

[<AutoOpen>]
module Ext =
    type StreamReader with
        // Copied from the .NET source
        static member DefaultBufferSize = 1024

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
    

type Message<'a> = { channelId: string; payload: 'a }

module Message =
    let serializer = new JsonSerializer()

    // Messages are composed of a 32-bit int indicating the payload length in bytes, followed by a newline,
    // followed by the message payload

    let read<'a> (stream: Stream) : Async<Message<'a>> = async {
        // TODO: catch exceptions and report protocol errors back to the client
        //use reader = new BinaryReader(stream, Encoding.UTF8, true) in
        let! payload = async {
            use reader = new StreamReader(stream, Encoding.UTF8, true, StreamReader.DefaultBufferSize, true)
            let! payloadLength = Async.AwaitTask <| reader.ReadLineAsync ()
            let payloadLength = int (payloadLength.TrimEnd [|'\r'; '\n'|])
            let! payload = reader.ReadBytesAsync payloadLength
            return new string(payload)
        }

        use strReader = new StringReader(payload)
        let message = serializer.Deserialize<Message<'a>> strReader
        return message
    }

    let write (stream: Stream) (message: Message<'a>) = async {
        let payload =
            use payloadWriter = new StringWriter()
            serializer.Serialize (payloadWriter, message)
            payloadWriter.ToString ()
        
        use writer = new StreamWriter(stream, Encoding.UTF8, StreamReader.DefaultBufferSize, true)

        do! Async.AwaitTask (writer.WriteLineAsync (string (Encoding.UTF8.GetByteCount payload)))
        do! Async.AwaitTask (writer.WriteAsync payload)
        do! Async.AwaitTask (writer.FlushAsync ())
    }

type MessageServer<'TRequest, 'TResponse>(listener: TcpListener, messageHandler: 'TRequest -> Async<'TResponse>) =
    new(address, port, messageHandler) = MessageServer(new TcpListener(address, port), messageHandler)
    new(port: int, messageHandler) = MessageServer(IPAddress.Loopback, port, messageHandler)

    member this.LocalEndpoint = listener.LocalEndpoint

    // TODO: is this always safe?
    member this.Port = (this.LocalEndpoint :?> IPEndPoint).Port

    member private this.HandleClient (client: TcpClient) = async {
        let clientStream = client.GetStream ()
        while true do
            let! request = Message.read<'TRequest> clientStream
            // ... Generate response here ...
            let! response = messageHandler request.payload
            let response = { channelId = request.channelId; payload = response }
            do! Message.write clientStream response
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

module MessageServer =
    /// Creates and starts a message server in a separate thread.
    let Start (address, port, messageHandler) =
        let server = new MessageServer<_, _>(address, port, messageHandler)
        server.Start ()
        server

type MessageClient<'TRequest, 'TResponse>(tcpClient: TcpClient) =
    new() = MessageClient(new TcpClient())

    member this.ConnectAsync (address: IPAddress, port) = async {
        do! Async.AwaitTask (tcpClient.ConnectAsync (address, port))
    }

    member this.GetResponseAsync (request: 'TRequest) : Async<'TResponse> = async {
        let stream = tcpClient.GetStream ()
        let channelId = Guid().ToString()
        do! Message.write stream { channelId = channelId; payload = request }
        let! response = Message.read stream
        return response.payload
    }

module MessageClient =
    let ConnectAsync<'TRequest, 'TResponse> (address, port) = async {
        let client = new MessageClient<'TRequest, 'TResponse>()
        do! client.ConnectAsync (address, port)
        return client
    }