module Expecto.RunnerServer
open System
open Expecto

type TestDictionaryMessage =
    | AddTest of Guid * Test
    | TryGetTest of AsyncReplyChannel<Test option> * Guid

module TestDictionaryMessage =
    let AddTest (guid, test) = AddTest (guid, test)
    let TryGetTest guid rc = TryGetTest (rc, guid)

type TestDictionaryAgent() =
    let agent = MailboxProcessor.Start(fun agent ->
        let rec processMessage msg tests =
            match msg with
            | AddTest (guid, test) -> Map.add guid test tests
            | TryGetTest (rc, guid) ->
                rc.Reply (Map.tryFind guid tests)
                tests

        let rec messageLoop tests = async {
            let! (msg: TestDictionaryMessage) = agent.Receive ()

            let tests = processMessage msg tests

            return! messageLoop tests
        }

        messageLoop Map.empty)

    member this.Post message = agent.Post message
    member this.PostAndReply message = agent.PostAndReply message
    member this.PostAndAsyncReply buildMessage = agent.PostAndAsyncReply buildMessage