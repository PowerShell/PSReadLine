param(
        [Parameter(Mandatory=$TRUE)]
        [String] $PathToModuleFiles,
        [Parameter(Mandatory=$TRUE)]
        [String] $ModuleName
     )

if (!(Test-Path -PathType Container $PathToModuleFiles)) {
    Write-Error "$PathToModuleFiles not found or is not a directory. Nothing to install."
    exit
}

$moduleRoot = "$($env:CommonProgramW6432)\Modules\"
$dest = "$moduleRoot\$ModuleName\"

if (!(Test-Path $moduleRoot)) {
    New-Item -ItemType directory $moduleRoot | out-null
}

Write-Host -NoNewLine "Copying $ModuleName module to $moduleRoot... "

Copy-Item -Force -Recurse $PathToModuleFiles $dest

Write-Host -ForegroundColor Green "done"

#Update PSModulePath if it needs it
$path = [Environment]::GetEnvironmentVariable("PSModulePath")

$pathContainsRoot = 0 -ne ($path.Split(';') | ? { Test-Path $_ } | ? { (Resolve-Path $_).Path -eq (Resolve-Path $moduleRoot).Path}).count

if(-Not $pathContainsRoot) {

    Write-Host -NoNewLine "Adding $moduleRoot to `$env:PSModulePath... "

    $path += ";$moduleRoot"
    [Environment]::SetEnvironmentVariable("PSModulePath", $path, "Machine")

    Write-Host -ForegroundColor Green "done"
}

Write-Host "Installation complete."
