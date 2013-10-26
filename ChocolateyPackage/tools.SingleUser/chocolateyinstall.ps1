$packageName = "PSReadLine"

try {
  $source = Resolve-Path ($PSScriptRoot + "\..\$packageName\")
  $dest = "$HOME\Documents\WindowsPowerShell\Modules\"

  if (!(Test-Path $dest)) {
      mkdir $dest
  }

  Copy-Item -Force -Recurse $source $dest

  Write-ChocolateySuccess $packageName
} catch {
  Write-ChocolateyFailure $packageName $($_.Exception.Message)
  throw
}
