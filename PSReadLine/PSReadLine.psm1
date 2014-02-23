
if ($Host.Name -notin 'ConsoleHost', 'ColorConsoleHost')
{
    # This is the sort of error you would get if you were using the 'PowerShellHost' field
    # in the .psd1 to enforce a certain host name:
    #
    #    Import-Module : The name of the current Windows PowerShell host is: 'ColorConsoleHost'. The module
    #    'C:\Users\me\Documents\WindowsPowerShell\Modules\psreadline\psreadline.psd1' requires the following Windows
    #    PowerShell host: 'ConsoleHost'.
    #
    throw "The name of the current Windows PowerShell host is: '$($Host.Name)'. The PSReadline module does not support this host."
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
