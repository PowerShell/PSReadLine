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

# Final bits to release go here
$targetDir = "bin/$Configuration/PSReadLine"

if ($PSVersionTable.PSEdition -eq "Core") {
    $target = "netcoreapp2.1"
} else {
    $target = "net461"
}

Write-Verbose "Building for '$target'" -Verbose

function ConvertTo-CRLF([string] $text) {
    $text.Replace("`r`n","`n").Replace("`n","`r`n")
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
         "PSReadLine/bin/$Configuration/$target/Microsoft.PowerShell.PSReadLine2.dll"
         "$PSScriptRoot/tools/GenerateFunctionHelp.ps1"
         "$PSScriptRoot/tools/CheckHelp.ps1"
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
    $powershell = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.Filename
    & $powershell -NoProfile -NonInteractive -File $PSScriptRoot/tools/GenerateFunctionHelp.ps1 $Configuration $generatedFunctionHelpFile.FullName
    assert ($LASTEXITCODE -eq 0) "Generating function help failed"

    $functionDescriptions = Get-Content -Raw $generatedFunctionHelpFile
    $aboutTopic = Get-Content -Raw $PSScriptRoot/docs/about_PSReadLine.help.txt
    $newAboutTopic = $aboutTopic -replace '{{FUNCTION_DESCRIPTIONS}}', $functionDescriptions
    $newAboutTopic = $newAboutTopic -replace "`r`n","`n"
    $newAboutTopic | Out-File -FilePath $targetDir\en-US\about_PSReadLine.help.txt -NoNewline -Encoding ascii

    & $powershell -NoProfile -NonInteractive -File $PSScriptRoot/tools/CheckHelp.ps1 $Configuration
    assert ($LASTEXITCODE -eq 0) "Checking help and function signatures failed"
}

