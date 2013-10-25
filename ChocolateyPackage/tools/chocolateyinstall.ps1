$packageName = "PSReadLine"

try {
  $source = Resolve-Path ($PSScriptRoot + "\..\$packageName\")
  $dest64 = "C:\Windows\system32\WindowsPowerShell\v1.0\Modules\"
  $dest32 = "C:\Windows\sysWOW64\WindowsPowerShell\v1.0\Modules\"

  $command = ""

  if (Test-Path -PathType Container $dest64) {
      $command = $command + "Copy-Item -Force -Recurse `'$source`' `'$dest64`';"
  }

  if (Test-Path -PathType Container $dest32) {
      $command = $command + "Copy-Item -Force -Recurse `'$source`' `'$dest32`'"
  }

  Start-ChocolateyProcessAsAdmin $command

  Write-ChocolateySuccess $packageName
} catch {
  Write-ChocolateyFailure $packageName $($_.Exception.Message)
  throw
}
