# MonoDevelop.UnitTests.Expecto
*A MonoDevelop / Visual Studio for Mac addin that provides an [Expecto](https://github.com/haf/expecto) test adapter*

This project is in its early stages. Contributions are always welcome.

## Test discovery
The addin currently discovers tests by searching the user assemblies for `TestsAttribute`, as in:

```fsharp
[<Tests>]
let test1 = test "hello world" { () }
```

## Building and running
Open MonoDevelop.UnitTests.Expecto.sln and run the MonoDevelop.UnitTests.Expecto project from Visual Studio for Mac / MonoDevelop.

## Installing
Run:

```bash
msbuild MonoDevelop.UnitTesting.Expecto/MonoDevelop.UnitTesting.Expecto.fsproj /p:Configuration=Release /p:InstallAddin=true
```

NOTE: This has project has not been tested on Windows MonoDevelop. It probably doesn't work (but it's probably not too much effort to make it work)