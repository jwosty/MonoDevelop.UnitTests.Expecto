namespace MonoDevelop.UnitTesting.Expecto
open System
open System.Diagnostics
open System.IO
open System.Reflection
open Expecto
open Expecto.RunnerServer

type RemoteTestRunner(client: MessageClient<_,_>) =
    static member Start () =
        let port = 12050
        let execDir = Path.GetDirectoryName (typeof<RemoteTestRunner>.Assembly.Location)
        let assemblyName = "Expecto.RunnerServer.Net461.exe"
        logfDebug "execDir = %s" execDir
        logfDebug "assemblyName = %s" assemblyName
        let assemblyPath = Path.Combine (execDir, assemblyName)

        let startInfo = new ProcessStartInfo("mono", sprintf "%s 12050" assemblyPath,
                                             RedirectStandardOutput = true, RedirectStandardError = true,
                                             UseShellExecute = false)
        let proc = Process.Start startInfo

        Async.Start <| async {
            let! str = Async.AwaitTask <| proc.StandardOutput.ReadLineAsync ()
            logfInfo "(TEST SERVER MSG): %s" str
        }

        ()

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