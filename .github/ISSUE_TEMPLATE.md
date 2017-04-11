<!--

Before submitting your bug report, please check for duplicates.

There are a few common issues that are commonly reported.

If there is an exception copying to/from the clipboard, it's probably the same as https://github.com/lzybkr/PSReadLine/issues/265

If there is an exception shortly after resizing the console, it's probably the same as https://github.com/lzybkr/PSReadLine/issues/292

If you agree it's a duplicate, go ahead and +1 on the duplicate, and if you have additional details worth adding, please do so.


If it is a bug report:

- make sure you are able to repro it on the latest released version. 
You can install the latest version from https://github.com/lzybkr/
- Search the existing issues.
- Refer to the [FAQ](../docs/FAQ.md).
- Refer to the [known issues](../docs/KNOWNISSUES.md).
- Fill out the following repro template

If it's not a bug, please remove the template and elaborate the issue in your own words.
-->

Environment data
----------------

<!-- provide the output of the following: -->

```powershell
& {
    "PS version: $($PSVersionTable.PSVersion)"
    "PSReadline version: $((Get-Module PSReadline).Version)"
    if ($IsLinux -or $IsOSX) {
        "os: $(uname -a)"
    } else {
        "os: $((dir $env:SystemRoot\System32\cmd.exe).VersionInfo.FileVersion)"
    }
    "PS file version: $((dir $pshome\powershell.exe).VersionInfo.FileVersion)"
}

```

Steps to reproduce or exception report
--------------------------------------

