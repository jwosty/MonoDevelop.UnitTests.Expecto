namespace MonoDevelop.UnitTesting.Expecto
open System
open Mono.Addins
open Mono.Addins.Description

[<assembly: Addin(
    "MonoDevelop.UnitTesting.Expecto",
    Namespace = "MonoDevelop.UnitTesting.Expecto",
    Version = "0.0.1-alpha"
)>]

[<assembly: AddinName("Expecto adapter")>]
[<assembly: AddinCategory("Testing")>]
[<assembly: AddinDescription("Expecto adapter for MonoDevelop / Visual Studio for Mac")>]
[<assembly: AddinAuthor("John Wostenberg")>]

do ()