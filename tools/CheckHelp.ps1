param($Configuration = 'Release')

if ($PSEdition -ne "Core") {
    Write-Warning "Skip checking help content on Windows PowerShell because 'Update-Help -Module PSReadLine' doesn't work properly on Windows PowerShell."
    return
}

#region "Start a non-interactive session to load the private build if needed"

$RepoRoot = (Resolve-Path "$PSScriptRoot/..").Path
Import-Module $PSScriptRoot/helper.psm1

$t ='Microsoft.PowerShell.PSConsoleReadLine' -as [type]
if ($null -ne $t)
{
    # Make sure we're runnning in a non-interactive session by relaunching
    $psExePath = Get-PSExePath
    & $psExePath -NoProfile -NonInteractive -File $PSCommandPath $Configuration
    exit $LASTEXITCODE
}

$save_PSModulePath = $env:PSModulePath
$env:PSModulePath = "$RepoRoot\bin\$Configuration;${env:PSModulePath}"
Import-Module PSReadLine

#endregion

#region "Run Update-Help to get the latest help content"

$psDataFolder = if ($IsWindows) {
    Join-Path ([System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Personal)) "PowerShell"
} else {
    [System.Management.Automation.Platform]::SelectProductNameForDirectory("data")
}
$helpDirectory = Join-Path $psDataFolder "Help"
$psReadLineHelpDirectory = Join-Path $helpDirectory "PSReadLine"
if (Test-Path $psReadLineHelpDirectory -PathType Container) {
    Remove-Item $psReadLineHelpDirectory -Recurse -Force
}

try {
    $savedProgressPreference = $ProgressPreference
    $ProgressPreference = "SilentlyContinue"
    Update-Help -Module PSReadLine -UICulture en-US -Force
} finally {
    $ProgressPreference = $savedProgressPreference
}

$psReadLineAboutHelpFile = Get-ChildItem $psReadLineHelpDirectory -Include "about_PSReadLine.help.txt" -Recurse | ForEach-Object FullName
$about_topic = Get-Content $psReadLineAboutHelpFile -Raw

#endregion

#region "ReportError utility"

$errorCount = 0
function ReportError
{
    [CmdletBinding()]
    param([string]$msg)

    $script:errorCount++
    $host.UI.WriteErrorLine($msg)
}

#endregion

#region "Check all bindable functions: signature and documentation"

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

#endregion

#region "Check if cmdlet parameters are all documented"

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

#endregion

#region "Check if bindable functions are all with descriptions"

Get-PSReadLineKeyHandler -Bound -Unbound |
    Where-Object { $_.Function -eq $_.Description } |
    ForEach-Object {
        ReportError "Function missing description: $($_.Function)"
    }

#endregion

#region "Check if default key bindings are all documented"

class BindableFunction
{
    [string]$Function
    [string]$Description
    [string[]]$CmdBindings = @()
    [string[]]$EmacsBindings = @()
    [string[]]$ViInsBindings = @()
    [string[]]$ViCmdBindings = @()
    # Can't use the following type because it's not loaded at parse time
    <#[Microsoft.PowerShell.KeyHandlerGroup]#>[object]$Group
}

function AppendTextWithLineWrapping
{
    param(
        [int] $Column,
        [string] $EndOfLine,
        [string] $LineToAppend,
        [System.Text.StringBuilder] $Buffer
    )

    $maxWidth = 80
    $indent = "    "
    if ($LineToAppend + $Column -lt $maxWidth)
    {
        $Buffer.Append($LineToAppend)
    }
    else
    {
        # Wrap the text
        $col = $Column
        $words = $LineToAppend -split ' '
        foreach ($word in $words)
        {
            if ($col + $word.Length -ge $maxWidth)
            {
                if ($Buffer[$Buffer.Length - 1] -eq " ")
                {
                    $Buffer.Remove($Buffer.Length - 1, 1)
                }

                $Buffer.Append("${EndOfLine}${indent}")
                $col = $indent.Length
            }

            $col += ($word.Length + 1)
            $Buffer.Append($word)
            $Buffer.Append(" ")
        }

        $Buffer.Remove($Buffer.Length - 1, 1)
    }
}

Set-PSReadLineOption -EditMode Windows
$cmdKeyBindings = Get-PSReadLineKeyHandler -Bound

