namespace MonoDevelop.UnitTesting.Expecto
open Expecto.RunnerServer.TestRunnerServer
open MonoDevelop.Core
open MonoDevelop.Projects
open MonoDevelop.UnitTesting
open MonoDevelop.Ide
open System
open System.IO
open System.Threading.Tasks
open global.Expecto

type ExpectoTestCase(name: string, id: Guid) =
    inherit UnitTest(HelperFunctions.ensureNonEmptyName name)

    //new(name, f, focus) = new ExpectoTestCase(name, { code = TestCode.Sync f; state = focus })

    member this.Id = id

    override this.OnRun testContext = null

type ExpectoTestList(name, tests: UnitTest list) as this =
    inherit UnitTestGroup(HelperFunctions.ensureNonEmptyName name)

    do
        List.iter this.Tests.Add tests

    override this.HasTests = true

    override this.OnRun testContext = null

type Tree<'Label, 'T> = | Node of 'Label * (Tree<'Label, 'T> list) | Leaf of 'Label * 'T

type ExpectoProjectTestSuite(project: DotNetProject) as this =
    inherit UnitTestGroup(project.Name, project)

    do
        IdeApp.ProjectOperations.EndBuild.Add (fun e ->
            if e.Success then
                this.Refresh () |> ignore)

    member this.OutputAssembly = project.GetOutputFileName(IdeApp.Workspace.ActiveConfiguration).FullPath.ToString()

    // ew that we have to do this, just because the compiler doesn't let you call base methods from inside an async callback
    member internal this.RefreshBase ct = base.Refresh ct

    /// Ensures that the test tree is up to date with respect to the last build -- for now, it is dumb and just always rebuilds
    /// the tree
    member this.Refresh () = this.Refresh Async.DefaultCancellationToken

    /// Ensures that the test tree is up to date with respect to the last build -- for now, it is dumb and just always rebuilds
    /// the tree
    override this.Refresh ct =
        logfInfo "Test tree refresh requested"
        let refresh = async {
            do! this.RebuildTestTree ()
            do! Async.AwaitTask (this.RefreshBase ct)
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
        Async.RunSynchronously <| async {
            logfInfo "Preparing to run tests"
            do! Async.AwaitTask (this.Build ())
            logfInfo "Project built; running tests"
            // TODO: implement
            return new UnitTestResult()
        }

    // If this were to return false, the group wouldn't show up in the pane, preventing tests from even being populated
    override this.HasTests = true

    /// Reconstructs the test tree using the output assembly generated from the last build. Calling OnCreateTests()
    /// will have exactly the same results.
    member private this.RebuildTestTree () = async {
        logfInfo "Refreshing project test tree"

        this.Tests.Clear ()

        try
            use! testRunner = RemoteTestRunner.Start () in
                let! tests = testRunner.Client.GetResponseAsync (ServerRequest.LoadTestsFromAssembly this.OutputAssembly)
                match tests with
                | ServerResponse.TestList tests ->
                    match tests with
                    | Ok tests ->
                        logfInfo "Discovered Expecto test: %A" tests

                        for test in Adapter.TryCreateMDTest tests do
                            this.Tests.Add test

                    | Error exnString ->
                        logfError "Server error while loading tests from '%s': %s" this.OutputAssembly exnString
            logfInfo "Client closed."
        with e ->
            logfError "Error while loading tests '%s': %A" this.OutputAssembly e
        }


    /// Just calls RebuildTestTree()
    override this.OnCreateTests () =
        Async.RunSynchronously (this.RebuildTestTree (), 10_000)


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

    static member TryCreateMDTest (tests: (Guid * FlatTestInfo) list) =
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
        let makeTestCase (name, id) = new ExpectoTestCase (name, id) :> UnitTest

        tests |> List.map (fun (id, testInfo) -> testInfo.name, id)
        |> buildTree makeTestList makeTestCase