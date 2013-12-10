
# Generate the ValidateSet attribute for the Builtin parameter
# Set-PSReadlineKeyHandler

$methods = ([PSConsoleUtilities.PSConsoleReadLine].GetMethods('public,static') |
    Where-Object {
        $method = $_
        $parameters = $method.GetParameters()
        $parameters.Count -eq 2 -and
            $parameters[0].ParameterType -eq [Nullable[ConsoleKeyInfo]] -and
            $parameters[1].ParameterType -eq [object]
    } |
    Sort-Object Name).Name

$indent = " " * 8
$prefix = "[ValidateSet("
$suffix = "]"

function GenerateStrings
{
    param([Parameter(ValueFromPipeline)]$Item)

    begin
    {
        $count = 0
        $comma = ""
        $buffer = New-Object System.Text.StringBuilder
    }

    process
    {
        $null = $buffer.Append($comma)
        if ($count -gt 0 -and ($count % 5) -eq 0)
        {
            # Remove the trailing space
            $null = $buffer.Remove($buffer.Length - 1, 1)
            $null = $buffer.Append("`n$indent$(' ' * $prefix.Length)")
        }
        $count++
        $null = $buffer.Append('"')
        $null = $buffer.Append($Item)
        $null = $buffer.Append('"')

        $comma = ", "
    }

    end
    {
        $buffer.ToString()
    }
}

"$indent$prefix$($methods | GenerateStrings))$suffix"
