﻿module Expecto.RunnerServer.MessageServer.Tests
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

let reverseString (str: string) = new string(Array.rev <| str.ToCharArray())

let ip = IPAddress.Loopback

[<Tests>]
let messageServerTests =
    testList "MessageServer+MessageClient" [
        testAsync "Client should be able to send a message to a server and get a response" {
            let server = MessageServer.Start (IPAddress.Any, 0, fun (str: string) -> async { return reverseString str })
            let! client = MessageClient.ConnectAsync (ip, server.Port)

            let! response = client.GetResponseAsync "foo bar"
            Expect.equal response "rab oof" "Check response"
        }
        testAsync "Client should be able to send messages and get responses more than once" {
             let server = MessageServer.Start (IPAddress.Any, 0, fun (str: string) -> async { return reverseString str })
             let! client = MessageClient.ConnectAsync (ip, server.Port)

             for i in 1..5 do
                 let! response = client.GetResponseAsync (sprintf "foo bar %d" i)
                 Expect.equal response (sprintf "%d rab oof" i) "Check response"
        } 
        testAsync "Server should be able to serve two clients simultaneously" {
            let server = MessageServer.Start (ip, 0, fun str -> async { return reverseString str })
            let! client1 = MessageClient.ConnectAsync (ip, server.Port)
            let! client2 = MessageClient.ConnectAsync (ip, server.Port)

            // it's fine that these aren't in parallel -- we are only testing that both clients can be connected
            let! foo = client1.GetResponseAsync "foo"
            let! bar = client2.GetResponseAsync "bar"
            let! baz = client1.GetResponseAsync "baz"

            Expect.equal foo "oof" "Check reverse foo"
            Expect.equal bar "rab" "Check reverse bar"
            Expect.equal baz "zab" "Check reverse baz"
        }
        testAsync "Client and server should be able to correspond out-of-order" {
            let server = MessageServer.Start (ip, 0, fun (delay, message) -> async {
                do! Async.Sleep delay
                return reverseString message
            })
            let! client = MessageClient.ConnectAsync (ip, server.Port)

            let mutable delayRecieved = false

            // TODO: rework the delay (mutexes?)
            let! getDelayed = Async.StartChild <| client.GetResponseAsync (250, "delay me")
            do! Async.Sleep 100
            let! getImmediate = Async.StartChild <| client.GetResponseAsync (0, "immediate")

            let! results = 
                Async.Parallel [
                    async {
                        let! delayedResp = getDelayed
                        delayRecieved <- true
                        Expect.equal delayedResp "em yaled" "Check immediate (non-delayed) response"
                    }
                    async {
                        let! immediateResp = getImmediate
                        Expect.equal delayRecieved false "Check that the delayed response has not yet been recieved (e.g. it should respond after immediate)."
                        Expect.equal immediateResp "etaidemmi" "Check immedate (non-delayed) response"
                    }
                ]
            ()
        }
    ]