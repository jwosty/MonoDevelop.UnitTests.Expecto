namespace MonoDevelop.UnitTesting.Expecto
open System
open System.IO
open System.Threading.Tasks
open MonoDevelop.Core
open MonoDevelop.Projects
open MonoDevelop.UnitTesting
open MonoDevelop.Ide
open global.Expecto

type TestCase = { code: TestCode; state: FocusState }

type ExpectoTestCase(name, testCase) =
    inherit UnitTest(HelperFunctions.ensureNonEmptyName name)

    new(name, f, focus) = new ExpectoTestCase(name, { code = TestCode.Sync f; state = focus })

    override this.OnRun testContext = null

type ExpectoTestList(name, tests: UnitTest list) as this =
    inherit UnitTestGroup(HelperFunctions.ensureNonEmptyName name)

    do
        List.iter this.Tests.Add tests

    override this.HasTests = true

    override this.OnRun testContext = null

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
            this.RebuildTestTree ()
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
    member private this.RebuildTestTree () =
        logfInfo "Refreshing project test tree"

        this.Tests.Clear ()

        let test = TestDiscoverer.getTestFromAssemblyPath this.OutputAssembly

        logfInfo "Discovered Expecto test: %A" test

        match Option.bind Adapter.TryCreateMDTest test with
        | Some test -> this.Tests.Add test
        | None -> ()

    /// Just calls RebuildTestTree()
    override this.OnCreateTests () = this.RebuildTestTree ()


and Adapter =
    static member TryCreateMDTests (tests, label) =
        let mdTests = List.choose (fun t -> Adapter.TryCreateMDTest t) tests
        new ExpectoTestList(label, mdTests)

    /// Creates a MonoDevelop/VSfM test tree from an Expecto lib test
    static member TryCreateMDTest (test: Test, ?label) : UnitTest option =
        let label = defaultArg label ""
        match test with
        | TestLabel (subLabel, test, state) ->
            let label' =
                if label = "" then subLabel
                else sprintf "%s/%s" label subLabel
            logfInfo "using label: %s" label'
            Adapter.TryCreateMDTest (test, label')
        | TestCase (code, state) -> Some (upcast new ExpectoTestCase(label, { code = code; state = state }))
        | TestList ([test], state) -> Adapter.TryCreateMDTest (test, label)
        | TestList (tests, state) -> Some (upcast Adapter.TryCreateMDTests (tests, label))
        | Test.Sequenced _ ->
            logfError "Expecto sequenced tests not implemented yet!"
            None