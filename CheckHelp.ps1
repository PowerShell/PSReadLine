
$about_topic = Get-Help about_PSReadline

$methods = ([PSConsoleUtilities.PSConsoleReadLine] |
    Get-Member -Static -Type Method |
    Where-Object Definition -match .*Nullable.*).Name

$methods | ForEach-Object {
    if ($about_topic -notlike "*$_*")
    {
        "Function not documented: $_"
    }
}

$commonParameters = echo Debug Verbose OutVariable OutBuffer ErrorAction WarningAction ErrorVariable WarningVariable PipelineVariable
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
