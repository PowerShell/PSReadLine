$packageName = "PSReadLine"

try {

  Remove-Item -Recurse -Force "$HOME\Documents\WindowsPowerShell\Modules\$packageName"

  Write-ChocolateySuccess $packageName
} catch {
  Write-ChocolateyFailure $packageName $($_.Exception.Message)
  throw
}
