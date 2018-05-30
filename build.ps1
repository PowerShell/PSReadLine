$scriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet -eq $null)
{
    $dotnet = "~/.dotnet/dotnet"
    if (!(Test-Path $dotnet))
    {
        throw "Could not find 'dotnet' cli"
    }
}
Push-Location (Join-Path $scriptRoot dotnet)
try
{
    & $dotnet publish -f netstandard20
}
finally
{
    Pop-Location
}
