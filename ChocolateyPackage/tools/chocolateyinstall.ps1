$packageName = "PSReadLine"

try {
  $source = Resolve-Path ($PSScriptRoot + "\..\$packageName\")

  Start-ChocolateyProcessAsAdmin "$PSScriptRoot\Install-Module.ps1 '$source' '$packageName'"


  Write-ChocolateySuccess $packageName
} catch {
  Write-ChocolateyFailure $packageName $($_.Exception.Message)
  throw
}
