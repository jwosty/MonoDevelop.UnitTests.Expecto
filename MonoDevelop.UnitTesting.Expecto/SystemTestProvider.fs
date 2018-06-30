namespace MonoDevelop.UnitTesting.Expecto

open System
open MonoDevelop.UnitTesting
open MonoDevelop.Projects

type SystemTestProvider() =
    interface ITestProvider with
        member this.CreateUnitTest entry =
            match entry with
            | :? DotNetProject as project -> 
                entry.GetAllItems ()
                |> Seq.map (fun project -> project, project.Name)
                |> printfn "items = %A"
                let grp = new ExpectoTestSuite(sprintf "%s (Expecto)" entry.Name)
                upcast grp
            | _ -> null

        member this.Dispose () = ()