$binaryModuleParams = @{
    Inputs  = { Get-ChildItem PSReadLine/*.cs, PSReadLine/PSReadLine.csproj, PSReadLine/PSReadLineResources.resx }
    Outputs = "PSReadLine/bin/$Configuration/$target/Microsoft.PowerShell.PSReadLine2.dll"
}

$xUnitTestParams = @{
    Inputs = { Get-ChildItem test/*.cs, test/*.json, test/PSReadLine.Tests.csproj }
    Outputs = "test/bin/$Configuration/$target/PSReadLine.Tests.dll"
}

$simulatorParams = @{
    Inputs = { Get-ChildItem TestPSReadLine/*.cs, TestPSReadLine/Program.manifest, TestPSReadLine/TestPSReadLine.csproj }
    Outputs = "TestPSReadLine/bin/$Configuration/$target/TestPSReadLine.dll"
}

<#
Synopsis: Build main binary module
#>
task BuildMainModule @binaryModuleParams {
    exec { dotnet publish -f $target -c $Configuration PSReadLine }
}

<#
Synopsis: Build xUnit tests
#>
task BuildXUnitTests @xUnitTestParams {
    exec { dotnet publish -f $target -c $configuration test }
}

<#
Synopsis: Build the console simulator.
#>
task BuildConsoleSimulator @simulatorParams {
    exec { dotnet publish -f $target -c $configuration TestPSReadLine }
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
task RunTests BuildMainModule, BuildXUnitTests, {
    $env:PSREADLINE_TESTRUN = 1

    Push-Location test
    if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT)
    {
        Add-Type @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

public class KeyboardLayoutHelper
{
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[] lpList);

    // Used when setting the layout.
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    // Used for getting the layout.
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    // Used in both getting and setting the layout
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr GetForegroundWindow();

    const int WM_INPUTLANGCHANGEREQUEST = 0x0050;

    private static string GetLayoutNameFromHKL(IntPtr hkl)
    {
        var lcid = (int)((uint)hkl & 0xffff);
        return (new CultureInfo(lcid)).Name;
    }

    public static IEnumerable<string> GetKeyboardLayouts()
    {
        int cnt = GetKeyboardLayoutList(0, null);
        var list = new IntPtr[cnt];
        GetKeyboardLayoutList(list.Length, list);

        foreach (var layout in list)
        {
            yield return GetLayoutNameFromHKL(layout);
        }
    }

    public static string GetCurrentKeyboardLayout()
    {
        uint processId;
        IntPtr layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), out processId));
        return GetLayoutNameFromHKL(layout);
    }

    public static IntPtr SetKeyboardLayout(string lang)
    {
        var layoutId = (new CultureInfo(lang)).KeyboardLayoutId;
        var layout = LoadKeyboardLayout(layoutId.ToString("x8"), 0x80);
        // Hacky, but tests are probably running in a console app and the layout change
        // is ignored, so post the layout change to the foreground window.
        PostMessage(GetForegroundWindow(), WM_INPUTLANGCHANGEREQUEST, 0, layoutId);
        // Wait a bit until the layout has been changed.
        do {
            Thread.Sleep(100);
        } while (GetCurrentKeyboardLayout() != lang);
        return layout;
    }
}
'@

        # Remember the current keyboard layout, changes are system wide and restoring
        # is the nice thing to do.
        $savedLayout = [KeyboardLayoutHelper]::GetCurrentKeyboardLayout()

        # We want to run tests in as many layouts as possible. We have key info
        # data for layouts that might not be installed, and tests would fail
        # if we don't set the system wide layout to match the key data we'll use.
        $layouts = [KeyboardLayoutHelper]::GetKeyboardLayouts()
        Write-Host "Available layouts:", $layouts -ForegroundColor Green
        foreach ($layout in $layouts)
        {
            if (Test-Path "KeyInfo-${layout}-windows.json")
            {
                Write-Host "Testing $layout" -ForegroundColor Green
                $null = [KeyboardLayoutHelper]::SetKeyboardLayout($layout)
                $os,$es = @(New-TemporaryFile; New-TemporaryFile)
                $filter = "FullyQualifiedName~Test.$($layout -replace '-','_')_Windows"
                exec {
                    # We have to use Start-Process so it creates a new window, because the keyboard
                    # layout change won't be picked up by any processes running in the current conhost.
                    $dnArgs = 'test', '--no-build', '-c', $configuration, '-f', $target, '--filter', $filter, '--logger', 'trx'
                    Start-Process -FilePath dotnet -Wait -RedirectStandardOutput $os -RedirectStandardError $es -ArgumentList $dnArgs
                    Get-Content $os,$es
                    Remove-Item $os,$es
                }
            }
        }
        # Restore the original keyboard layout
        $null = [KeyboardLayoutHelper]::SetKeyboardLayout($savedLayout)
    }
    else
    {
        exec { dotnet test --no-build -c $configuration -f $target --filter "FullyQualifiedName~Test.en_US_Linux" --logger trx }
    }
    Pop-Location

    Remove-Item env:PSREADLINE_TESTRUN
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
        # ensure files have \r\n line endings as the signing tool only uses those endings to avoid mixed endings
        $content = Get-Content -Path $file -Raw
        Set-Content -Path (Join-Path $targetDir (Split-Path $file -Leaf)) -Value (ConvertTo-CRLF $content) -Force
    }

    $binPath = "PSReadLine/bin/$Configuration/$target/publish"
    Copy-Item $binPath/Microsoft.PowerShell.PSReadLine2.dll $targetDir

    if (Test-Path $binPath/System.Runtime.InteropServices.RuntimeInformation.dll)
    {
        Copy-Item $binPath/System.Runtime.InteropServices.RuntimeInformation.dll $targetDir
    }
    else
    {
        Write-Warning "Build using $target is not sufficient to be downlevel compatible"
    }

    # Copy module manifest, but fix the version to match what we've specified in the binary module.
    $version = (Get-ChildItem -Path $targetDir/Microsoft.PowerShell.PSReadLine2.dll).VersionInfo.FileVersion
    $moduleManifestContent = ConvertTo-CRLF (Get-Content -Path 'PSReadLine/PSReadLine.psd1' -Raw)

    $getContentArgs = @{
        Raw = $true;
        Path = "./bin/$Configuration/PSReadLine/Microsoft.PowerShell.PSReadLine2.dll"
    }
    if ($PSVersionTable.PSEdition -eq 'Core')
    {
        $getContentArgs += @{AsByteStream = $true}
    }
    else
    {
        $getContentArgs += @{Encoding = "Byte"}
    }
    $b = Get-Content @getContentArgs
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
        ProjectUri = 'https://github.com/lzybkr/PSReadLine'
    }

    Publish-Module @publishParams
}

<#
Synopsis: Remove temporary items.
#>
task Clean {
    git clean -fdX
}

<#
Synopsis: Default build rule - build and create module layout
#>
task . LayoutModule, RunTests
