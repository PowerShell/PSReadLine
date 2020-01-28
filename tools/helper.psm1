
$MinimalSDKVersion = '2.1.802'
$IsWindowsEnv = [System.Environment]::OSVersion.Platform -eq "Win32NT"
$RepoRoot = (Resolve-Path "$PSScriptRoot/..").Path
$LocalDotnetDirPath = if ($IsWindowsEnv) { "$env:LocalAppData\Microsoft\dotnet" } else { "$env:HOME/.dotnet" }

<#
.SYNOPSIS
    Get the path of the currently running powershell executable.
#>
function Get-PSExePath
{
    if (-not $Script:PSExePath) {
        $Script:PSExePath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
    }
    return $Script:PSExePath
}

<#
.SYNOPSIS
    Find the dotnet SDK that meets the minimal version requirement.
#>
function Find-Dotnet
{
    $dotnetFile = if ($IsWindowsEnv) { "dotnet.exe" } else { "dotnet" }
    $dotnetExePath = Join-Path -Path $LocalDotnetDirPath -ChildPath $dotnetFile

    # If dotnet is already in the PATH, check to see if that version of dotnet can find the required SDK.
    # This is "typically" the globally installed dotnet.
    $foundDotnetWithRightVersion = $false
    $dotnetInPath = Get-Command 'dotnet' -ErrorAction Ignore
    if ($dotnetInPath) {
        $foundDotnetWithRightVersion = Test-DotnetSDK $dotnetInPath.Source
    }

    if (-not $foundDotnetWithRightVersion) {
        if (Test-DotnetSDK $dotnetExePath) {
            Write-Warning "Can't find the dotnet SDK version $MinimalSDKVersion or higher, prepending '$LocalDotnetDirPath' to PATH."
            $env:PATH = $LocalDotnetDirPath + [IO.Path]::PathSeparator + $env:PATH
        }
        else {
            throw "Cannot find the dotnet SDK for .NET Core 2.1. Please specify '-Bootstrap' to install build dependencies."
        }
    }
}

<#
.SYNOPSIS
    Check if the dotnet SDK meets the minimal version requirement.
#>
function Test-DotnetSDK
{
    param($dotnetExePath)

    if (Test-Path $dotnetExePath) {
        $installedVersion = & $dotnetExePath --version
        return $installedVersion -ge $MinimalSDKVersion
    }
    return $false
}

<#
.SYNOPSIS
    Install the dotnet SDK if we cannot find an existing one.
#>
function Install-Dotnet
{
    [CmdletBinding()]
    param(
        [string]$Channel = 'release',
        [string]$Version = '2.1.802'
    )

    try {
        Find-Dotnet
        return  # Simply return if we find dotnet SDk with the correct version
    } catch { }

    $logMsg = if (Get-Command 'dotnet' -ErrorAction Ignore) {
        "dotnet SDK out of date. Require '$MinimalSDKVersion' but found '$dotnetSDKVersion'. Updating dotnet."
    } else {
        "dotent SDK is not present. Installing dotnet SDK."
    }
    Write-Log $logMsg -Warning

    $obtainUrl = "https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain"

    try {
        Remove-Item $LocalDotnetDirPath -Recurse -Force -ErrorAction Ignore
        $installScript = if ($IsWindowsEnv) { "dotnet-install.ps1" } else { "dotnet-install.sh" }
        Invoke-WebRequest -Uri $obtainUrl/$installScript -OutFile $installScript

        if ($IsWindowsEnv) {
            & .\$installScript -Channel $Channel -Version $Version
        } else {
            bash ./$installScript -c $Channel -v $Version
        }
    }
    finally {
        Remove-Item $installScript -Force -ErrorAction Ignore
    }
}

<#
.SYNOPSIS
    Write log message for the build.
#>
function Write-Log
{
    param(
        [string] $Message,
        [switch] $Warning,
        [switch] $Indent
    )

    $foregroundColor = if ($Warning) { "Yellow" } else { "Green" }
    $indentPrefix = if ($Indent) { "    " } else { "" }
    Write-Host -ForegroundColor $foregroundColor "${indentPrefix}${Message}"
}


$KeyboardLayoutHelperCode = @'
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

<#
.SYNOPSIS
    Start to run the xUnit tests.
