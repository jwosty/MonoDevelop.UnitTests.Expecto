module MonoDevelop.UnitTesting.Expecto.TestDiscoverer
open System
open System.IO
open System.Reflection
open Expecto

let getTestFromAssembly (assembly: Assembly) = Expecto.Impl.testFromAssembly assembly

let getTestFromAssemblyPath (assemblyPath: string) =
    try
        if File.Exists assemblyPath then
            getTestFromAssembly (Assembly.LoadFrom assemblyPath)
        else None
    with e ->
        logfError "Exception caught while loading tests: %A" e
        None