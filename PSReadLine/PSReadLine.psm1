
#
# .SYNOPSIS
#
#     This function is called by the console host when reading input to execute commands.
#
function PSConsoleHostReadline
{
    [PSConsoleUtilities.PSConsoleReadLine]::ReadLine()
}
 