Set-PSReadLineOption -EditMode Emacs
$emacsKeyBindings = Get-PSReadLineKeyHandler -Bound

Set-PSReadLineOption -EditMode Vi
$viKeyBindings = Get-PSReadLineKeyHandler -Bound

$bindableFunctionsFromReflection = ((Get-Command Set-PSReadLineKeyHandler).Parameters['Function'].Attributes).ValidValues
$allFunctions = @{}

foreach ($fn in $bindableFunctionsFromReflection)
{
    $bindableFn = [BindableFunction]::new()
    $bindableFn.Function = $fn
    $bindableFn.Group = [Microsoft.PowerShell.PSConsoleReadLine]::GetDisplayGrouping($fn)

    $allFunctions[$fn] = $bindableFn
}

foreach ($binding in $cmdKeyBindings)
{
    $allFunctions[$binding.Function].CmdBindings += $binding.Key
}

foreach ($binding in $emacsKeyBindings)
{
    $allFunctions[$binding.Function].EmacsBindings += $binding.Key
}

foreach ($binding in $viKeyBindings)
{
    $mode = if ($binding.Key[0] -eq '<') { 'ViCmdBindings' } else { 'ViInsBindings' }
    # Some vi functions are private but are stil reported by Get-PSReadLineKeyHandler
    $bindableFn = $allFunctions[$binding.Function]
    if ($null -ne $bindableFn)
    {
        $bindableFn.$mode += $binding.Key
    }
}

# Use CRLF explicitly because the 'about_PSReadLine.help.txt' pulled down by 'Update-Help' uses CRLF end-of-line sequence.
$eol = "`r`n"
$indent = "-   "
$mismatchFound = $false

$allFunctions.Values |
    Group-Object -Property Group |
    Sort-Object {[Microsoft.PowerShell.KeyHandlerGroup]$_.Name} |
    ForEach-Object {

        $_.Group | Sort-Object Function | ForEach-Object {
            $sb = [System.Text.StringBuilder]::new()

            $null = if ($_.CmdBindings.Length -gt 0) {
                $sb.Append($indent)
                $sb.Append("Cmd: <")
                AppendTextWithLineWrapping -Column 10 -EndOfLine $eol -LineToAppend ($_.CmdBindings -join '>, <') -Buffer $sb
                $sb.Append('>')
            }
            $null = if ($_.EmacsBindings.Length -gt 0) {
                if ($sb.Length -gt 0) { $sb.Append($eol) }
                $sb.Append($indent)
                $sb.Append("Emacs: <")
                AppendTextWithLineWrapping -Column 12 -EndOfLine $eol -LineToAppend ($_.EmacsBindings -join '>, <') -Buffer $sb
                $sb.Append('>')
            }
            $null = if ($_.ViInsBindings.Length -gt 0) {
                if ($sb.Length -gt 0) { $sb.Append($eol) }
                $sb.Append($indent)
                $sb.Append("Vi insert mode: <")
                AppendTextWithLineWrapping -Column 21 -EndOfLine $eol -LineToAppend ($_.ViInsBindings -join '>, <') -Buffer $sb
                $sb.Append('>')
            }
            $null = if ($_.ViCmdBindings.Length -gt 0) {
                if ($sb.Length -gt 0) { $sb.Append($eol) }
                $sb.Append($indent)
                $sb.Append("Vi command mode: ")
                AppendTextWithLineWrapping -Column 21 -EndOfLine $eol -LineToAppend ($_.ViCmdBindings -join ', ') -Buffer $sb
            }

            if ($sb.Length -eq 0)
            {
                $bindings = "${indent}Function is unbound."
            }
            else {
                $bindings = $sb.ToString()
            }

            $pattern = "*${eol}{0}${eol}*${eol}{1}${eol}${eol}*" -f $_.Function, $bindings
            if ($about_topic -notlike $pattern)
            {
                if (!$mismatchFound) {
                    $mismatchFound = $true
                    $host.UI.WriteErrorLine("`nMismatch found in 'about_PSReadLine.help.txt' for the following key bindings:")
                }
                ReportError -msg $pattern.Substring(1, $pattern.Length - 2)
            }
        }
    }

#endregion

$env:PSModulePath = $save_PSModulePath
exit $errorCount
