# Check PowerShell version compatibility
if ($PSVersionTable.PSVersion -lt [Version]'7.4.0') {
    $errorMessage = @"
PSReadLine 3.0+ requires PowerShell 7.4 or later.
Current version: $($PSVersionTable.PSVersion)

To use PSReadLine with your PowerShell version, install an older version:
  Install-Module PSReadLine -RequiredVersion 2.4.5 -Force -SkipPublisherCheck

To upgrade PowerShell: https://aka.ms/install-powershell
"@
    throw $errorMessage
}

function PSConsoleHostReadLine
{
    [System.Diagnostics.DebuggerHidden()]
    param()

    ## Get the execution status of the last accepted user input.
    ## This needs to be done as the first thing because any script run will flush $?.
    $lastRunStatus = $?
    Microsoft.PowerShell.Core\Set-StrictMode -Off
    [Microsoft.PowerShell.PSConsoleReadLine]::ReadLine($host.Runspace, $ExecutionContext, $lastRunStatus)
}
