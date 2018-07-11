namespace Expecto.RunnerServer
open System
open System.Reflection
open System.Threading
open Expecto

type TestDictionaryMessage =
    | AddTest of Guid * FlatTest
    | AddTestsFromAssembly of AsyncReplyChannel<Result<(Guid * FlatTest) list, exn>> * assemblyPath:string
    | TryGetTest of AsyncReplyChannel<FlatTest option> * Guid
    | TryGetTestResultGetter of AsyncReplyChannel<Async<Result<Expecto.Impl.TestSummary, exn>> option> * Guid
    | UnloadTestsAndAssemblies

module TestDictionaryMessage =
    let AddTest (guid, test) = AddTest (guid, test)
    let AddTestsFromAssembly assemblyPath rc = AddTestsFromAssembly (rc, assemblyPath)
    let TryGetTest guid rc = TryGetTest (rc, guid)
    let TryGetTestResultGetter guid rc = TryGetTestResultGetter (rc, guid)

type TestDictionaryAgent() =
    let config =
      { Expecto.Tests.defaultConfig with
            ``parallel`` = false
            verbosity = Logging.LogLevel.Verbose
            printer = Impl.TestPrinters.silent }

    let startSingleTestAgent config test = MailboxProcessor.Start (fun singleTestAgent -> async {
        let! summary = async {
            try
                let! cnclToken = Async.CancellationToken
                let! summary = Expecto.Impl.execTestAsync cnclToken config test
                return Ok summary
            with e ->
                return Error e
        }

        let rec messageLoop () = async {
            let! (rc: AsyncReplyChannel<_>) = singleTestAgent.Receive ()
            rc.Reply summary
            return! messageLoop ()
        }

        return! messageLoop ()
    })

    let agent = MailboxProcessor.Start (fun agent ->
        let rec processMessage msg tests =
            match msg with
            | AddTest (guid, test) -> Map.add guid (test, None) tests
            | AddTestsFromAssembly (rc, assemblyPath) ->
                try
                    let assembly = Assembly.LoadFrom assemblyPath
                    let newT =
                        match Expecto.Impl.testFromAssembly assembly with
                        | Some tests -> tests
                        | None -> failwithf "Expecto failed to load tests from assembly '%s' (%A)" assemblyPath assembly
                    let newTests =
                        Expecto.Test.toTestCodeList newT
                        |> List.map (fun flatTest -> Guid.NewGuid(), flatTest)
                    rc.Reply (Ok newTests)
                    newTests |> List.fold (fun tests (testId, test) ->
                        processMessage (AddTest (testId, test)) tests) tests
                with e ->
                    rc.Reply (Error e)
                    tests
            | UnloadTestsAndAssemblies ->
                try
                    Map.empty
                with e ->
                    printfn "Exception caught in TestDictionaryAgent while processing %A: %A" msg e
                    tests
            | TryGetTest (rc, guid) ->
                rc.Reply (Map.tryFind guid tests |> Option.map fst)
                tests
            | TryGetTestResultGetter (rc, guid) ->
                let tryGetResults, tests =
                    let t = Map.tryFind guid tests
                    match t with
                    // test exists, and test agent has already been started
                    | Some (test, Some (testAgent: MailboxProcessor<_>)) ->
                        Some (testAgent.PostAndAsyncReply id), tests
                    // test exists, but test agent has not been started yet
                    | Some (test, None) ->
                        let testAgent = startSingleTestAgent config test
                        let tests = Map.map (fun theGuid (test, theAgent) -> if theGuid = guid then test, Some testAgent else test, theAgent) tests
                        Some (testAgent.PostAndAsyncReply id), tests
                    // test does not exist
                    | None -> None, tests
                rc.Reply tryGetResults
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