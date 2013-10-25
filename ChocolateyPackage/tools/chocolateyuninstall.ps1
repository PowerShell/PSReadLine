$packageName = "PSReadLine"

try {
  $dir64 = "C:\Windows\system32\WindowsPowerShell\v1.0\Modules\$packageName\"
  $dir32 = "C:\Windows\sysWOW64\WindowsPowerShell\v1.0\Modules\$packageName\"

  $command = ""

  if (Test-Path -PathType Container $dir64) {
      $command = $command + "Remove-Item -Recurse -Force `'$dir64`';"
  }

  if (Test-Path -PathType Container $dir32) {
      $command = $command + "Remove-Item -Recurse -Force `'$dir32`'"
  }

  Start-ChocolateyProcessAsAdmin $command

  Write-ChocolateySuccess $packageName
} catch {
  Write-ChocolateyFailure $packageName $($_.Exception.Message)
  throw
}
