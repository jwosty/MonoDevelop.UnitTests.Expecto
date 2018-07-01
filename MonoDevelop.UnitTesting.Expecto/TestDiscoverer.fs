module MonoDevelop.UnitTesting.Expecto.TestDiscoverer
open System
open System.Reflection
open Expecto

let getTestsFromAssembly (assembly: Assembly) =
    try Expecto.Impl.testFromAssembly assembly
    with e -> None

let getTestsFromAssemblyPath (assemblyPath: string) =
    getTestsFromAssembly (Assembly.Load assemblyPath)