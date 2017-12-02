
param(
    $Configuration = 'Release',

    [Parameter(Mandatory)]
    $OutFile
)

$errorActionPreference = "Stop"

$ourAssembly = "$PSScriptRoot\PSReadLine\bin\$Configuration\Microsoft.PowerShell.PSReadLine2.dll"

$t ='Microsoft.PowerShell.PSConsoleReadLine' -as [type]
if ($null -ne $t -and $t.Assembly.Location -ne $ourAssembly)
{
    # Make sure we're runnning in a non-interactive session by relaunching
    powershell -NoProfile -NonInteractive -File $PSCommandPath $Configuration $OutFile
    exit $LASTEXITCODE
}

try {

Import-Module "$PSScriptRoot\bin\$Configuration\PSReadLine\PSReadLine.psd1"

$helpContent = [xml](Get-Content "$PSScriptRoot\PSReadLine\bin\$Configuration\Microsoft.PowerShell.PSReadLine.xml")

Set-PSReadLineOption -EditMode Windows
$cmdKeyBindings = Get-PSReadLineKeyHandler -Bound

Set-PSReadLineOption -EditMode Emacs
$emacsKeyBindings = Get-PSReadLineKeyHandler -Bound

Set-PSReadLineOption -EditMode Vi
$viKeyBindings = Get-PSReadLineKeyHandler -Bound

$bindableFunctionsFromReflection = ((Get-Command Set-PSReadLineKeyHandler).Parameters['Function'].Attributes).ValidValues

[enum]::GetNames([Microsoft.PowerShell.KeyHandlerGroup]) |
    ForEach-Object { '{0},{1}' -f $_,[Microsoft.PowerShell.KeyHandler]::GetGroupingDescription($_) } |
    ForEach-Object { $groupingDescriptions = @{} } { $k,$v = $_ -split ',',2; $groupingDescriptions[$k] = $v }

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

$allFunctions = @{}

foreach ($fn in $bindableFunctionsFromReflection)
{
    $bindableFn = [BindableFunction]::new()
    $bindableFn.Function = $fn
    $bindableFn.Group = [Microsoft.PowerShell.PSConsoleReadLine]::GetDisplayGrouping($fn)

    $allFunctions[$fn] = $bindableFn
}

foreach ($member in $helpContent.doc.members.member)
{
    $re = [regex]'^M:Microsoft\.PowerShell\.PSConsoleReadLine\.([a-zA-Z]+)\(System\.Nullable\{System\.ConsoleKeyInfo\},System.Object\)$'
    $name = $member.GetAttribute('name')
    if ($name -match $re)
    {
        $fn = $matches[1]
        $bindableFn = $allFunctions[$fn]
        if ($null -eq $bindableFn) { continue }

        $summary = $member.summary
        if ($summary -is [System.Xml.XmlElement])
        {
            # Special case to handle a cref, this is not generic code
            $parts = foreach ($c in $summary.ChildNodes)
            {
                if ($c -is [System.Xml.XmlText])
                {
                    $c.InnerText
                }
                elseif ($c -is [System.Xml.XmlElement])
                {
                    $null = $c.cref -match $re
                    $matches[1]
                }
            }

            $summary = $parts -join ''
        }

        $bindableFn.Description = $summary.Trim()
    }
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


$indent = "      "
$maxWidth = 90

$allFunctions.Values |
    Group-Object -Property Group |
    Sort-Object {[Microsoft.PowerShell.KeyHandlerGroup]$_.Name} |
    ForEach-Object {
        $label = $groupingDescriptions[$_.Name]
        "  {0}`n  {1}`n" -f $label, ('-' * $label.Length)

        $_.Group | Sort-Object Function | ForEach-Object {
            $desc = $_.Description -replace "`n",'' -replace " +"," "
            if ($desc.Length - $indent.Length -gt $maxWidth)
            {
                # Wrap the text
                $sb = [System.Text.StringBuilder]::new()

                $col = $indent.Length
                $words = $desc -split ' '
                foreach ($word in $words)
                {
                    if ($col + $word.Length -gt $maxWidth)
                    {
                        $null = $sb.Append("`n$indent")
                        $col = $indent.Length
                    }

                    $col += ($word.Length + 1)
                    $null = $sb.Append($word)
                    $null = $sb.Append(" ")
                }

                $null = $sb.Remove($sb.Length - 1, 1)
                $desc = $sb.ToString()
            }

            $sb = [System.Text.StringBuilder]::new()

            $null = if ($_.CmdBindings.Length -gt 0) {
                $sb.Append($indent)
                $sb.Append("Cmd: <")
                $sb.Append($_.CmdBindings -join '>, <')
                $sb.Append('>')
            }
            $null = if ($_.EmacsBindings.Length -gt 0) {
                if ($sb.Length -gt 0) { $sb.AppendLine() }
                $sb.Append($indent)
                $sb.Append("Emacs: <")
                $sb.Append($_.EmacsBindings -join '>, <')
                $sb.Append('>')
            }
            $null = if ($_.ViInsBindings.Length -gt 0) {
                if ($sb.Length -gt 0) { $sb.AppendLine() }
                $sb.Append($indent)
                $sb.Append("Vi insert mode: <")
                $sb.Append($_.ViInsBindings -join '>, <')
                $sb.Append('>')
            }
            $null = if ($_.ViCmdBindings.Length -gt 0) {
                if ($sb.Length -gt 0) { $sb.AppendLine() }
                $sb.Append($indent)
                $sb.Append("Vi command mode: ")
                $sb.Append($_.ViCmdBindings -join ', ')
            }

            if ($sb.Length -eq 0)
            {
                $bindings = "${indent}Function is unbound."
            }
            else {
                $bindings = $sb.ToString()
            }

            "    {0}:`n`n{1}{2}`n`n{3}`n" -f $_.Function, $indent, $desc, $bindings
        }
    } | Out-File -LiteralPath $OutFile -Encoding ascii

} catch
{
    & {
        [Microsoft.PowerShell.PSConsoleReadLine].Assembly.Location | Out-String
        $_ | Out-String
        $_.Exception | Out-String
        $_.Exception.StackTrace | Out-String
        $_.Exception.InnerException | Out-String
        $_.Exception.InnerException.StackTrace | Out-String

    } | Out-Host
    exit 1
}

exit 0
