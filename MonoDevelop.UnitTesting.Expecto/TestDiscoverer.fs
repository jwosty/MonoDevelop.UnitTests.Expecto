module MonoDevelop.UnitTesting.Expecto.TestDiscoverer
open System
open System.Reflection
open Expecto

let getTestFromAssembly (assembly: Assembly) = Expecto.Impl.testFromAssembly assembly

let getTestFromAssemblyPath (assemblyPath: string) =
    try getTestFromAssembly (Assembly.LoadFrom assemblyPath)
    with e ->
        logfError "Exception caught while loading tests: %A" e
        None