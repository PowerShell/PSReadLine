$packageName = "PSReadLine"

try {

  Start-ChocolateyProcessAsAdmin "$PSScriptRoot\Remove-Module.ps1 '$packageName'"

  Write-ChocolateySuccess $packageName
} catch {
  Write-ChocolateyFailure $packageName $($_.Exception.Message)
  throw
}
