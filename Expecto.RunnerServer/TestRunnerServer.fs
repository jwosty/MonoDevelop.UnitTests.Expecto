module Expecto.RunnerServer.TestRunnerServer
open System
open Expecto
open Expecto.RunnerServer

/// Pretty much just like Expecto.FlatTest, but without the runnable TestCode
type FlatTestInfo = { name: string; state: FocusState; focusOn: bool; sequenced: SequenceMethod }

module FlatTestInfo =
    let fromFlatTest (test: FlatTest) =
        { name = test.name; state = test.state; focusOn = test.focusOn; sequenced = test.sequenced }

type ServerRequest = | LoadTestsFromAssembly of assemblyPath: string
type ServerResponse = | TestList of Result<(Guid * FlatTestInfo) list, string>

let handleClientMessage (tdAgent: TestDictionaryAgent) message = async {
    match message with
    | LoadTestsFromAssembly asmPath ->
        let! result = tdAgent.PostAndAsyncReply (TestDictionaryMessage.AddTestsFromAssembly asmPath)
        match result with
        | Ok loadedTests ->
            let testInfoItems =
                loadedTests |> List.map (fun (testId, test) -> testId, FlatTestInfo.fromFlatTest test)
            return TestList (Ok testInfoItems)
        | Error e -> return TestList (Error (e.ToString ()))
}

/// Creates and runs a message server on the current thread
let createAndStart port =
    let tdAgent = new TestDictionaryAgent()
    let server = MessageServer (handleClientMessage tdAgent)
    server.StartAsync () |> Async.RunSynchronously