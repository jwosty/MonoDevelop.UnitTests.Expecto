namespace MonoDevelop.UnitTesting.Expecto
open System
open Mono.Addins
open Mono.Addins.Description

[<assembly: Addin(
    "ExpectoRunner",
    Namespace = "MonoDevelop.UnitTesting.Expecto",
    Version = "0.0"
)>]

[<assembly: AddinName("ExpectoRunner")>]
[<assembly: AddinCategory("IDE extensions")>]
[<assembly: AddinDescription("ExpectoRunner")>]
[<assembly: AddinAuthor("John Wostenberg")>]

do ()