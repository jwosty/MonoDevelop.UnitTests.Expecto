module MonoDevelop.UnitTesting.Expecto.TestSuite.Tests
open Expecto
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices
open MonoDevelop.UnitTesting.Expecto
open System.IO
open System.Reflection

let compiler = FSharpChecker.Create ()

/// <summary>
/// Compiles an F# program (which may reference Expecto) as a string into a dynamic assembly, throwing an
/// InvalidArgumentException if compilation fails.
/// </summary>
let compile sourceCode = async {
    let srcFile = Path.GetTempFileName ()
    let srcFile = Path.ChangeExtension (srcFile, ".fs")
    File.WriteAllText (srcFile, sourceCode)
    let expectoDll = typeof<Expecto.TestsAttribute>.Assembly.Location
    let! (errs, exitCode, assembly) = compiler.CompileToDynamicAssembly ([|"fsc.exe"; "-o"; "Program.exe"; "-r"; expectoDll; srcFile|], None)
    match assembly with
    | Some assembly -> return assembly
    | None -> return invalidArg "sourceCode" (sprintf "Complation failed (exit code %d). Errors: %A" exitCode errs)
}

let getPublicTypes (assembly: Assembly) =
    assembly.GetTypes() |> Seq.filter (fun typ -> typ.IsPublic)

/// <summary>
/// Scans an assembly for Expecto tests, but works on dynamic assemblies, unlike Expecto.Impl.testFromAssembly
/// </summary>
let testFromAssembly (assembly: Assembly) =
    getPublicTypes assembly
    |> Seq.choose Expecto.Impl.testFromType
    |> Seq.toList
    |> Expecto.Impl.listToTestListOption

let dummyFun = (fun x -> ())

[<Tests>]
let mdUTTests =
    testList "ExpectoTestCase" [
        test "Equals should return true when the instances are constructed with the same arguments" {
            Expect.equal (new ExpectoTestCase("foo", dummyFun, Normal)) (new ExpectoTestCase("foo", dummyFun, Normal)) ""
        }
    ]

[<Tests>]
let adapterTests =
    testList "Adapter" [
        testList "CreateMDTest" [
            //test "Single test" {
            //    let exTest = testCase "foo" dummyFun
            //    let mdTest = new ExpectoTestCase("foo", dummyFun)
            //    Expect.equal (Adapter.CreateMDTest exTest) (upcast mdTest) ""
            //    ()
            //}
        ]
    ]