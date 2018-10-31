function global:PSConsoleHostReadLine
{
    ## Get the execution status of the last accepted user input.
    ## This needs to be done as the first thing because any script run will flush $?.
    $lastRunStatus = $?
    Microsoft.PowerShell.Core\Set-StrictMode -Off
    [Microsoft.PowerShell.PSConsoleReadLine, Microsoft.PowerShell.PSReadLine3, Version=3.0.0.0, Culture=neutral, PublicKeyToken=null]::ReadLine($host.Runspace, $ExecutionContext, $lastRunStatus)
}
