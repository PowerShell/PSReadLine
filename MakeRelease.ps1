
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

cp $PSScriptRoot\PSReadline\bin\Release\PSReadline.dll $targetDir
cp -re $PSScriptRoot\PSReadline\en-US $targetDir
cp $PSScriptRoot\PSReadline\PSReadline.psd1 $targetDir
cp $PSScriptRoot\PSReadline\PSReadline.psm1 $targetDir
cp $PSScriptRoot\PSReadline\PSReadline.format.ps1xml $targetDir

del $PSScriptRoot\PSReadline.zip -ea Ignore
[System.IO.Compression.ZipFile]::CreateFromDirectory($targetDir, "$PSScriptRoot\PSReadline.zip")
