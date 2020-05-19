<#
.SYNOPSIS
    A script that provides simple entry points for bootstrapping, building and testing.
.DESCRIPTION
    A script to make it easy to bootstrap, build and run tests.
.EXAMPLE
    PS > .\build.ps1 -Bootstrap
    Check and install prerequisites for the build.
.EXAMPLE
    PS > .\build.ps1 -Configuration Release -Framework net461
    Build the main module with 'Release' configuration and targeting 'net461'.
.EXAMPLE
    PS > .\build.ps1
    Build the main module with the default configuration (Debug) and the default target framework (determined by the current session).
.EXAMPLE
    PS > .\build.ps1 -Test
    Run xUnit tests with the default configuration (Debug) and the default target framework (determined by the current session).
.PARAMETER Clean
    Clean the local repo, but keep untracked files.
.PARAMETER Bootstrap
    Check and install the build prerequisites.
.PARAMETER Test
    Run tests.
.PARAMETER Configuration
    The configuration setting for the build. The default value is 'Debug'.
.PARAMETER Framework
    The target framework for the build.
    When not specified, the target framework is determined by the current PowerShell session:
    - If the current session is PowerShell Core, then use 'netcoreapp2.1' as the default target framework.
    - If the current session is Windows PowerShell, then use 'net461' as the default target framework.
#>
[CmdletBinding()]
param(
    [switch] $Clean,
    [switch] $Bootstrap,
    [switch] $Test,
    [switch] $CheckHelpContent,

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [ValidateSet("net461", "netcoreapp2.1")]
    [string] $Framework
)

# Clean step
if ($Clean) {
    try {
        Push-Location $PSScriptRoot
        git clean -fdX
        return
    } finally {
        Pop-Location
    }
}

Import-Module "$PSScriptRoot/tools/helper.psm1"

if ($Bootstrap) {
    Write-Log "Validate and install missing prerequisits for building ..."

    Install-Dotnet
    if (-not (Get-Module -Name InvokeBuild -ListAvailable)) {
        Write-Log -Warning "Module 'InvokeBuild' is missing. Installing 'InvokeBuild' ..."
        Install-Module -Name InvokeBuild -Scope CurrentUser -Force
    }

    return
}

# Common step required by both build and test
Find-Dotnet
if (-not (Get-Module -Name InvokeBuild -ListAvailable)) {
    throw "Cannot find the 'InvokeBuild' module. Please specify '-Bootstrap' to install build dependencies."
}

# Build/Test step
$buildTask = if ($Test) { "RunTests" } else { "ZipRelease" }
$arguments = @{ Task = $buildTask; Configuration = $Configuration }

if ($Framework) { $arguments.Add("Framework", $Framework) }
if ($CheckHelpContent) { $arguments.Add("CheckHelpContent", $true) }

Invoke-Build @arguments
