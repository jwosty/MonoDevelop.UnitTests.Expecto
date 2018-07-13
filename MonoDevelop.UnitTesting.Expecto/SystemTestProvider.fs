namespace MonoDevelop.UnitTesting.Expecto

open System
open MonoDevelop.UnitTesting
open MonoDevelop.Projects

type SystemTestProvider() =
    let testRunner = Async.RunSynchronously <| RemoteTestRunner.Start ()

    interface ITestProvider with
        member this.CreateUnitTest entry =
            match entry with
            | :? DotNetProject as project ->
                Some (new ExpectoProjectTestSuite(project, testRunner) :> UnitTest)
            | _ -> None
            |> Option.toObj

        member this.Dispose () = ()