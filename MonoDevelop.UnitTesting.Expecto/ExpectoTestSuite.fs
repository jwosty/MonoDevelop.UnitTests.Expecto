namespace MonoDevelop.UnitTesting.Expecto

open System
open MonoDevelop.Projects
open MonoDevelop.UnitTesting

type ExpectoTestSuite =
    inherit UnitTestGroup

    new(name) = { inherit UnitTestGroup(name) }
    new(name, ownerSolutionItem) = { inherit UnitTestGroup(name, ownerSolutionItem) }

    override this.OnRun testContext =
        
        raise (new NotImplementedException())