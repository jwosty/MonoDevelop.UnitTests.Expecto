namespace MonoDevelop.UnitTesting.Expecto

open System
open System.Threading.Tasks
open MonoDevelop.Core
open MonoDevelop.Projects
open MonoDevelop.UnitTesting
open MonoDevelop.Ide

type ExpectoTestCase(name) =
    inherit UnitTest(name)
    
    override this.OnRun testContext = new UnitTestResult()


type ExpectoProjectTestSuite(project: DotNetProject) =
    inherit UnitTestGroup(project.Name, project)

    override this.OnRun testContext =
        logfInfo "Expecto: running test!"
        null

    // If this returns false, the group doesn't show up in the pane, preventing building from being triggered
    override this.HasTests = true

    override this.OnCreateTests () =
        let outputAssemblyPath = project.GetOutputFileName(IdeApp.Workspace.ActiveConfiguration).FullPath.ToString()
        let tests = TestDiscoverer.getTests outputAssemblyPath
        for testName in tests do
            this.Tests.Add (new ExpectoTestCase(testName))