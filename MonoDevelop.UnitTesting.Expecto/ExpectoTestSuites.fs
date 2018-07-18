namespace MonoDevelop.UnitTesting.Expecto
open Expecto.RunnerServer
open Expecto.RunnerServer.TestRunnerServer
open MonoDevelop.Core
open MonoDevelop.Projects
open MonoDevelop.UnitTesting
open MonoDevelop.Ide
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open global.Expecto

type ExpectoTestCase(fullName: string, name: string, id: Guid, getTestRunner) =
    inherit UnitTest(HelperFunctions.ensureNonEmptyName name)

    let convertTestResult = function
        | Impl.TestResult.Passed ->
            let mutable result = UnitTestResult.CreateSuccess ()
            result.Passed <- 1
            result
        | Impl.TestResult.Error e ->
            let mutable result = UnitTestResult.CreateFailure e
            result.Errors <- 1
            result
        | Impl.TestResult.Failed msg ->
            // TODO: re-examine this and see if we can do something better, since the normal Expecto test results don't report the FailedException...
            let result = UnitTestResult.CreateFailure (new Exception(msg))
            result.Failures <- 1
            result
        | Impl.TestResult.Ignored msg ->
            let result = UnitTestResult.CreateIgnored msg
            result.Ignored <- 1
            result
    
    member this.Id = id

    member this.OnRunAsync () : Async<UnitTestResult> = async {
        let (testRunner: RemoteTestRunner) = getTestRunner ()
        let! result = testRunner.Client.GetResponseAsync (ServerRequest.RunTest id)
        match result with
        | ServerResponse.TestResult (Ok testResult as x) ->
            logfInfo "Got test result for '%A': %A" fullName x
            return convertTestResult testResult.result
        | ServerResponse.TestResult (Error errStr as x) ->
            logfInfo "Got test result for '%A': %A" fullName x
            logfError "Remote error reported while executing test (test fullName, id = %A): %A" (fullName, id) errStr
            let result = UnitTestResult.CreateFailure (new Exception(sprintf "Internal test runner error: %s" errStr))
            result.Errors <- 1
            return result
        | _ ->
            let msg =
                sprintf "Remote test runner gave unexpected response (expected test execution result) (test fullName, id = %A): %A" (fullName, id) result
            logfError "%s" msg
            let result = UnitTestResult.CreateFailure (new Exception(msg))
            result.Errors <- 1
            return result
    }

    override this.OnRun testContext =
        Async.RunSynchronously (this.OnRunAsync ())

type ExpectoTestList(name, tests: UnitTest list) as this =
    inherit UnitTestGroup(HelperFunctions.ensureNonEmptyName name)

    do
        List.iter this.Tests.Add tests

    override this.HasTests = true

    override this.OnRun testContext =
        let mdResult = UnitTestResult.CreateSuccess ()
        for test in this.Tests do
            let result = test.Run testContext
            mdResult.Add result
        mdResult

type Tree<'Label, 'T> = | Node of 'Label * (Tree<'Label, 'T> list) | Leaf of 'Label * 'T

