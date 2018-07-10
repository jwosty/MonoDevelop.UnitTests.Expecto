module Expecto.RunnerServer.MessageServer.Tests
open System
open System.IO
open System.Net
open Expecto
open Expecto.RunnerServer

let testMessage = { channelId = "ABC-123"; payload = [1..10] }

[<Tests>]
let messageTests =
    testList "Message" [
        testAsync "Should be able to write to a stream" {
            use stream = new MemoryStream()
            do! Message.write stream testMessage

            stream.Position <- 0L
            let reader = new StreamReader(stream)

            let! contents = Async.AwaitTask (reader.ReadToEndAsync ())
            Expect.isGreaterThan contents.Length 0 "Check total number of chars written"
        }
        testAsync "Should be able to write a message then read it back" {
            let message = testMessage
            use stream = new MemoryStream()

            do! Message.write stream message
            stream.Position <- 0L
            let! message' = Message.read<int list> stream

            Expect.equal message message' ""
        }
    ]

[<Tests>]
let messageServerTests =
    testList "MessageServer+MessageClient" [
        testAsync "MC should be able to send a message to a server and get a response" {
            let ip, port = IPAddress.Loopback, 10201
            let server = MessageServer.Start (ip, port, fun x -> async { return x + 1 })
            let client = new MessageClient<_, _>()

            do! client.ConnectAsync (ip, port)
            let! response = client.GetResponseAsync 42
            Expect.equal response 43 "Check response"
        }
    ]