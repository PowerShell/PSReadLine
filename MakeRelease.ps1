[CmdletBinding()]
param([switch]$Install)

# restore nuget packages
$nugetExe = (Get-Command nuget.exe -ea Ignore).Path
if ($null -eq $nugetExe)
{
    $nugetExe = "${env:TEMP}\nuget.exe"
    if (!(Test-Path $nugetExe))
    {
        iwr http://nuget.org/nuget.exe -OutFile $nugetExe
    }
}
& $nugetExe restore $PSScriptRoot\PSReadline\PSReadline.sln

# generate external help
if (!(Get-Module platyPS -List) -and !(Get-Module platyPS))
{
    Write-Error -Message "Requires platyPS to generate help: Install-Module platyPS; Import-Module platyPS"
}
else
{
    platyPS\New-ExternalHelp $PSScriptRoot\docs -Force -OutputPath $PSScriptRoot\PSReadline\en-US
}
# end generate external help

add-type -AssemblyName System.IO.Compression.FileSystem

if (-not(Get-Command -Name msbuild -ErrorAction Ignore))
{
    $env:path += ";${env:SystemRoot}\Microsoft.Net\Framework\v4.0.30319"
}

msbuild $PSScriptRoot\PSReadline\PSReadLine.sln /t:Rebuild /p:Configuration=Release

$targetDir = "${env:Temp}\PSReadline"

if (Test-Path -Path $targetDir)
{
    rmdir -Recurse -Force $targetDir
}

$null = mkdir $targetDir
$null = mkdir $targetDir\en-US

$files = @('PSReadline\Changes.txt',
           'PSReadline\License.txt',
           'PSReadline\SamplePSReadlineProfile.ps1',
           'PSReadline\PSReadline.psd1',
           'PSReadline\PSReadline.psm1',
           'PSReadline\bin\Release\Microsoft.PowerShell.PSReadline.dll')

foreach ($file in $files)
{
    copy $PSScriptRoot\$file $targetDir
}

$files = @('PSReadline\en-US\about_PSReadline.help.txt',
           'PSReadline\en-US\PSReadline.dll-help.xml')

foreach ($file in $files)
{
    copy $PSScriptRoot\$file $targetDir\en-us
}

foreach ($file in (dir -re -af $targetDir))
{
    $file.IsReadOnly = $false
}

$version = (Get-ChildItem -Path $targetDir\Microsoft.PowerShell.PSReadline.dll).VersionInfo.FileVersion

& $PSScriptRoot\Update-ModuleManifest.ps1 $targetDir\PSReadline.psd1 $version

del $PSScriptRoot\PSReadline.zip -ErrorAction Ignore
[System.IO.Compression.ZipFile]::CreateFromDirectory($targetDir, "$PSScriptRoot\PSReadline.zip")

if ($Install)
{
    $InstallDir = "$HOME\Documents\WindowsPowerShell\Modules"

    if (-not(Test-Path -Path $InstallDir))
    {
        mkdir -force $InstallDir
    }

    try
    {
        if (Test-Path -Path $InstallDir\PSReadline)
        {
            rmdir -Recurse -force $InstallDir\PSReadline -ErrorAction Stop
        }
        copy -Recurse $targetDir $InstallDir
    }
    catch
    {
        Write-Error -Message "Can't install, module is probably in use."
    }
}
