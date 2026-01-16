#Requires -Version 7.4

<#
.SYNOPSIS
    A script that provides simple entry points for bootstrapping, building and testing.
.DESCRIPTION
    A script to make it easy to bootstrap, build and run tests.
    This build targets .NET 8.0, which is the runtime for PowerShell 7.4 LTS.
    PowerShell 7.4 LTS is supported until November 2026.
.EXAMPLE
    PS > .\build.ps1 -Bootstrap
    Check and install prerequisites for the build.
.EXAMPLE
    PS > .\build.ps1 -Configuration Release
    Build the project in Release configuration.
.EXAMPLE
    PS > .\build.ps1
    Build the main module with the default configuration (Debug) configuration.
.EXAMPLE
    PS > .\build.ps1 -Test
    Run xUnit tests with the default configuration.
.PARAMETER Clean
    Clean the local repo, but keep untracked files.
.PARAMETER Bootstrap
    Check and install the build prerequisites.
.PARAMETER Test
    Run tests.
.PARAMETER Configuration
    The configuration setting for the build. The default value is 'Debug'.
#>
[CmdletBinding(DefaultParameterSetName = 'default')]
param(
    [Parameter(ParameterSetName = 'cleanup')]
    [switch] $Clean,

    [Parameter(ParameterSetName = 'bootstrap')]
    [switch] $Bootstrap,

    [Parameter(ParameterSetName = 'test')]
    [switch] $Test,

    [Parameter(ParameterSetName = 'test')]
    [switch] $CheckHelpContent,

    [Parameter(ParameterSetName = 'default')]
    [Parameter(ParameterSetName = 'test')]
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug"
)

# Clean step
if ($Clean) {
    try {
        Push-Location $PSScriptRoot
        git clean -fdX
    } finally {
        Pop-Location
    }

    return
}

Import-Module "$PSScriptRoot/tools/helper.psm1"

if ($Bootstrap) {
    Write-Log "Validate and install missing prerequisites for building ..."

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

if ($CheckHelpContent) { $arguments.Add("CheckHelpContent", $true) }

Invoke-Build @arguments
