
param([switch]$Install)

add-type -AssemblyName System.IO.Compression.FileSystem

if (!(Get-Command -Name msbuild -ea Ignore))
{
    $env:path += ";${env:SystemRoot}\Microsoft.Net\Framework\v4.0.30319"
}

msbuild $PSScriptRoot\PSReadline\PSReadLine.sln /t:Rebuild /p:Configuration=Release

$targetDir = "${env:Temp}\PSReadline"

if (Test-Path -Path $targetDir)
{
    Remove-Item -re $targetDir
}

$null = New-Item -Path $targetDir -ItemType directory
$null = New-Item -Path $targetDir\en-US -ItemType directory

$files = @('PSReadline\Changes.txt',
           'PSReadline\License.txt',
           'PSReadline\SamplePSReadlineProfile.ps1',
           'PSReadline\PSReadline.psd1',
           'PSReadline\PSReadline.psm1',
           'PSReadline\PSReadline.format.ps1xml',
           'PSReadline\bin\Release\PSReadline.dll')

foreach ($file in $files)
{
    Copy-Item -Path $PSScriptRoot\$file -Destination $targetDir
}

$files = @('PSReadline\en-US\about_PSReadline.help.txt',
           'PSReadline\en-US\PSReadline.dll-help.xml')

foreach ($file in $files)
{
    Copy-Item -Path $PSScriptRoot\$file -Destination $targetDir\en-us
}

$version = (Get-ChildItem -Path $targetDir\PSReadline.dll).VersionInfo.FileVersion

& $PSScriptRoot\Update-ModuleManifest.ps1 $targetDir\PSReadline.psd1 $version

#make sure chocolatey is installed and in the path
if (Get-Command -Name cpack -ea Ignore)
{
    $chocolateyDir = "$PSScriptRoot\ChocolateyPackage"

    if (Test-Path -Path $chocolateyDir\PSReadline)
    {
        Remove-Item -Recurse -Path $chocolateyDir\PSReadline
    }

    & $PSScriptRoot\Update-NuspecVersion.ps1 "$chocolateyDir\PSReadline.nuspec" $version

    Copy-Item -Recurse -Path $targetDir -Destination $chocolateyDir\PSReadline

    cpack "$chocolateyDir\PSReadline.nuspec"
}

Remove-Item -Path $PSScriptRoot\PSReadline.zip -ea Ignore
[System.IO.Compression.ZipFile]::CreateFromDirectory($targetDir, "$PSScriptRoot\PSReadline.zip")

if ($Install)
{
    $InstallDir = "$HOME\Documents\WindowsPowerShell\Modules"

    if (!(Test-Path -Path $InstallDir))
    {
        New-Item -force -Path $InstallDir -ItemType Directory
    }

    try
    {
        if (Test-Path -Path $InstallDir\PSReadline)
        {
            Remove-Item -Recurse -force -Path $InstallDir\PSReadline -ea Stop
        }
        Copy-Item -Recurse -Path $targetDir -Destination $InstallDir
    }
    catch
    {
        Write-Error -Message "Can't install, module is probably in use."
    }
}
