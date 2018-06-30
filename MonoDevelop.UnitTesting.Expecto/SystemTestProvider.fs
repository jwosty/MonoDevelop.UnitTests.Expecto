﻿namespace MonoDevelop.UnitTesting.Expecto

open System
open MonoDevelop.UnitTesting
open MonoDevelop.Projects

type SystemTestProvider() =
    interface ITestProvider with
        member this.CreateUnitTest entry =
            upcast new ExpectoTestSuite("My test (hello F#)");

        member this.Dispose () = ()
