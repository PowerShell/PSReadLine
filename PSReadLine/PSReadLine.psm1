
Add-Type -TypeDefinition @'
    using System;
    using System.Runtime.InteropServices;

    public static class ConsoleHelperNativeMethods
    {
        [DllImport( "kernel32.dll" )]
        public static extern IntPtr GetConsoleWindow();
    }
'@

# The purpose of the PSReadline module is to give a better experience in console-based
# hosts. If the host is not console-based, PSReadline can't do anything. Rather than
# having a list of hosts which we know are console-based, let's just check to see if the
# current process has a console associated with it.
if ([IntPtr]::Zero -eq [ConsoleHelperNativeMethods]::GetConsoleWindow())
{
    throw "The current Windows PowerShell host ('$($Host.Name)') is not a console-based host, therefore the PSReadline module is of no use to it."
}

#
# .SYNOPSIS
#
#     This function is called by the console host when reading input to execute commands.
#
function PSConsoleHostReadline
{
    [PSConsoleUtilities.PSConsoleReadLine]::ReadLine()
}

# Load history
Get-History | ForEach-Object { [PSConsoleUtilities.PSConsoleReadLine]::AddToHistory($_.CommandLine) }
