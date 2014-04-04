
#
# .SYNOPSIS
#
#     This function is called by the console host when reading input to execute commands.
#
function PSConsoleHostReadline
{
    # PSHost doesn't expose it's runspace.  The InternalHost does, but that won't
    # work for arbitrary hosts, so we turn off strict mode.
    Set-StrictMode -Off
    $remoteRunspace = if ($host.IsRunspacePushed) { $host.Runspace } else { $null }
    [PSConsoleUtilities.PSConsoleReadLine]::ReadLine($remoteRunspace)
}
 
# Load history
Get-History | ForEach-Object { [PSConsoleUtilities.PSConsoleReadLine]::AddToHistory($_.CommandLine) }
