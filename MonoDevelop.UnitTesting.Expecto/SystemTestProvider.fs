namespace MonoDevelop.UnitTesting.Expecto

open System
open MonoDevelop.UnitTesting
open MonoDevelop.Projects

type SystemTestProvider() =
    interface ITestProvider with
        member this.CreateUnitTest entry =
            match entry with
            | :? DotNetProject as project ->
                try
                    logfInfo "Creating test suite"
                    Some (new ExpectoProjectTestSuite(project) :> UnitTest)
                with e ->
                    logfError "Exception caught inside SystemTestProvider.CreateUnitTest: %A" e
                    None
            | _ -> None
            |> Option.toObj

        member this.Dispose () = ()