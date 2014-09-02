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

$nuspecConfig = [xml] (Get-Content -Path $FilePath);
$nuspecConfig.DocumentElement.metadata.version = $Version;

if (!$?) {
    Write-Error -Message "Unable to perform update.";
    exit 1;
}

$nuspecConfig.Save($FilePath);
