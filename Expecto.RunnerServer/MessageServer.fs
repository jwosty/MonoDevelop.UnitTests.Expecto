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