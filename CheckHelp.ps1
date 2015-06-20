Import-Module -Name PSReadline

$about_topic = Get-Help -Name about_PSReadline

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
        "Function $($method.Name) parameter names should be key and arg"
    }
    if (!$parameters[1].HasDefaultValue -or ($null -ne $parameters[1].DefaultValue))
    {
        "Function $($method.Name) arg parameter missing default"
    }
    if (!$parameters[0].HasDefaultValue -or ($null -ne $parameters[0].DefaultValue))
    {
        "Function $($method.Name) key parameter missing default"
    }
}

$methods.Name | ForEach-Object {
    if ($about_topic -cnotmatch "\n +$_ +")
    {
        "Function not documented: $_"
    }
}

$commonParameters = Write-Output Debug Verbose OutVariable OutBuffer ErrorAction WarningAction ErrorVariable WarningVariable PipelineVariable
Get-Command -Type Cmdlet -Module PSReadline |
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
                        "Parameter $parameterName not documented in cmdlet $cmdletName"
                    }
                }
            }
    }

Get-PSReadlineKeyHandler |
    Where-Object { $_.Function -eq $_.Description } |
    ForEach-Object {
        "Function missing description: $($_.Function)"
    }
