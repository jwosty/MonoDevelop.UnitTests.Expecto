﻿namespace MonoDevelop.UnitTesting.Expecto
open System
open System.Diagnostics
open System.IO
open System.Reflection
open Expecto
open Expecto.RunnerServer

type RemoteTestRunner(client: MessageClient<_,_>, serverProcess: Process) =
    let mutable disposed = false

    member this.Client = client

    interface IDisposable with
        member this.Dispose () =
            (client :> IDisposable).Dispose ()
            if not serverProcess.HasExited then
                logfDebug "Closing server process with PID %A" serverProcess.Id
                serverProcess.Kill ()
            disposed <- true

    static member Start () = async {
        let execDir = Path.GetDirectoryName (typeof<RemoteTestRunner>.Assembly.Location)
        let assemblyName = "Expecto.RunnerServer.Net461.exe"
        logfDebug "execDir = %s" execDir
        logfDebug "assemblyName = %s" assemblyName
        let assemblyPath = Path.Combine (execDir, assemblyName)

        let startInfo = new ProcessStartInfo("mono", sprintf "%s" assemblyPath,
                                             RedirectStandardOutput = true, RedirectStandardError = true,
                                             UseShellExecute = false)
        let proc = Process.Start startInfo

        let! portStr = Async.AwaitTask <| proc.StandardOutput.ReadLineAsync ()
        let port =
            match Int32.TryParse portStr with
            | true, port -> port
            | false, _ -> raise (new FormatException(sprintf "Failed to connect to server. Server gave an invalid port number: %s" portStr))

        Async.Start <| async {
            let! str = Async.AwaitTask <| proc.StandardOutput.ReadLineAsync ()
            logfDebug "(Test server @ pid %d): %s" proc.Id str
        }

        logfDebug "Attempting to connect to test runner server"
        let! client = TestRunnerServer.connectClient port
        logfDebug "Connected to test runner server successfully"

        return new RemoteTestRunner(client, proc)
    }

module TestDiscoverer =
    let getTestFromAssembly (assembly: Assembly) = Expecto.Impl.testFromAssembly assembly

    let getTestFromAssemblyPath (assemblyPath: string) =
        try
            if File.Exists assemblyPath then
                getTestFromAssembly (Assembly.LoadFrom assemblyPath)
            else None
        with e ->
            logfError "Exception caught while loading tests: %A" e
            None