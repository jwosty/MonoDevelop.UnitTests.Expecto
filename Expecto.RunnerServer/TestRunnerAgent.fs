module Expecto.RunnerServer
open System
open Expecto

type Message =
    | AddTest of Guid * Test
    | TryGetTest of AsyncReplyChannel<Test option> * Guid

module Message =
    let AddTest (guid, test) = AddTest (guid, test)
    let TryGetTest guid rc = TryGetTest (rc, guid)

type TestRunnerAgent() =
    let agent = MailboxProcessor.Start(fun agent ->
        let rec messageLoop tests = async {
            let! (msg: Message) = agent.Receive ()

            let tests =
                match msg with
                | AddTest (guid, test) ->
                    Map.add guid test tests
                | TryGetTest (rc, guid) ->
                    rc.Reply (Map.tryFind guid tests)
                    tests

            return! messageLoop tests
        }

        messageLoop Map.empty)

    member this.Post message = agent.Post message
    member this.PostAndReply message = agent.PostAndReply message
    member this.PostAndAsyncReply buildMessage = agent.PostAndAsyncReply buildMessage