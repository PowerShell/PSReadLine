param (
     [Parameter(Mandatory=$TRUE)]
     [String] $FilePath,
     [Parameter(Mandatory=$TRUE)]
     [String] $Version
)

if ((Test-Path $FilePath -PathType Leaf) -ne $TRUE) {
    Write-Error -Message ($FilePath + ' not found.') -Category InvalidArgument;
    exit 1;
}

#normalize path
$FilePath = (Resolve-Path $FilePath).Path;

$moduleVersionPattern = "ModuleVersion = '.*'";
$newVersion = "ModuleVersion = '" + $Version + "'";

(Get-Content $FilePath) | ForEach-Object {$_ -replace $moduleVersionPattern, $newVersion} | Set-Content $FilePath;