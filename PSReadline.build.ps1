#
# To build, make sure you've installed InvokeBuild
#   Install-Module -Repository PowerShellGallery -Name InvokeBuild -RequiredVersion 3.1.0
#
# Then:
#   Invoke-Build
#
# Or:
#   Invoke-Build -Task ZipRelease
#
# Or:
#   Invoke-Build -Configuration Debug
#
# etc.
#

[CmdletBinding()]
param([switch]$Install,
      [string]$Configuration = (property Configuration Release))

use 4.0 MSBuild

# Final bits to release go here
$targetDir = "bin/$Configuration/PSReadline"


<#
Synopsis: Ensure nuget is installed
#>
task CheckNugetInstalled `
{
    $script:nugetExe = (Get-Command nuget.exe -ea Ignore).Path
    if ($null -eq $nugetExe)
    {
        $script:nugetExe = "${env:TEMP}/nuget.exe"
        if (!(Test-Path $nugetExe))
        {
            Invoke-WebRequest http://nuget.org/nuget.exe -OutFile $nugetExe
        }
    }
}


<#
Synopsis: Ensure platyPS is installed
#>
task CheckPlatyPSInstalled `
{
    if ($null -eq (Get-Module -List platyPS))
    {
        Install-Module -Scope CurrentUser -Repository PSGallery -Name platyPS
    }
}


$restoreNugetParameters = @{
    Inputs  = "PSReadLine/packages.config"
    # We could look for other files, but this is probably good enough.
    Outputs = "PSReadLine/packages/Microsoft.PowerShell.3.ReferenceAssemblies.1.0.0/Microsoft.PowerShell.3.ReferenceAssemblies.1.0.0.nupkg"
}

<#
Synopsis: Restore PowerShell reference assemblies
#>
task RestoreNugetPackages @restoreNugetParameters CheckNugetInstalled,{
    exec { & $nugetExe restore PSReadline/PSReadline.sln }
}


$buildMamlParams = @{
    Inputs  = { Get-Item docs/*.md }
    Outputs = "$targetDir/en-US/PSReadline.dll-help.xml"
}

<#
Synopsis: Generate maml help from markdown
#>
task BuildMamlHelp @buildMamlParams {
    platyPS\New-ExternalHelp docs -Force -OutputPath $targetDir/en-US
}


$binaryModuleParams = @{
    Inputs  = { Get-ChildItem PSReadLine/*.cs, PSReadLine/PSReadLine.csproj, PSReadLine/PSReadLineResources.resx }
    Outputs = "PSReadLine/bin/$Configuration/Microsoft.PowerShell.PSReadLine.dll"
}

<#
Synopsis: Build main binary module
#>
task BuildMainModule @binaryModuleParams RestoreNugetPackages, {
    exec { msbuild PSReadline/PSReadLine.csproj /t:Rebuild /p:Configuration=$Configuration }
}


$buildTestParams = @{
    Inputs  = { Get-ChildItem TestPSReadLine/*.cs, TestPSReadLine/TestPSReadLine.csproj }
    Outputs = "TestPSReadLine/bin/$Configuration/TestPSReadLine.exe"
}

<#
Synopsis: Build executable for interactive testing/development
#>
task BuildTestHost @buildTestParams BuildMainModule, {
    exec { msbuild TestPSReadLine/TestPSReadLine.csproj /t:Rebuild /p:Configuration=$Configuration }
}


<#
Synopsis: Copy all of the files that belong in the module to one place in the layout for installation
#>
task LayoutModule BuildMainModule, BuildMamlHelp, {
    $extraFiles =
        'PSReadLine/Changes.txt',
        'PSReadLine/License.txt',
        'PSReadLine/SamplePSReadlineProfile.ps1',
        'PSReadLine/PSReadLine.psm1'

    foreach ($file in $extraFiles)
    {
        Copy-Item $file $targetDir
    }


    Copy-Item PSReadLine/bin/$Configuration/Microsoft.PowerShell.PSReadLine.dll $targetDir
    Copy-Item PSReadLine/en-US/about_PSReadline.help.txt $targetDir/en-US

    # Copy module manifest, but fix the version to match what we've specified in the binary module.
    $version = (Get-ChildItem -Path $targetDir/Microsoft.PowerShell.PSReadline.dll).VersionInfo.FileVersion
    $moduleManifestContent = Get-Content -Path 'PSReadLine/PSReadLine.psd1' -Raw
    [regex]::Replace($moduleManifestContent, "ModuleVersion = '.*'", "ModuleVersion = '$version'") | Set-Content -Path $targetDir/PSReadLine.psd1

    # Make sure we don't ship any read-only files
    foreach ($file in (Get-ChildItem -Recurse -File $targetDir))
    {
        $file.IsReadOnly = $false
    }
}


<#
Synopsis: Zip up the binary for release.
#>
task ZipRelease LayoutModule, {
    Compress-Archive -Force -LiteralPath $targetDir -DestinationPath "bin/$Configuration/PSReadline.zip"
}


<#
Synopsis: Install newly built PSReadline
#>
task Install LayoutModule, {
    $InstallDir = "$HOME\Documents\WindowsPowerShell\Modules"

    if (!(Test-Path -Path $InstallDir))
    {
        New-Item -ItemType Directory -Force $InstallDir
    }

    try
    {
        if (Test-Path -Path $InstallDir\PSReadline)
        {
            Remove-Item -Recurse -Force $InstallDir\PSReadline -ErrorAction Stop
        }
        Copy-Item -Recurse $targetDir $InstallDir
    }
    catch
    {
        Write-Error -Message "Can't install, module is probably in use."
    }
}


<#
Synopsis: Default build rule - build and create module layout
#>
task . LayoutModule

