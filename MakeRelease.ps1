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

<<<<<<< HEAD
$null = New-Item -Path $targetDir -ItemType directory
$null = New-Item -Path $targetDir\en-US -ItemType directory
=======
$null = mkdir -Path $targetDir
$null = mkdir -Path $targetDir\en-US
>>>>>>> Updates to PowerShell scripts for the following:

$files = @('PSReadline\Changes.txt',
           'PSReadline\License.txt',
           'PSReadline\SamplePSReadlineProfile.ps1',
           'PSReadline\PSReadline.psd1',
           'PSReadline\PSReadline.psm1',
           'PSReadline\PSReadline.format.ps1xml',
           'PSReadline\bin\Release\PSReadline.dll')

ForEach-Object ($file in $files)
{
<<<<<<< HEAD
    Copy-Item -Path $PSScriptRoot\$file -Destination $targetDir
=======
    copy $PSScriptRoot\$file $targetDir
>>>>>>> Updates to PowerShell scripts for the following:
}

$files = @('PSReadline\en-US\about_PSReadline.help.txt',
           'PSReadline\en-US\PSReadline.dll-help.xml')

ForEach-Object ($file in $files)
{
<<<<<<< HEAD
    Copy-Item -Path $PSScriptRoot\$file -Destination $targetDir\en-us
=======
    copy $PSScriptRoot\$file $targetDir\en-us
>>>>>>> Updates to PowerShell scripts for the following:
}

$version = (Get-ChildItem -Path $targetDir\PSReadline.dll).VersionInfo.FileVersion

& $PSScriptRoot\Update-ModuleManifest.ps1 $targetDir\PSReadline.psd1 $version

#make sure chocolatey is installed and in the path
if (Get-Command -Name cpack -ea Ignore)
{
    $chocolateyDir = "$PSScriptRoot\ChocolateyPackage"

    if (Test-Path -Path $chocolateyDir\PSReadline)
    {
<<<<<<< HEAD
        Remove-Item -Recurse -Path $chocolateyDir\PSReadline
=======
        rmdir -re $chocolateyDir\PSReadline
>>>>>>> Updates to PowerShell scripts for the following:
    }

    & $PSScriptRoot\Update-NuspecVersion.ps1 "$chocolateyDir\PSReadline.nuspec" $version

<<<<<<< HEAD
    Copy-Item -Recurse -Path $targetDir -Destination $chocolateyDir\PSReadline
=======
    copy -re $targetDir $chocolateyDir\PSReadline
>>>>>>> Updates to PowerShell scripts for the following:

    cpack "$chocolateyDir\PSReadline.nuspec"
}

<<<<<<< HEAD
Remove-Item -Path $PSScriptRoot\PSReadline.zip -ea Ignore
=======
rm $PSScriptRoot\PSReadline.zip -ea Ignore
>>>>>>> Updates to PowerShell scripts for the following:
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
<<<<<<< HEAD
            Remove-Item -Recurse -force -Path $InstallDir\PSReadline -ea Stop
        }
        Copy-Item -Recurse -Path $targetDir -Destination $InstallDir
=======
            rm -re -force -Path $InstallDir\PSReadline -ea Stop
        }
        copy -Recurse -Path $targetDir -Destination $InstallDir
>>>>>>> Updates to PowerShell scripts for the following:
    }
    catch
    {
        Write-Error -Message "Can't install, module is probably in use."
    }
}
