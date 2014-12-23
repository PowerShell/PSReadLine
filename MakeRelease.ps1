param([switch]$Install, [switch]$BuildChocolatey)

add-type -AssemblyName System.IO.Compression.FileSystem

if (-not(Get-Command -Name msbuild -ErrorAction Ignore))
{
    $env:path += ";${env:SystemRoot}\Microsoft.Net\Framework\v4.0.30319"
}

msbuild $PSScriptRoot\PSReadline\PSReadLine.sln /t:Rebuild /p:Configuration=Release

$targetDir = "${env:Temp}\PSReadline"

if (Test-Path -Path $targetDir)
{
    rmdir -Recurse $targetDir
}

$null = mkdir $targetDir
$null = mkdir $targetDir\en-US

$files = @('PSReadline\Changes.txt',
           'PSReadline\License.txt',
           'PSReadline\SamplePSReadlineProfile.ps1',
           'PSReadline\PSReadline.psd1',
           'PSReadline\PSReadline.psm1',
           'PSReadline\PSReadline.format.ps1xml',
           'PSReadline\bin\Release\PSReadline.dll')

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

$version = (Get-ChildItem -Path $targetDir\PSReadline.dll).VersionInfo.FileVersion

& $PSScriptRoot\Update-ModuleManifest.ps1 $targetDir\PSReadline.psd1 $version

#make sure chocolatey is installed and in the path
if ($BuildChocolatey -and (Get-Command -Name cpack -ErrorAction Ignore))
{
    $chocolateyDir = "$PSScriptRoot\ChocolateyPackage"

    if (Test-Path -Path $chocolateyDir\PSReadline)
    {
        rmdir -Recurse $chocolateyDir\PSReadline
    }

    & $PSScriptRoot\Update-NuspecVersion.ps1 "$chocolateyDir\PSReadline.nuspec" $version

    copy -Recurse $targetDir $chocolateyDir\PSReadline

    cpack "$chocolateyDir\PSReadline.nuspec"
}

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
