
param([switch]$Install)

add-type -AssemblyName System.IO.Compression.FileSystem

if (!(gcm msbuild -ea Ignore))
{
    $env:path += ";${env:SystemRoot}\Microsoft.Net\Framework\v4.0.30319"
}

msbuild $PSScriptRoot\PSReadline\PSReadLine.sln /t:Rebuild /p:Configuration=Release

$targetDir = "${env:Temp}\PSReadline"

if (Test-Path $targetDir)
{
    rmdir -re $targetDir
}

$null = mkdir $targetDir 
$null = mkdir $targetDir\en-US

$files = @('Changes.txt',
           'License.txt',
           'PSReadline\PSReadline.psd1',
           'PSReadline\PSReadline.psm1',
           'PSReadline\PSReadline.format.ps1xml',
           'PSReadline\PSReadline.format.ps1xml',
           'PSReadline\bin\Release\PSReadline.dll')

foreach ($file in $files)
{
    cp $PSScriptRoot\$file $targetDir
}

$files = @('PSReadline\en-US\about_PSReadline.help.txt',
           'PSReadline\en-US\PSReadline.dll-help.xml')

foreach ($file in $files)
{
    cp $PSScriptRoot\$file $targetDir\en-us
}

del $PSScriptRoot\PSReadline.zip -ea Ignore
[System.IO.Compression.ZipFile]::CreateFromDirectory($targetDir, "$PSScriptRoot\PSReadline.zip")

if ($Install)
{
    $InstallDir = "${env:HOME}\Documents\WIndowsPowerShell\Modules"

    if (!(Test-Path $InstallDir))
    {
        mkdir -force $InstallDir
    }

    try
    {
        if (Test-Path $InstallDir\PSReadline)
        {
            rm -Recurse -force $InstallDir\PSReadline -ea Stop
        }
        cp -Recurse $targetDir $InstallDir
    }
    catch
    {
        Write-Error "Can't install, module is probably in use."
    }
}
