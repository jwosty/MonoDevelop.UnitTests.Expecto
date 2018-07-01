namespace MonoDevelop.UnitTesting.Expecto

open System
open MonoDevelop.UnitTesting
open MonoDevelop.Projects

type SystemTestProvider() =
    interface ITestProvider with
        member this.CreateUnitTest entry =
            match entry with
            | :? DotNetProject as project ->
                Some (new ExpectoProjectTestSuite(project) :> UnitTest)
            | _ -> None
            |> Option.toObj

        member this.Dispose () = ()