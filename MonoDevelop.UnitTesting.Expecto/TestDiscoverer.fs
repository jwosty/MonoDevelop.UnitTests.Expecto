module MonoDevelop.UnitTesting.Expecto.TestDiscoverer
open System
open System.Reflection
open Expecto

let getTests (assemblyPath: string) =
    //let assembly = Assembly.Load assemblyPath
    //assembly.GetTypes ()
    ["test1"; "test2"]