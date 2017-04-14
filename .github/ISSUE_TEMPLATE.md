<!--
Before submitting your bug report, please check for duplicates, and +1 the duplicate if you find one, adding additional details if you have any to add.

There are a few common issues that are commonly reported.

If there is an exception copying to/from the clipboard, it's probably the same as https://github.com/lzybkr/PSReadLine/issues/265

If there is an exception shortly after resizing the console, it's probably the same as https://github.com/lzybkr/PSReadLine/issues/292
-->

Environment data
----------------

<!-- provide the output of the following:
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
-->

Steps to reproduce or exception report
--------------------------------------
