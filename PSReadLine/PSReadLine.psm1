
[bool]$firstTime = $true

#
# .SYNOPSIS
#
#     This function is called by the console host when reading input to execute commands.
#
function PSConsoleHostReadline
{
    if ($firstTime)
    {
        # This initialization is delayed until the first call so that
        # a profile with something like:
        #
        #     ipmo PSReadline
        #     Set-PSReadlineOption -HistorySaveStyle ??????
        #
        # PSReadline will then handle the history choice in a reasonable way
        # regardless of the style.

        $script:firstTime = $false

        $options = [PSConsoleUtilities.PSConsoleReadLine]::GetOptions()

        # Honor $MaximumHistoryCount, but get it safely in case it was removed in the profile.
        $MaximumHistoryCount = Get-Variable -ea Ignore -ValueOnly MaximumHistoryCount
        if ($MaximumHistoryCount -gt 0)
        {
            $options.MaximumHistoryCount = $MaximumHistoryCount
            [PSConsoleUtilities.PSConsoleReadLine]::SetOptions($options)
        }

        if ($options.HistorySaveStyle -eq [PSConsoleUtilities.HistorySaveStyle]::SaveNothing)
        {
            # PSReadline isn't saving history, but we might still have history to reuse
            Get-History | ForEach-Object { [PSConsoleUtilities.PSConsoleReadLine]::AddToHistory($_.CommandLine) }
        }
    }

    # PSHost doesn't expose it's runspace.  The InternalHost does, but that won't
    # work for arbitrary hosts, so we turn off strict mode.
    Set-StrictMode -Off
    $remoteRunspace = if ($host.IsRunspacePushed) { $host.Runspace } else { $null }

    [PSConsoleUtilities.PSConsoleReadLine]::ReadLine($remoteRunspace, $ExecutionContext)
}
 
