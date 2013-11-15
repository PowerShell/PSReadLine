$packageName = "PSReadLine"

try {

  Start-ChocolateyProcessAsAdmin "$PSScriptRoot\Remove-Module.ps1 '$packageName'"

  Write-ChocolateySuccess $packageName
} catch {
  $message = @"
  Uhoh. Looks like $packageName doesn't want to uninstall! But don't worry there is probably a good explanation.

  It is highly likely that you have an instance of PowerShell running with $packageName loaded.

  You should probably try closing all instances of PowerShell and rerunning this uninstall from cmd.exe.
"@

  Write-Host "==============================================="
  Write-Host $message
  Write-Host "==============================================="
  throw
}
