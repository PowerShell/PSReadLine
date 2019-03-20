[CmdletBinding()]
param($maxKeys = 25)

class KeyInfo
{
    KeyInfo([string]$k, [ConsoleKeyInfo]$ki, [bool]$investigate)
    {
        $this.Key = $k
        $this.KeyChar = $ki.KeyChar
        $this.ConsoleKey = $ki.Key
        $this.Modifiers = $ki.Modifiers
        $this.Investigate = $investigate
    }

    [string]$Key
    [string]$KeyChar
    [string]$ConsoleKey
    [string]$Modifiers
    [bool]$Investigate
}

$quit = $false

function ReadOneKey {
    param(
        [string]$key,
        [string]$prompt = 'Enter <{0}>'
    )

    function Test-ConsoleKeyInfos($k1, $k2, $key) {
        if ($k1.Modifiers -ne $k2.Modifiers) {
            # Some differences are OK
            if ($key.Length -eq 1) {
                # Don't care about Shift or Alt (because of AltGr), but
                # Control should match
                if ($k1.Modifiers -band [ConsoleModifiers]::Control -or
                    $k2.Modifiers -band [ConsoleModifiers]::Control) {
                    return $false
                }
            }
        }
        if ($k1.Key -ne $k2.Key) {
            $keyOk = $false
            switch -regex ($k1.Key,$k2.Key) {
            '^Oem.*' { $keyOk = $true; break }
            '^D[0-9]$' { $keyOk = $true; break }
            '^NumPad[0-9]$' { $keyOk = $true; break }
            }
            if (!$keyOk) {
                return $false
            }
        }

        return $k1.KeyChar -eq $k2.KeyChar
    }

    $expectedKi = [Microsoft.PowerShell.ConsoleKeyChordConverter]::Convert($key)[0]

    Write-Host -NoNewline ("`n${prompt}: " -f $key)
    $ki = [Console]::ReadKey($true)
    if ($ki.KeyChar -ceq 'Q') { $script:quit = $true; return }

    $investigate = $false
    $doubleChecks = 0
    while (!(Test-ConsoleKeyInfos $ki $expectedKi $key)) {
        $doubleChecks++

        if ($doubleChecks -eq 1) {
            Write-Host -NoNewline "`nDouble checking that last result, enter <${k}> again: "
        } else {
            Write-Host -NoNewline "`nLast result not confirmed, enter <Spacebar> to skip or enter <${k}> to try again: "
            $investigate = $true
        }
        $kPrev = $ki
        $ki = [Console]::ReadKey($true)
        if ($ki.KeyChar -ceq 'Q') { $quit = $true; return }
        if ($ki.Key -eq [ConsoleKey]::Spacebar) {
            $ki = $kPrev
            break
        }

        if (Test-ConsoleKeyInfos $ki $kPrev $key) {
            break
        }
    }

    return [KeyInfo]::new($key, $ki, $investigate)
}

$setConsoleInputMode = $false
try
{
    if ($PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows)
    {
        Add-Type @"
            using System;
            using System.Runtime.InteropServices;

            public class KeyInfoNativeMethods
            {
                [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
                public static extern IntPtr GetStdHandle(int handleId);

                [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
                public static extern bool GetConsoleMode(IntPtr hConsoleOutput, out uint dwMode);

                [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
                public static extern bool SetConsoleMode(IntPtr hConsoleOutput, uint dwMode);

                public static uint GetConsoleInputMode()
                {
                    var handle = GetStdHandle(-10);
                    uint mode;
                    GetConsoleMode(handle, out mode);
                    return mode;
                }

                public static void SetConsoleInputMode(uint mode)
                {
                    var handle = GetStdHandle(-10);
                    SetConsoleMode(handle, mode);
                }
            }
"@

        [Flags()]
        enum ConsoleModeInputFlags
        {
            ENABLE_PROCESSED_INPUT = 0x0001
            ENABLE_LINE_INPUT = 0x0002
            ENABLE_ECHO_INPUT = 0x0004
            ENABLE_WINDOW_INPUT = 0x0008
            ENABLE_MOUSE_INPUT = 0x0010
            ENABLE_INSERT_MODE = 0x0020
            ENABLE_QUICK_EDIT_MODE = 0x0040
            ENABLE_EXTENDED_FLAGS = 0x0080
            ENABLE_AUTO_POSITION = 0x0100
            ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0200
        }

        $prevMode = [KeyInfoNativeMethods]::GetConsoleInputMode()
        $mode = $prevMode -band
                -bnot ([ConsoleModeInputFlags]::ENABLE_PROCESSED_INPUT -bor
                        [ConsoleModeInputFlags]::ENABLE_LINE_INPUT  -bor
                        [ConsoleModeInputFlags]::ENABLE_WINDOW_INPUT -bor
                        [ConsoleModeInputFlags]::ENABLE_MOUSE_INPUT)
        Write-Verbose "Setting mode $mode"
        [KeyInfoNativeMethods]::SetConsoleInputMode($mode)
        $setConsoleInputMode = $true
    }
    else {
        [Console]::TreatControlCAsInput = $true
    }

    $keyData = [System.Collections.Generic.List[KeyInfo]]::new()

    $keys = Get-Content $PSScriptRoot\keydata.txt
    $keys = $keys | Get-Random -Count $keys.Count
    for ($i = 0; $i -lt $keys.Count; $i++) {
        $k = $keys[$i]
        if ($k -ceq 'Q') { continue }
        $ki = ReadOneKey $k
        if ($quit) { break }
        $keyData.Add($ki)
    }
}
finally
{
    if ($setConsoleInputMode) {
        [KeyInfoNativeMethods]::SetConsoleInputMode($prevMode)
    }
    else {
        [Console]::TreatControlCAsInput = $false
    }

    $keyData | ConvertTo-Json | Out-File -Encoding ascii KeyInfo.json
}
