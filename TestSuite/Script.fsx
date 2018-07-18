#r "../packages/Expecto.8.1.1/lib/net461/Expecto.dll"
#r "../packages/FSharp.Compiler.Service.23.0.3/lib/net45/FSharp.Compiler.Service.dll"

#load "DynamicCompiler.fsx"

open System
open System.Reflection
open System.IO

let makerw () =
    let stream = new MemoryStream()
    let reader = new StreamReader(stream, System.Text.Encoding.UTF8, false, 64, true)
    let writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 64, true)
    reader, writer