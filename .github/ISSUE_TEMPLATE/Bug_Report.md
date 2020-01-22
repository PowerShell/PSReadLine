---
name: "Bug report 🐛"
about: Report errors or unexpected behaviors 🤔
title: ''
labels: ''
assignees: ''

---

<!--

Before submitting your bug report ...
- Please make sure you are able to reproduce the issue with the latest version of PSReadLine.
- Please check for duplicates. +1 the duplicate if you find one and add additional details if you have any.

The maintainer may close your issue without further explanation or engagement if:
- You delete this entire template and go your own path;
- You file an issue that has many duplicates;
- You file an issue completely blank in the body.

-->

## Environment

```none
[run the script below and paste the output here]
```

<!--

The following script will generate the environment data that helps triage and investigate the issue.
Please run the script in the PowerShell session where you ran into the issue and provide the output above.

& {
    $hostName = $Host.Name
    if ($hostName -eq "ConsoleHost" -and (Get-Command Get-CimInstance -ErrorAction SilentlyContinue)) {
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

## Exception report

<!-- Copy and paste the keys and the exception stack trace printed by PSReadLine, if there is any -->

## Steps to reproduce

<!-- A description of how to trigger this bug. -->

## Expected behavior

<!-- A description of what you're expecting, possibly containing screenshots or reference material. -->

## Actual behavior

<!-- What's actually happening? -->
