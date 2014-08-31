param (
     [Parameter(Mandatory=$TRUE)]
     [String] $FilePath,
     [Parameter(Mandatory=$TRUE)]
     [String] $Version
)

if ((Test-Path -Path $FilePath -PathType Leaf) -ne $TRUE) {
    Write-Error -Message ($FilePath + ' not found.') -Category InvalidArgument;
    exit 1;
}

#normalize path
$FilePath = (Resolve-Path -Path $FilePath).Path;

$moduleVersionPattern = "ModuleVersion = '.*'";
$newVersion = "ModuleVersion = '" + $Version + "'";

(Get-Content -Path $FilePath) | For-Each {$_ -replace $moduleVersionPattern, $newVersion} | Set-Content -Path $FilePath;
