
[CmdletBinding()]
param(
    [switch]
    $Clean,

    [switch]
    $Bootstrap,

    [switch]
    $Test,

    [ValidateSet("Debug", "Release")]
    [string]
    $Configuration = "Debug",

    [ValidateSet("net461", "netcoreapp2.1")]
    [string]
    $Framework
)

# Clean step
if($Clean) {
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
    Write-Log "Validate and install missing prerequisits for building ..."

    Install-Dotnet
    if (-not (Get-Module -Name platyPS -ListAvailable)) {
        Write-Log -Warning "Module 'platyPS' is missing. Installing 'platyPS' ..."
        Install-Module -Name platyPS -Scope CurrentUser -Force
    }
    if (-not (Get-Module -Name InvokeBuild -ListAvailable)) {
        Write-Log -Warning "Module 'InvokeBuild' is missing. Installing 'InvokeBuild' ..."
        Install-Module -Name InvokeBuild -Scope CurrentUser -Force
    }

    return
}

# Common step required by both build and test
Find-Dotnet
if (-not (Get-Module -Name platyPS -ListAvailable)) {
    throw "Cannot find the 'platyPS' module. Please specify '-Bootstrap' to install build dependencies."
}
if (-not (Get-Module -Name InvokeBuild -ListAvailable)) {
    throw "Cannot find the 'InvokeBuild' module. Please specify '-Bootstrap' to install build dependencies."
}

# Build/Test step
$buildTask = if ($Test) { "RunTests" } else { "ZipRelease" }

$arguments = @{ Task = $buildTask; Configuration = $Configuration }
if ($Framework) { $arguments.Add("Framework", $Framework) }
Invoke-Build @arguments
