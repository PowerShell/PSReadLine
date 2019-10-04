<!--
Before submitting your bug report, please check for duplicates, and +1 the duplicate if you find one, adding additional details if you have any to add.

There are a few common issues that are commonly reported.

If there is an exception copying to/from the clipboard, it's probably the same as https://github.com/PowerShell/PSReadLine/issues/265
If there is an exception shortly after resizing the console, it's probably the same as https://github.com/PowerShell/PSReadLine/issues/292
-->

Environment data
----------------

<!--

The following script will generate the environment data that helps triage and investigate the issue.
Please run the script in the PowerShell session where you ran into the issue and provide the output here.

& {
    $hostName = $Host.Name
    if ($hostName -eq "ConsoleHost" -and (Get-Command Get-CimInstance)) {
        $id = $PID
        $inWindowsTerminal = $false
        while ($true) {
            $p = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId Like $id"
            if (!$p -or !$p.Name) { break }
            if ($p.Name -eq "WindowsTerminal.exe") { $inWindowsTerminal = $true; break }
            $id = $p.ParentProcessId
        }
        if ($inWindowsTerminal) { $hostName += " (Windows Terminal)" }
    }

    "`nPS version: $($PSVersionTable.PSVersion)"
    $v = (Get-Module PSReadline).Version
    $m = Get-Content "$(Split-Path -Parent (Get-Module PSReadLine).Path)\PSReadLine.psd1" | Select-String "Prerelease = '(.*)'"
    if ($m) {
        $v = "$v-" + $m.Matches[0].Groups[1].Value
    }
    "PSReadline version: $v"
    if ($IsLinux -or $IsMacOS) {
        "os: $(uname -a)"
    } else {
        "os: $((dir $env:SystemRoot\System32\cmd.exe).VersionInfo.FileVersion)"
    }
    "PS file version: $($name = if ($PSVersionTable.PSEdition -eq "Core") { "pwsh.dll" } else { "powershell.exe" }; (dir $pshome\$name).VersionInfo.FileVersion)"
    "HostName: $hostName"
    "BufferWidth: $([console]::BufferWidth)"
    "BufferHeight: $([console]::BufferHeight)`n"
}

-->

Steps to reproduce or exception report
--------------------------------------
