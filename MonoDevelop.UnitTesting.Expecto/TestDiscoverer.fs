namespace MonoDevelop.UnitTesting.Expecto
open System
open System.Threading
open System.Diagnostics
open System.IO
open System.Reflection
open Expecto
open Expecto.RunnerServer

type RemoteTestRunner(client: MessageClient<_,_>, serverProcess: Process) =
    let mutable disposed = false
    let mutable cancelKeepAlive = None

    member this.Client =
        if disposed then invalidOp "Cannot access a disposed object."
        client

    interface IDisposable with
        member this.Dispose () =
            if not disposed then
                (client :> IDisposable).Dispose ()
                if not serverProcess.HasExited then
                    logfInfo "Closing server process with PID %A" serverProcess.Id
                    match cancelKeepAlive with
                    | Some (cancelKeepAlive: CancellationTokenSource) -> cancelKeepAlive.Cancel ()
                    | None -> ()
                    serverProcess.Kill ()
                disposed <- true

    member this.StartKeepAlives () =
        if disposed then invalidOp "Cannot access a disposed object."

        // TODO: maybe we should lock here -- this method might not be thread safe
        match cancelKeepAlive with
        | Some _ -> invalidOp "StartKeepAlives may not be called more than once"
        | None ->
            let cnclToken = new CancellationTokenSource()
            cancelKeepAlive <- Some cnclToken
            Async.Start (async {
                while true do
                    do! Async.Sleep (TestRunnerServer.defaultKeepAliveTimeout / 2)
                    let request = TestRunnerServer.KeepAlive
                    let! response = client.GetResponseAsync request
                    match response with
                    | TestRunnerServer.KeepAliveAcknowledged ->
                        logfInfo "KeepAlive acknowledged"
                    | _ -> logfWarning "Server responded to %A with incorrect response (%A)" request response
            }, cnclToken.Token)

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

        let testRunner = new RemoteTestRunner(client, proc)
        testRunner.StartKeepAlives ()
        return testRunner
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