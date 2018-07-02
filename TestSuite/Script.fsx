#r "../packages/Expecto.8.1.1/lib/net461/Expecto.dll"
#r "../packages/FSharp.Compiler.Service.23.0.3/lib/net45/FSharp.Compiler.Service.dll"

#load "DynamicCompiler.fsx"

open System
open System.Reflection


Expecto.Impl.testFromAssembly (Assembly.LoadFrom "/Users/jwostenberg/Code/FSharp/TestSandbox/TestSuite1/bin/Debug/TestSuite1.exe")

let program = """
    namespace Program
    open System
    open Expecto

    module MyModule =
        let myNumber = 42

        //[<Tests>]
        //let passingTest = test "passing test" { Expect.equal 42 myNumber "" }

        [<Tests>]
        let failingTest = test "failing test" { Expect.equal 0 myNumber "" }

        [<EntryPoint>]
        let main argv = 
            printfn "Hello world"
            0
"""

let asm = compile program |> Async.RunSynchronously

testFromAssembly asm

asm.GetTypes () |> Array.map (fun typ ->
    let members =
        typ.GetMembers ()
        |> Array.choose (function
            | :? MethodInfo as meth when meth.IsStatic -> Some meth
            | _ -> None)
        //|> Array.filter (fun meth -> (meth.GetCustomAttributes(typeof<Expecto.TestsAttribute>))
        |> Array.map (fun meth -> meth.Name, meth.ReturnType.Name, meth.Attributes)

    typ.Name, members)