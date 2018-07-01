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
    inherit UnitTest(name)

    new(name, f, focus) = new ExpectoTestCase(name, { code = TestCode.Sync f; state = focus })

    override this.OnRun testContext = null

type ExpectoTestList(name, tests: UnitTest list) =
    inherit UnitTestGroup(name)

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
        let tests = TestDiscoverer.getTestsFromAssemblyPath outputAssemblyPath
        match tests with
        | Some tests ->
            ()
            //tests
            //for testName in tests do
                //this.Tests.Add (new ExpectoTestCase(testName))
        | None -> ()

and Adapter =
    //static member CreateMDTests (name, tests: Test list) : UnitTest =
        //match test with
        //| TestCase (code, state) -> 

    /// Creates a MonoDevelop/VSfM test tree from an Expecto lib test
    static member CreateMDTest (test: Test) : UnitTest =
        match test with
        | TestLabel (name, test, state) ->
            match test with
            | TestCase (code, state) -> upcast new ExpectoTestCase(name, { code = code; state = state })
            | TestList (tests, state) -> notImpl "TestList not implemented"
            | _ -> notImpl ""
        | _ -> notImpl ""