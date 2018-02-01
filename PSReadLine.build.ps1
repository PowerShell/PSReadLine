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

use 15.0 MSBuild

# Final bits to release go here
$targetDir = "bin/$Configuration/PSReadLine"


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
            Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $nugetExe
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
    Outputs = "PSReadLine/packages/Microsoft.PowerShell.5.ReferenceAssemblies.1.0.0/Microsoft.PowerShell.5.ReferenceAssemblies.1.0.0.nupkg"
}

<#
Synopsis: Restore PowerShell reference assemblies
#>
task RestoreNugetPackages @restoreNugetParameters CheckNugetInstalled,{
    exec { & $nugetExe restore PSReadLine/PSReadLine.sln }
}


$buildMamlParams = @{
    Inputs  = { Get-ChildItem docs/*.md }
    Outputs = "$targetDir/en-US/Microsoft.PowerShell.PSReadLine2.dll-help.xml"
}

<#
Synopsis: Generate maml help from markdown
#>
task BuildMamlHelp @buildMamlParams {
    platyPS\New-ExternalHelp docs -Force -OutputPath $targetDir/en-US/Microsoft.PowerShell.PSReadLine2.dll-help.xml
}

$buildAboutTopicParams = @{
    Inputs = {
         Get-ChildItem docs/about_PSReadLine.help.txt
         "PSReadLine/bin/$Configuration/Microsoft.PowerShell.PSReadLine2.dll"
         "$PSScriptRoot/GenerateFunctionHelp.ps1"
         "$PSScriptRoot/CheckHelp.ps1"
    }
    Outputs = "$targetDir/en-US/about_PSReadLine.help.txt"
}

<#
Synopsis: Generate about topic with function help
#>
task BuildAboutTopic @buildAboutTopicParams {
    # This step loads the dll that was just built, so only do that in another process
    # so the file isn't locked in any way for the rest of the build.

    $generatedFunctionHelpFile = New-TemporaryFile
    powershell -NoProfile -NonInteractive -File $PSScriptRoot/GenerateFunctionHelp.ps1 $Configuration $generatedFunctionHelpFile.FullName
    assert ($LASTEXITCODE -eq 0) "Generating function help failed"

    $functionDescriptions = Get-Content -Raw $generatedFunctionHelpFile
    $aboutTopic = Get-Content -Raw $PSScriptRoot/docs/about_PSReadLine.help.txt
    $newAboutTopic = $aboutTopic -replace '{{FUNCTION_DESCRIPTIONS}}', $functionDescriptions
    $newAboutTopic = $newAboutTopic -replace "`r`n","`n"
    $newAboutTopic | Out-File -FilePath $targetDir\en-US\about_PSReadLine.help.txt -NoNewline -Encoding ascii

    powershell -NoProfile -NonInteractive -File $PSScriptRoot/CheckHelp.ps1 $Configuration
    assert ($LASTEXITCODE -eq 0) "Checking help and function signatures failed"
}

$binaryModuleParams = @{
    Inputs  = { Get-ChildItem PSReadLine/*.cs, PSReadLine/PSReadLine.csproj, PSReadLine/PSReadLineResources.resx }
    Outputs = "PSReadLine/bin/$Configuration/Microsoft.PowerShell.PSReadLine2.dll"
}

<#
Synopsis: Build main binary module
#>
task BuildMainModule @binaryModuleParams RestoreNugetPackages, {
    exec { msbuild PSReadLine/PSReadLine.csproj /t:Rebuild /p:Configuration=$Configuration /p:Platform=AnyCPU }
}

<#
Synopsis: Generate the file catalog
#>
task GenerateCatalog {
    exec {
        Remove-Item -ea Ignore $PSScriptRoot/bin/$Configuration/PSReadLine/PSReadLine.cat
        $null = New-FileCatalog -CatalogFilePath $PSScriptRoot/bin/$Configuration/PSReadLine/PSReadLine.cat `
                                -Path $PSScriptRoot/bin/$Configuration/PSReadLine `
                                -CatalogVersion 2.0
    }
}

$buildTestParams = @{
    Inputs  = { Get-ChildItem TestPSReadLine/*.cs, TestPSReadLine/TestPSReadLine.csproj }
    Outputs = "TestPSReadLine/bin/$Configuration/TestPSReadLine.exe"
}

<#
Synopsis: Build executable for interactive testing/development
#>
task BuildTestHost @buildTestParams BuildMainModule, {
    exec { msbuild TestPSReadLine/TestPSReadLine.csproj /t:Rebuild /p:Configuration=$Configuration /p:Platform=AnyCPU }
}


$buildUnitTestParams = @{
    Inputs  = { Get-ChildItem test/*.cs, test/PSReadLine.Tests.csproj }
    Outputs = "test/bin/$Configuration/PSReadLine.Tests.dll"
}


<#
Synopsis: Build the unit tests
#>
task BuildTests @buildUnitTestParams BuildMainModule, {
    exec { msbuild test/PSReadLine.tests.csproj /t:Rebuild /p:Configuration=$Configuration /p:Platform=AnyCPU }
}


<#
Synopsis: Run the unit tests
#>
task RunTests BuildTests, {
    exec {
        $env:PSREADLINE_TESTRUN = 1
        $runner = "$PSScriptRoot\PSReadLine\packages\xunit.runner.console.2.3.1\tools\net452\xunit.console.exe"
        if ($env:APPVEYOR)
        {
            $outXml = "$PSScriptRoot\xunit-results.xml"
            & $runner $PSScriptRoot\test\bin\$Configuration\PSReadLine.Tests.dll -appveyor -xml $outXml
            $wc = New-Object 'System.Net.WebClient'
            $wc.UploadFile("https://ci.appveyor.com/api/testresults/xunit/$($env:APPVEYOR_JOB_ID)", $outXml)
        }
        else
        {
            & $runner $PSScriptRoot\test\bin\$Configuration\PSReadLine.Tests.dll
        }
        Remove-Item env:PSREADLINE_TESTRUN
    }
}

<#
Synopsis: Copy all of the files that belong in the module to one place in the layout for installation
#>
task LayoutModule BuildMainModule, BuildMamlHelp, {
    $extraFiles =
        'PSReadLine/Changes.txt',
        'PSReadLine/License.txt',
        'PSReadLine/SamplePSReadLineProfile.ps1',
        'PSReadLine/PSReadLine.format.ps1xml',
        'PSReadLine/PSReadLine.psm1'

    foreach ($file in $extraFiles)
    {
        Copy-Item $file $targetDir
    }


    Copy-Item PSReadLine/bin/$Configuration/Microsoft.PowerShell.PSReadLine2.dll $targetDir
    Copy-Item PSReadLine/bin/$Configuration/System.Runtime.InteropServices.RuntimeInformation.dll $targetDir

    # Copy module manifest, but fix the version to match what we've specified in the binary module.
    $version = (Get-ChildItem -Path $targetDir/Microsoft.PowerShell.PSReadLine2.dll).VersionInfo.FileVersion
    $moduleManifestContent = Get-Content -Path 'PSReadLine/PSReadLine.psd1' -Raw

    $b = Get-Content -Encoding Byte -Raw ./bin/$Configuration/PSReadLine/Microsoft.PowerShell.PSReadLine2.dll
    $a = [System.Reflection.Assembly]::Load($b)
    $semVer = ($a.GetCustomAttributes([System.Reflection.AssemblyInformationalVersionAttribute], $false)).InformationalVersion

    if ($semVer -match "(.*)-(.*)")
    {
        # Make sure versions match
        if ($matches[1] -ne $version) { throw "AssemblyFileVersion mismatch with AssemblyInformationalVersion" }
        $prerelease = $matches[2]

        # Put the prerelease tag in private data
        $moduleManifestContent = [regex]::Replace($moduleManifestContent, "}", "PrivateData = @{ PSData = @{ Prerelease = '$prerelease' } }$([System.Environment]::Newline)}")
    }

    $moduleManifestContent = [regex]::Replace($moduleManifestContent, "ModuleVersion = '.*'", "ModuleVersion = '$version'")
    $moduleManifestContent | Set-Content -Path $targetDir/PSReadLine.psd1

    # Make sure we don't ship any read-only files
    foreach ($file in (Get-ChildItem -Recurse -File $targetDir))
    {
        $file.IsReadOnly = $false
    }
}, BuildAboutTopic


<#
Synopsis: Zip up the binary for release.
#>
task ZipRelease RunTests, LayoutModule, {
    Compress-Archive -Force -LiteralPath $targetDir -DestinationPath "bin/$Configuration/PSReadLine.zip"
}


<#
Synopsis: Install newly built PSReadLine
#>
task Install LayoutModule, {

    function Install($InstallDir) {
        if (!(Test-Path -Path $InstallDir))
        {
            New-Item -ItemType Directory -Force $InstallDir
        }

        try
        {
            if (Test-Path -Path $InstallDir\PSReadLine)
            {
                Remove-Item -Recurse -Force $InstallDir\PSReadLine -ErrorAction Stop
            }
            Copy-Item -Recurse $targetDir $InstallDir
        }
        catch
        {
            Write-Error -Message "Can't install, module is probably in use."
        }
    }

    Install "$HOME\Documents\WindowsPowerShell\Modules"
    Install "$HOME\Documents\PowerShell\Modules"
}

<#
Synopsis: Publish to PSGallery
#>
task Publish -If ($Configuration -eq 'Release') LayoutModule, {

    $manifest = Import-PowerShellDataFile $PSScriptRoot/bin/Release/PSReadLine/PSReadLine.psd1

    $version = $manifest.ModuleVersion
    if ($null -ne $manifest.PrivateData)
    {
        $psdata = $manifest.PrivateData['PSData']
        if ($null -ne $psdata)
        {
            $prerelease = $psdata['Prerelease']
            if ($null -ne $prerelease)
            {
                $version = $version + '-' + $prerelease
            }
        }
    }

    $yes = Read-Host "Publish version $version (y/n)"

    if ($yes -ne 'y') { throw "Publish aborted" }

    $nugetApiKey = Read-Host "Nuget api key for PSGallery"

    $publishParams = @{
        Path = "$PSScriptRoot/bin/Release/PSReadLine"
        NuGetApiKey = $nugetApiKey
        Repository = "PSGallery"
        ReleaseNotes = (Get-Content -Raw $PSScriptRoot/bin/Release/PSReadLine/Changes.txt)
        ProjectUri = 'https://github.com/lzybkr/PSReadLine'
    }

    Publish-Module @publishParams
}

<#
Synopsis: Default build rule - build and create module layout
#>
task . LayoutModule, RunTests

