namespace MonoDevelop.UnitTesting.Expecto

open System
open MonoDevelop.Core
open MonoDevelop.Core.Logging
open MonoDevelop.UnitTesting
open MonoDevelop.Projects
open Microsoft.FSharp.Core.Printf

[<AutoOpen>]
module Logging =
    let logf logLevel fmt = kprintf (fun str -> LoggingService.Log (logLevel, str)) fmt
    let logfInfo fmt = kprintf LoggingService.LogInfo fmt
    let logfDebug fmt = kprintf LoggingService.LogDebug fmt
    let logfWarning fmt = kprintf LoggingService.LogWarning fmt
    let logfError fmt = kprintf LoggingService.LogDebug fmt