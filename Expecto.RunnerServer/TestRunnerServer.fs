module Expecto.RunnerServer.TestRunnerServer
open System
open Expecto
open Expecto.RunnerServer

/// Pretty much just like Expecto.FlatTest, but without the runnable TestCode
type FlatTestInfo = { name: string; state: FocusState; focusOn: bool; sequenced: SequenceMethod }

module FlatTestInfo =
    let fromFlatTest (test: FlatTest) =
        { name = test.name; state = test.state; focusOn = test.focusOn; sequenced = test.sequenced }

type ServerRequest =
    | LoadTestsFromAssembly of assemblyPath: string
    | RunTest of Guid
type ServerResponse =
    | TestList of Result<(Guid * FlatTestInfo) list, string>
    | TestResult of Result<Impl.TestSummary, string>

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
    | RunTest testId ->
        let! testResultGetter = tdAgent.PostAndAsyncReply (TestDictionaryMessage.TryGetTestResultGetter testId)
        match testResultGetter with
        | Some getTestResult ->
            let! testResult = getTestResult
            match testResult with
            | Ok testSummary -> return TestResult (Ok testSummary)
            | Error e -> return TestResult (Error (e.ToString ()))
        | None -> return TestResult (Error (sprintf "No test found with id %A." testId))
}

/// Creates and runs a message server on the current thread
//let createAndStart (port: int) = async {
//    let tdAgent = new TestDictionaryAgent()
//    let server = MessageServer (port, handleClientMessage tdAgent)
//    return (server, server.StartAsync ())
//}

let connectClient port : Async<MessageClient<ServerRequest, ServerResponse>> =
    MessageClient.ConnectAsync (Net.IPAddress.Loopback, port)

/// An entry point that a host stub can call
let main argv =
    let port =
        Array.tryItem 0 argv
        |> Option.bind (fun x ->
            match Int32.TryParse x with
            | true, x -> Some x
            | _ -> None)
        |> Option.defaultValue 0


    Async.RunSynchronously <| async {
        let tdAgent = new TestDictionaryAgent()
        let server = new MessageServer<_,_>(port, fun server -> handleClientMessage tdAgent)

        let! awaitServerExit = Async.StartChild <| server.StartAsync ()
        // We tell the client what port we're listening on by making sure it's the first thing to go to stdout
        // TODO: (there should probably be tests around this behavior)
        printfn "%d" server.Port
        printfn "Server listening on port: %d" server.Port // human-readable output as well
        do! awaitServerExit
        ()
    }
    0