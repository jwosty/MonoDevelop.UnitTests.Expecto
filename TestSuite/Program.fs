module MonoDevelop.UnitTesting.Expecto.TestSuite.Main
open Expecto


[<EntryPoint>]
let main argv =
    Tests.runTestsInAssembly Impl.ExpectoConfig.defaultConfig argv