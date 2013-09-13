
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
