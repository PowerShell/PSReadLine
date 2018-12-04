[CmdletBindingAttribute()]
param($maxKeys = 25)

class KeyInfo
{
    KeyInfo([string]$k, [ConsoleKeyInfo]$ki)
    {
        $this.Key = $k
        $this.KeyChar = $ki.KeyChar
        $this.ConsoleKey = $ki.Key
        $this.Modifiers = $ki.Modifiers
    }

    [string]$Key
    [string]$KeyChar
    [string]$ConsoleKey
    [string]$Modifiers
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

    $allKeys = & {
        [string[]]$lc = [char]'a'..[char]'z'
        [string[]]$uc = [char]'A'..[char]'Z'
        [string[]]$digits = [char]'0'..[char]'9'
        [string[]]$symbols = -split '~ ` ! @ # $ % ^ & * ( ) - _ = + [ { ] } \ | ; : '' " , < . > / ?'
        [string[]]$rest = -split 'F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12 Insert Delete Home End PageUp PageDown Escape Tab LeftArrow RightArrow UpArrow DownArrow'

        $set = $lc, $uc, $digits, $symbols, $symbols, $symbols, $symbols, $symbols, $rest, $rest, $rest, $rest, $rest
        $prefix = '', 'Ctrl+', 'Alt+', 'Ctrl+Alt+'

        for ($i = 0; $i -lt $maxKeys; $i++) {
            $p = $prefix[(0..($prefix.Count) | Get-Random)]
            $k = "$p$(Get-Random -InputObject (Get-Random -InputObject $set -Count 1) -Count 1)"
            Write-Host -NoNewline "`nEnter ${k}: "
            [KeyInfo]::new($k, [Console]::ReadKey($true))
        }
    }

    $allKeys | ConvertTo-Json | Out-File -Encoding ascii KeyInfo.json
}
finally
{
    if ($setConsoleInputMode)
    {
        [KeyInfoNativeMethods]::SetConsoleInputMode($prevMode)
    }
}
