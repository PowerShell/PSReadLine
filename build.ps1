#Requires -Version 7.4

<#
.SYNOPSIS
    A script that provides simple entry points for bootstrapping, building and testing.
.DESCRIPTION
    A script to make it easy to bootstrap, build and run tests.
.EXAMPLE
    PS > .\build.ps1 -Bootstrap
    Check and install prerequisites for the build.
.EXAMPLE
    PS > .\build.ps1 -Configuration Release
    Build the main module with 'Release' configuration targeting 'netstandard2.0'.
.EXAMPLE
    PS > .\build.ps1
    Build the main module with the default configuration (Debug) targeting 'netstandard2.0'.
.EXAMPLE
    PS > .\build.ps1 -Test
    Run xUnit tests with the default configuration (Debug) and the default target framework (net472 on Windows or net6.0 otherwise).
.PARAMETER Clean
    Clean the local repo, but keep untracked files.
.PARAMETER Bootstrap
    Check and install the build prerequisites.
.PARAMETER Test
    Run tests.
.PARAMETER Configuration
    The configuration setting for the build. The default value is 'Debug'.
.PARAMETER Framework
    The target framework when testing:
      - net472: run tests with .NET Framework
      - net6.0: run tests with .NET 6.0
    When not specified, the target framework is determined by the current OS platform:
      - use 'net472' on Windows
      - use 'net6.0' on Unix platforms
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

    [Parameter(ParameterSetName = 'test')]
    [ValidateSet("net472", "net6.0")]
    [string] $Framework,

    [Parameter(ParameterSetName = 'default')]
    [Parameter(ParameterSetName = 'test')]
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [Parameter(ParameterSetName = 'bootstrap')]
    [ValidateSet("Public","Internal")]
    [string] $FeedSource = "Public"
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

if ($Bootstrap -and $FeedSource -eq "Public") {
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

if ($Framework) { $arguments.Add("TestFramework", $Framework) }
if ($CheckHelpContent) { $arguments.Add("CheckHelpContent", $true) }

Invoke-Build @arguments
