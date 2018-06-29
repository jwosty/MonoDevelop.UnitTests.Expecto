namespace MonoDevelop.UnitTesting.Expecto

open System
open MonoDevelop.Projects
open MonoDevelop.UnitTesting

type ExpectoTestSuite =
    inherit UnitTest

    new(name) = { inherit UnitTest(name) }
    new(name, ownerSolutionItem) = { inherit UnitTest(ownerSolutionItem) }

    override this.OnRun testContext =
        raise (new NotImplementedException())