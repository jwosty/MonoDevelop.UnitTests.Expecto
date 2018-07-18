namespace MonoDevelop.UnitTesting.Expecto

open System
open MonoDevelop.Core
open MonoDevelop.Core.Logging
open MonoDevelop.UnitTesting
open MonoDevelop.Projects
open Microsoft.FSharp.Core.Printf

[<AutoOpen>]
module Logging =
    let inline private log logLevel str =
        LoggingService.Log (logLevel, sprintf "Expecto test extension: %s" str)

    let logf logLevel fmt = kprintf (log logLevel) fmt
    let logfInfo fmt = kprintf (log LogLevel.Info) fmt
    let logfDebug fmt = kprintf (log LogLevel.Debug) fmt
    let logfWarning fmt = kprintf (log LogLevel.Warn) fmt
    let logfError fmt = kprintf (log LogLevel.Error) fmt

module HelperFunctions =
    let ensureNonEmptyName name =
        if String.IsNullOrEmpty name then "(Unnamed)" else name

    let inline inc (x: byref<_>) = x <- x + 1