type ExpectoProjectTestSuite(project: DotNetProject) as this =
    inherit UnitTestGroup(project.Name, project)

    let testRunnerRestartLock = new SemaphoreSlim(1, 1)
    let theLock = new SemaphoreSlim(1, 1)

    let mutable testRunner = Async.RunSynchronously <| acquireAsync testRunnerRestartLock RemoteTestRunner.Start
    /// A DateTime representing the last time that a test runner read/loaded/examined the user's test assembly
    let mutable lastTestRunnerRestart = None
    /// The configuration last used to load a test
    let mutable lastTestLoadConfig = None

    do
        IdeApp.ProjectOperations.EndBuild.Add (fun e ->
            if e.Success then
                this.Refresh () |> ignore)

    /// Returns whether or not a call to Refresh is needed in order to keep up to date
    member this.RefreshRequired () =
        let currentConfig = IdeApp.Workspace.ActiveConfiguration
        match lastTestRunnerRestart, lastTestLoadConfig with
        | Some lastTestRunnerRestart, Some lastTestLoadConfig ->
            lastTestLoadConfig = currentConfig && project.GetLastBuildTime currentConfig > lastTestRunnerRestart
        | _ -> true

    member this.RestartTestRunnerAsync () = acquireAsync testRunnerRestartLock (fun () -> async {
        logfInfo "Restarting remote test runner..."
        (testRunner :> IDisposable).Dispose ()
        logfInfo "Old test runner client closed."
        let! newTestRunner = RemoteTestRunner.Start ()
        lastTestRunnerRestart <- Some DateTime.Now
        testRunner <- newTestRunner
    })

    member this.GetTestRunner () = testRunner

    member this.OutputAssembly = project.GetOutputFileName(IdeApp.Workspace.ActiveConfiguration).FullPath.ToString()

    // ew that we have to do this, just because the compiler doesn't let you call base methods from inside an async callback
    member internal this.RefreshBase ct = base.Refresh ct
    member internal this.NotifyChanged () = this.OnTestChanged ()

    /// Ensures that the test tree is up to date with respect to the last build -- for now, it is dumb and just always rebuilds
    /// the tree
    member this.Refresh () = this.Refresh Async.DefaultCancellationToken

    /// Ensures that the test tree is up to date with respect to the last build -- for now, it is dumb and just always rebuilds
    /// the tree
    override this.Refresh ct =
        logfInfo "Test tree refresh requested"
        let refresh = async {
            if this.RefreshRequired () then
                logfInfo "Test refresh required; refreshing"
                do! this.RebuildTestTree ()
                do! Async.AwaitTask (this.RefreshBase ct)
            else
                logfInfo "Test refresh not required; skipping"
        }
        Async.StartAsTask (refresh, cancellationToken = ct) :> _

    //override this.OnBuild () =
        //Async.StartAsTask <| async {
        //    let! ct = Async.CancellationToken
        //    let! buildResult = Async.AwaitTask <| IdeApp.ProjectOperations.Build(project, new Nullable<_>(ct), null).Task
        //    if not buildResult.Failed then
        //        this.Refresh ct |> ignore
        //    return null
        //} :> _

    override this.OnRun testContext =
        Async.RunSynchronously <| acquireAsync theLock (fun () -> async {
            logfInfo "Preparing to run tests"
            do! Async.AwaitTask (this.Build ())
            logfInfo "Project built; running tests"
            let mdResult = UnitTestResult.CreateSuccess ()
            for test in this.Tests do
                let result = test.Run testContext
                mdResult.Add result
            logfInfo "mdResult = %A" mdResult

            //do! this.RebuildTestTree ()

            return mdResult
        })

    // If this were to return false, the group wouldn't show up in the pane, preventing tests from even being populated
    override this.HasTests = true

    /// Reconstructs the test tree using the output assembly generated from the last build. Calling OnCreateTests()
    /// will have exactly the same results.
    member private this.RebuildTestTree () =
        acquireAsync theLock (fun () -> async {
            logfInfo "Refreshing project test tree"

            // This disposes all tests
            //this.UpdateTests ()

            try
                do! this.RestartTestRunnerAsync ()
                let! tests = testRunner.Client.GetResponseAsync (ServerRequest.LoadTestsFromAssembly this.OutputAssembly)
                match tests with
                | ServerResponse.TestList tests ->
                    match tests with
                    | Ok tests ->
                        logfInfo "Discovered Expecto test: %A" tests

                        this.Tests.Clear ()
                        
                        for test in Adapter.TryCreateMDTest this.GetTestRunner tests do
                            this.Tests.Add test

                    | Error exnString ->
                        logfError "Server error while loading tests from '%s': %s" this.OutputAssembly exnString
                | _ -> failwithf "Server gave unexpected response (expected list of tests to load): %A" tests

                this.NotifyChanged ()

            with e ->
                logfError "Error while loading tests '%s': %A" this.OutputAssembly e
            }

        )
    
    /// Just calls RebuildTestTree()
    override this.OnCreateTests () = Async.Start (this.RebuildTestTree ())


and Adapter =
    //static member TryCreateMDTests (tests, label) =
        //let mdTests = List.choose (fun t -> Adapter.TryCreateMDTest t) tests
        //new ExpectoTestList(label, mdTests)

    /// Creates a MonoDevelop/VSfM test tree from an Expecto lib test
    //static member TryCreateMDTest (test: Test, ?label) : UnitTest option =
        //let label = defaultArg label ""
        //match test with
        //| TestLabel (subLabel, test, state) ->
        //    let label' =
        //        if label = "" then subLabel
        //        else sprintf "%s/%s" label subLabel
        //    logfInfo "using label: %s" label'
        //    Adapter.TryCreateMDTest (test, label')
        //| TestCase (code, state) -> Some (upcast new ExpectoTestCase(label, { code = code; state = state }))
        //| TestList ([test], state) -> Adapter.TryCreateMDTest (test, label)
        //| TestList (tests, state) -> Some (upcast Adapter.TryCreateMDTests (tests, label))
        //| Test.Sequenced _ ->
            //logfError "Expecto sequenced tests not implemented yet!"
            //None

    static member TryCreateMDTest getTestRunner (tests: (Guid * FlatTestInfo) list) =
        // This is not the right solution. I should really rebuild some kind of serializable tree structure that we can transmit over
        // the wire (can't be a normal Test object since you can't serialize the lambdas they contain)


        let rec buildTree (makeNode: string * ('Tree list) -> 'Tree)
                          (makeLeaf: (string * 'a -> 'Tree))
                          (leaves: (string * 'a) list)
                          : 'Tree list =
            // group everything by top-level path
            let groups =
                leaves |> List.map (fun (leafPath: string, leafValue) ->
                    // the `2` argument means that it will not return an array of length > 2, so we don't have to cover those cases
                    // also, empty array case doesn't happen
                    match leafPath.Split ([|'/'|], 2) with
                    // leaf
                    | [|leafName|] | [|"";leafName|] -> None, (leafName, leafValue)
                    // node
                    | [|parent;leafPath|] -> Some parent, (leafPath, leafValue))
                |> List.groupBy fst

            [for group in groups do
                match group with
                // leaf
                | None, children ->
                    let children = children |> List.map (fun (_, (leafName, leafValue)) -> makeLeaf (leafName, leafValue))
                    yield! children
                // node
                | Some parentName, children -> 
                    let children =
                        children
                        |> List.map (fun (_, (leafPath, leafValue)) -> (leafPath, leafValue))
                    let children = buildTree makeNode makeLeaf children
                    yield makeNode (parentName, children) ]
        
        let makeTestList (name, children) = new ExpectoTestList (name, children) :> UnitTest
        let makeTestCase (name, (fullName, id)) = new ExpectoTestCase (fullName, name, id, getTestRunner) :> UnitTest

        tests |> List.map (fun (id, testInfo) -> testInfo.name, (testInfo.name, id))
        |> buildTree makeTestList makeTestCase