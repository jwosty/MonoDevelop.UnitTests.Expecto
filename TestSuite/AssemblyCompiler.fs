module Expecto.RunnerServer.Tests.AssemblyCompiler
open Expecto
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices
open System.IO
open System.Reflection

let compiler = FSharpChecker.Create ()

/// Compiles the given F# source code to an assembly with the given name as an .exe, returning
/// the full path to the resulting assembly
let compile sourceCode assemblyName = async {
    let outDir = Path.Combine ([|Path.GetTempPath (); "Expecto.RunnerServer.Tests.AssemblyCompiler"; Path.GetRandomFileName ()|])
    let assemblyPath = Path.Combine (outDir, assemblyName + ".exe")
    let srcFilePath = Path.Combine (outDir, assemblyName + ".fs")
    Directory.CreateDirectory outDir |> ignore
    File.WriteAllText (srcFilePath, sourceCode)
    let expectoDll = typeof<Expecto.TestsAttribute>.Assembly.Location
    //File.Copy (expectoDll, Path.Combine (outDir, Path.GetFileName expectoDll))
    let! (errs, exitCode) = compiler.Compile ([|"fsc.exe"; "-o"; assemblyPath; "-r"; expectoDll; srcFilePath|])
    if exitCode <> 0 then
        invalidArg "sourceCode" (sprintf "Compilation of F# source code failed: %A" errs)
    return assemblyPath
}