#>
function Start-TestRun
{
    param(
        [string]
        $Configuration,

        [string]
        $Framework
    )

    $testResultFolder = 'TestResults'

    function RunXunitTestsInNewProcess ([string] $Layout, [string] $OperatingSystem)
    {
        $filter = "FullyQualifiedName~Test.{0}_{1}" -f ($Layout -replace '-','_'), $OperatingSystem
        $testResultFile = "xUnitTestResults.{0}.xml" -f $Layout
        $testResultFile = Join-Path $testResultFolder $testResultFile

        $stdOutput, $stdError = @(New-TemporaryFile; New-TemporaryFile)
        $arguments = 'test', '--no-build', '-c', $Configuration, '-f', $Framework, '--filter', $filter, '--logger', "xunit;LogFilePath=$testResultFile"

        Start-Process -FilePath dotnet -Wait -RedirectStandardOutput $stdOutput -RedirectStandardError $stdError -ArgumentList $arguments
        Get-Content $stdOutput, $stdError
        Remove-Item $stdOutput, $stdError
    }

    try
    {
        $env:PSREADLINE_TESTRUN = 1
        Push-Location "$RepoRoot/test"

        $xUnitTestExecuted = $true
        if ($IsWindowsEnv)
        {
            if ($env:APPVEYOR -or $env:TF_BUILD)
            {
                # AppVeyor CI builder only has en-US keyboard layout installed.
                # We have to run tests from a new process because `GetCurrentKeyboardLayout` simply fails when called from
                # the `pwsh` process started by AppVeyor. Our xUnit tests depends on `GetCurrentKeyboardLayout` to tell if
                # a test case should run.
                RunXunitTestsInNewProcess -Layout 'en-US' -OperatingSystem 'Windows'
            }
            else
            {
                if (-not ("KeyboardLayoutHelper" -as [type]))
                {
                    Add-Type $KeyboardLayoutHelperCode
                }

                try
                {
                    $xUnitTestExecuted = $false

                    # Remember the current keyboard layout, changes are system wide and restoring
                    # is the nice thing to do.
                    $savedLayout = [KeyboardLayoutHelper]::GetCurrentKeyboardLayout()

                    # We want to run tests in as many layouts as possible. We have key info
                    # data for layouts that might not be installed, and tests would fail
                    # if we don't set the system wide layout to match the key data we'll use.
                    $layouts = [KeyboardLayoutHelper]::GetKeyboardLayouts()
                    Write-Log "Available layouts: $layouts"

                    foreach ($layout in $layouts)
                    {
                        if (Test-Path "KeyInfo-${layout}-windows.json")
                        {
                            Write-Log "Testing $layout ..."
                            $null = [KeyboardLayoutHelper]::SetKeyboardLayout($layout)

                            # We have to use Start-Process so it creates a new window, because the keyboard
                            # layout change won't be picked up by any processes running in the current conhost.
                            RunXunitTestsInNewProcess -Layout $layout -OperatingSystem 'Windows'
                            $xUnitTestExecuted = $true
                        }
                        else
                        {
                            Write-Log "Testing not supported for the keyboard layout '$layout'."
                        }
                    }
                }
                finally
                {
                    # Restore the original keyboard layout
                    $null = [KeyboardLayoutHelper]::SetKeyboardLayout($savedLayout)
                }
            }
        }
        else
        {
            RunXunitTestsInNewProcess -Layout 'en-US' -OperatingSystem 'Linux'
        }

        if ($xUnitTestExecuted)
        {
            # Check to see if there were any failures in xUnit tests, and throw exception to fail the build if so.
            Get-ChildItem $testResultFolder | Test-XUnitTestResults > $null
        }
    }
    finally
    {
        Pop-Location
        Remove-Item env:PSREADLINE_TESTRUN
    }
}

<#
.SYNOPSIS
    Check to see if the xUnit test run was successful.
#>
function Test-XUnitTestResults
{
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string] $TestResultsFile
    )

    Process
    {
        if (-not (Test-Path $TestResultsFile))
        {
            throw "File not found $TestResultsFile"
        }

        try
        {
            $results = [xml] (Get-Content $TestResultsFile)
        }
        catch
        {
            throw "Cannot convert $TestResultsFile to xml : $($_.message)"
        }

        $failedTests = $results.assemblies.assembly.collection | Where-Object failed -gt 0

        if (-not $failedTests)
        {
            return $true
        }

        throw "$($failedTests.failed) tests failed"
    }
}
