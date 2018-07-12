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
let createAndStart (port: int) =
    let tdAgent = new TestDictionaryAgent()
    let server = MessageServer (port, handleClientMessage tdAgent)
    server.StartAsync () |> Async.RunSynchronously

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
        |> Option.defaultValue 12050
    printfn "Test runner server listening on port %d" port
    createAndStart port
    0