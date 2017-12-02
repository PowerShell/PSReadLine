param($Configuration = 'Release')

$ourAssembly = "$PSScriptRoot\PSReadLine\bin\$Configuration\Microsoft.PowerShell.PSReadLine2.dll"

$t ='Microsoft.PowerShell.PSConsoleReadLine' -as [type]
if ($null -ne $t -and $t.Assembly.Location -ne $ourAssembly)
{
    # Make sure we're runnning in a non-interactive session by relaunching
    powershell -NoProfile -NonInteractive -File $PSCommandPath $Configuration
    exit $LASTEXITCODE
}

$save_PSModulePath = $env:PSModulePath
$env:PSModulePath = "$PSScriptRoot\bin\$Configuration;${env:PSModulePath}"
Import-Module PSReadLine

$errorCount = 0

function ReportError
{
    [CmdletBinding()]
    param([string]$msg)

    $script:errorCount++
    $host.UI.WriteErrorLine($msg)
}

$about_topic = Get-Content -Raw "$PSScriptRoot\bin\$Configuration\PSReadLine\en-US\about_PSReadLine.help.txt"

$methods = [Microsoft.PowerShell.PSConsoleReadLine].GetMethods('public,static') |
    Where-Object {
        $method = $_
        $parameters = $method.GetParameters()
        $parameters.Count -eq 2 -and
            $parameters[0].ParameterType -eq [Nullable[ConsoleKeyInfo]] -and
            $parameters[1].ParameterType -eq [object]
    }

foreach ($method in $methods)
{
    $parameters = $method.GetParameters()
    if ($parameters[0].Name -ne 'key' -or $parameters[1].Name -ne 'arg')
    {
        ReportError "Function $($method.Name) parameter names should be key and arg"
    }
    if (!$parameters[1].HasDefaultValue -or ($null -ne $parameters[1].DefaultValue))
    {
        ReportError "Function $($method.Name) arg parameter missing default"
    }
    if (!$parameters[0].HasDefaultValue -or ($null -ne $parameters[0].DefaultValue))
    {
        ReportError "Function $($method.Name) key parameter missing default"
    }
}

$methods.Name | ForEach-Object {
    if ($about_topic -cnotmatch $_)
    {
        ReportError "Function not documented: $_"
    }
}

$commonParameters = Write-Output Debug Verbose OutVariable OutBuffer ErrorAction WarningAction ErrorVariable WarningVariable PipelineVariable InformationAction InformationVariable
Get-Command -Type Cmdlet -Module PSReadLine |
    ForEach-Object {
        $cmdletInfo = $_
        $cmdletName = $cmdletInfo.Name
        $cmdletHelp = Get-Help -Detailed $cmdletName
        $cmdletInfo.Parameters.Keys |
            ForEach-Object {
                $parameterName = $_
                if ($parameterName -notin $commonParameters)
                {
                    $parameterHelp = $cmdletHelp.Parameters.parameter | Where-Object Name -eq $parameterName
                    if ($parameterHelp -eq $null)
                    {
                        ReportError "Parameter $parameterName not documented in cmdlet $cmdletName"
                    }
                }
            }
    }

Get-PSReadLineKeyHandler -Bound -Unbound |
    Where-Object { $_.Function -eq $_.Description } |
    ForEach-Object {
        ReportError "Function missing description: $($_.Function)"
    }


$env:PSModulePath = $save_PSModulePath
exit $errorCount
