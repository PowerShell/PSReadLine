
# Generate the ValidateSet attribute for the Builtin parameter
# Set-PSReadlineKeyHandler

$methods = ([PSConsoleUtilities.PSConsoleReadLine] |
    Get-Member -Static -Type Method |
    Where-Object Definition -match .*Nullable.*).Name

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
