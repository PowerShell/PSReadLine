# How to debug this module.

To prevent conflict with existing instance of PSRL, debug-version of PSRL must run side-by-side.

Therefore, the present commit changes the output of the build to `Microsoft.PowerShell.PSReadLine3.dll`.

To debug you need to perform the following steps:

- Cherry-pick the commit tagged `WORK`.
- Run the `build.ps1` build.
- Create a symbolic link like so:

```cmd
DOS> cd PSReadLine/bin/Debug
DOS> mklink /D PSReadLine3 net6.0 
```

Then you can register a new module in pwsh:

```pwsh
$Env:PSModulePath="..\PSReadLine\bin\Debug;$($EnvPSModulePath)"
```
