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
                logfInfo "Closing server process with PID %A" serverProcess.Id
                serverProcess.Kill ()
            disposed <- true
    
    static member Start () = async {
        let execDir = Path.GetDirectoryName (typeof<RemoteTestRunner>.Assembly.Location)
        let assemblyName = "Expecto.RunnerServer.Net461.exe"
        logfInfo "execDir = %s" execDir
        logfInfo "assemblyName = %s" assemblyName
        let assemblyPath = Path.Combine (execDir, assemblyName)

        // TODO: escape the command line arguments correctly
        let startInfo = new ProcessStartInfo("mono", sprintf "\"%s\"" assemblyPath,
                                             RedirectStandardOutput = true, RedirectStandardError = true,
                                             UseShellExecute = false)
        let proc = Process.Start startInfo

        Async.Start <| async {
            let! str = Async.AwaitTask <| proc.StandardError.ReadLineAsync ()
            logfError "(Test server @ pid %d): %s" proc.Id str
        }

        let! portStr = Async.AwaitTask <| proc.StandardOutput.ReadLineAsync ()
        let port =
            match Int32.TryParse portStr with
            | true, port -> port
            | false, _ -> raise (new FormatException(sprintf "Failed to connect to server. Server gave an invalid port number: '%s'" portStr))

        Async.Start <| async {
            let! str = Async.AwaitTask <| proc.StandardOutput.ReadLineAsync ()
            logfInfo "(Test server @ pid %d): %s" proc.Id str
        }

        logfInfo "Attempting to connect to test runner server"
        let! client = TestRunnerServer.connectClient port
        logfInfo "Connected to test runner server successfully"

        // start a background thread to just send heartbeats
        Async.Start <| async {
            do! Async.Sleep (TestRunnerServer.defaultKeepAliveTimeout / 2)
            logfInfo "Keep alive sent"
            let request = TestRunnerServer.KeepAlive
            let! response = client.GetResponseAsync request
            match response with
            | TestRunnerServer.KeepAliveAcknowledged ->
                logfInfo "Keep alive acknowledged"
            | _ -> logfWarning "Server responded to %A with incorrect response (%A)" request response
        }

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