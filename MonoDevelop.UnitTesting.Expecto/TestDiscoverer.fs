namespace MonoDevelop.UnitTesting.Expecto
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
            logfInfo "(Test server @ pid %d): %s" proc.Id str
        }

        // HACK: I can't believe I actually wrote this... It's really really bad but gets the job done for now
        let mutable retriesLeft = 100
        let mutable client = None
        while (Option.isNone client && retriesLeft > 0) do
            try
                logfDebug "Attempting to connect to test runner server"
                let! c = TestRunnerServer.connectClient port
                client <- Some c
                logfDebug "Connected to test runner server successfully"
            with e ->
                ignore e
                // we can't catch just `SocketException`s because something is wrapping it in a System.Exception
                do! Async.Sleep 100
                logfDebug "Failed to open connection to test runner server; retrying..."

        match client with
        | Some client -> return new RemoteTestRunner(client, proc)
        | None -> return failwith "Client failed to connect to server after 100 retries (every 100ms)"
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