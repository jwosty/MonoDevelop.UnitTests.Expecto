namespace MonoDevelop.UnitTesting.Expecto

open System
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

type ExpectoProjectTestSuite(project: DotNetProject) =
    inherit UnitTestGroup(project.Name, project)

    override this.OnRun testContext =
        logfInfo "Expecto: running test!"
        null

    // If this were to return false, the group wouldn't show up in the pane, preventing building from being triggered
    override this.HasTests = true

    override this.OnCreateTests () =
        let outputAssemblyPath = project.GetOutputFileName(IdeApp.Workspace.ActiveConfiguration).FullPath.ToString()
        let test = TestDiscoverer.getTestFromAssemblyPath outputAssemblyPath
        logfInfo "Discovered test: %A" test
        match Option.bind Adapter.TryCreateMDTest test with
        | Some test -> this.Tests.Add test
        | None -> ()

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
        | TestList (tests, state) -> Some (upcast Adapter.TryCreateMDTests (tests, label))
        | Test.Sequenced _ ->
            logfError "Expecto sequenced tests not implemented yet!"
            None