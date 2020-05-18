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
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = (property Configuration Release),

    [ValidateSet("net461", "netcoreapp2.1")]
    [string]$Framework,

    [switch]$CheckHelpContent
)

Import-Module "$PSScriptRoot/tools/helper.psm1"

# Final bits to release go here
$targetDir = "bin/$Configuration/PSReadLine"

if (-not $Framework)
{
    $Framework = if ($PSVersionTable.PSEdition -eq "Core") { "netcoreapp2.1" } else { "net461" }
}

Write-Verbose "Building for '$Framework'" -Verbose

function ConvertTo-CRLF([string] $text) {
    $text.Replace("`r`n","`n").Replace("`n","`r`n")
}

$binaryModuleParams = @{
    Inputs  = { Get-ChildItem PSReadLine/*.cs, PSReadLine/PSReadLine.csproj, PSReadLine/PSReadLineResources.resx }
    Outputs = "PSReadLine/bin/$Configuration/$Framework/Microsoft.PowerShell.PSReadLine2.dll"
}

$xUnitTestParams = @{
    Inputs = { Get-ChildItem test/*.cs, test/*.json, test/PSReadLine.Tests.csproj }
    Outputs = "test/bin/$Configuration/$Framework/PSReadLine.Tests.dll"
}

$mockPSConsoleParams = @{
    Inputs = { Get-ChildItem MockPSConsole/*.cs, MockPSConsole/Program.manifest, MockPSConsole/MockPSConsole.csproj }
    Outputs = "MockPSConsole/bin/$Configuration/$Framework/MockPSConsole.dll"
}

<#
Synopsis: Build main binary module
#>
task BuildMainModule @binaryModuleParams {
    exec { dotnet publish -f $Framework -c $Configuration PSReadLine }
}

<#
Synopsis: Build xUnit tests
#>
task BuildXUnitTests @xUnitTestParams {
    exec { dotnet publish -f $Framework -c $Configuration test }
}

<#
Synopsis: Build the mock powershell console.
#>
task BuildMockPSConsole @mockPSConsoleParams {
    exec { dotnet publish -f $Framework -c $Configuration MockPSConsole }
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

<#
Synopsis: Run the unit tests
#>
task RunTests BuildMainModule, BuildXUnitTests, { Start-TestRun -Configuration $Configuration -Framework $Framework }

<#
Synopsis: Check if the help content is in sync.
#>
task CheckHelpContent -If $CheckHelpContent {
    # This step loads the dll that was just built, so only do that in another process
    # so the file isn't locked in any way for the rest of the build.
    $psExePath = Get-PSExePath
    & $psExePath -NoProfile -NonInteractive -File $PSScriptRoot/tools/CheckHelp.ps1 $Configuration
    assert ($LASTEXITCODE -eq 0) "Checking help and function signatures failed"
}

<#
Synopsis: Copy all of the files that belong in the module to one place in the layout for installation
#>
task LayoutModule BuildMainModule, {
    if (-not (Test-Path $targetDir -PathType Container)) {
        New-Item $targetDir -ItemType Directory -Force > $null
    }

    $extraFiles =
        'License.txt',
        'PSReadLine/Changes.txt',
        'PSReadLine/SamplePSReadLineProfile.ps1',
        'PSReadLine/PSReadLine.format.ps1xml',
        'PSReadLine/PSReadLine.psm1'

    foreach ($file in $extraFiles)
    {
        # ensure files have \r\n line endings as the signing tool only uses those endings to avoid mixed endings
        $content = Get-Content -Path $file -Raw
        Set-Content -Path (Join-Path $targetDir (Split-Path $file -Leaf)) -Value (ConvertTo-CRLF $content) -Force
    }

    $binPath = "PSReadLine/bin/$Configuration/$Framework/publish"
    Copy-Item $binPath/Microsoft.PowerShell.PSReadLine2.dll $targetDir

    if (Test-Path $binPath/System.Runtime.InteropServices.RuntimeInformation.dll)
    {
        Copy-Item $binPath/System.Runtime.InteropServices.RuntimeInformation.dll $targetDir
    }
    else
    {
        Write-Warning "Build using $Framework is not sufficient to be downlevel compatible"
    }

    # Copy module manifest, but fix the version to match what we've specified in the binary module.
    $moduleManifestContent = ConvertTo-CRLF (Get-Content -Path 'PSReadLine/PSReadLine.psd1' -Raw)
    $versionInfo = (Get-ChildItem -Path $targetDir/Microsoft.PowerShell.PSReadLine2.dll).VersionInfo
    $version = $versionInfo.FileVersion
    $semVer = $versionInfo.ProductVersion

    if ($semVer -match "(.*)-(.*)")
    {
        # Make sure versions match
        if ($matches[1] -ne $version) { throw "AssemblyFileVersion mismatch with AssemblyInformationalVersion" }
        $prerelease = $matches[2]

        # Put the prerelease tag in private data
        $moduleManifestContent = [regex]::Replace($moduleManifestContent, "}", "PrivateData = @{ PSData = @{ Prerelease = '$prerelease'; ProjectUri = 'https://github.com/PowerShell/PSReadLine' } }$([System.Environment]::Newline)}")
    }

    $moduleManifestContent = [regex]::Replace($moduleManifestContent, "ModuleVersion = '.*'", "ModuleVersion = '$version'")
    $moduleManifestContent | Set-Content -Path $targetDir/PSReadLine.psd1

    # Make sure we don't ship any read-only files
    foreach ($file in (Get-ChildItem -Recurse -File $targetDir))
    {
        $file.IsReadOnly = $false
    }
}, CheckHelpContent

<#
Synopsis: Zip up the binary for release.
#>
task ZipRelease LayoutModule, {
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
task Publish -If ($Configuration -eq 'Release') {

    $binDir = "$PSScriptRoot/bin/Release/PSReadLine"

    # Check signatures before publishing
    Get-ChildItem -Recurse $binDir -Include "*.dll","*.ps*1" | Get-AuthenticodeSignature | ForEach-Object {
        if ($_.Status -ne 'Valid') {
            throw "$($_.Path) is not signed"
        }
        if ($_.SignerCertificate.Subject -notmatch 'CN=Microsoft Corporation.*') {
            throw "$($_.Path) is not signed with a Microsoft signature"
        }
    }

    # Check newlines in signed files before publishing
    Get-ChildItem -Recurse $binDir -Include "*.ps*1" | Get-AuthenticodeSignature | ForEach-Object {
        $lines = (Get-Content $_.Path | Measure-Object).Count
        $fileBytes = [System.IO.File]::ReadAllBytes($_.Path)
        $toMatch = ($fileBytes | ForEach-Object { "{0:X2}" -f $_ }) -join ';'
        $crlf = ([regex]::Matches($toMatch, ";0D;0A") | Measure-Object).Count

        if ($lines -ne $crlf) {
            throw "$($_.Path) appears to have mixed newlines"
        }
    }

    $manifest = Import-PowerShellDataFile $binDir/PSReadLine.psd1

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

    $nugetApiKey = Read-Host -AsSecureString "Nuget api key for PSGallery"

    $publishParams = @{
        Path = $binDir
        NuGetApiKey = [PSCredential]::new("user", $nugetApiKey).GetNetworkCredential().Password
        Repository = "PSGallery"
        ReleaseNotes = (Get-Content -Raw $binDir/Changes.txt)
        ProjectUri = 'https://github.com/PowerShell/PSReadLine'
    }

    Publish-Module @publishParams
}

<#
Synopsis: Remove temporary items.
#>
task Clean {
    git clean -fdx
}

<#
Synopsis: Default build rule - build and create module layout
#>
task . LayoutModule, RunTests
