# MonoDevelop.UnitTests.Expecto
*A MonoDevelop / Visual Studio for Mac addin that provides an [Expecto](https://github.com/haf/expecto) test adapter*

This project is in its early stages. Contributions are always welcome.

## Building and running
Open MonoDevelop.UnitTests.Expecto.sln and run the MonoDevelop.UnitTests.Expecto project from Visual Studio for Mac / MonoDevelop.

## Installing
Run `msbuild MonoDevelop.UnitTests.Expecto.sln /target:PackageAddin`. In the IDE, do `Extensions > Install from file` and select the `.mpack` that should now be in the build directory.
