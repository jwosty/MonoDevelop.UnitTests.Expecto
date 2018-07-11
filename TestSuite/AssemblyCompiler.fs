namespace Expecto.RunnerServer.Tests
open Expecto
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices
open System.IO
open System.Reflection

type AssemblyCompiler =
    static member private Compiler = FSharpChecker.Create ()

    /// Compiles the given F# source code to an assembly with the given name as an .exe, returning
    /// the full path to the resulting assembly
    static member Compile (sourceCode, assemblyName) = async {
        let outDir = Path.Combine ([|Path.GetTempPath (); "Expecto.RunnerServer.Tests.AssemblyCompiler"; Path.GetRandomFileName ()|])
        return! AssemblyCompiler.Compile (sourceCode, outDir, assemblyName)
    }

    /// Compiles the given F# source code to an assembly with the given name as an .exe, returning
    /// the full path to the resulting assembly
    static member Compile (sourceCode, outputDir, assemblyName) = async {
        let assemblyPath = Path.Combine (outputDir, assemblyName + ".exe")
        let srcFilePath = Path.Combine (outputDir, assemblyName + ".fs")

        Directory.CreateDirectory outputDir |> ignore
        File.WriteAllText (srcFilePath, sourceCode)

        let expectoDll = typeof<Expecto.TestsAttribute>.Assembly.Location
        //File.Copy (expectoDll, Path.Combine (outDir, Path.GetFileName expectoDll))
        let! (errs, exitCode) = AssemblyCompiler.Compiler.Compile ([|"fsc.exe"; "-o"; assemblyPath; "-r"; expectoDll; srcFilePath|])
        if exitCode <> 0 then
            invalidArg "sourceCode" (sprintf "Compilation of F# source code failed: %A" errs)
        return assemblyPath
    }