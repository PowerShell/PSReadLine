param(
    [Parameter(Mandatory=$TRUE)]
    [String] $ModuleName
)

    $moduleRoot = "$($env:CommonProgramW6432)\Modules\"
    $dir = "$moduleRoot\$ModuleName\"

    if (Test-Path $dir) {
        Write-Host -NoNewLine "Removing $ModuleName module from $moduleRoot... "

        Remove-Item -Recurse -Force $dir

        Write-Host -ForegroundColor Green "done"
    }

    if (Test-Path $moduleRoot) {
        if (!(Get-ChildItem -Recurse -Force $moduleRoot)) {
            Write-Host "Module directory empty."

            Write-Host -NoNewLine "Removing module directory from `$env:PSModulePath... "

            $path = [Environment]::GetEnvironmentVariable("PSModulePath")

            #Remove match paths but be careful about handling paths that don't
            #exist. We don't want to accidentally remove a path that we didn't
            #add.
            $cleanedPath = $path.Split(';') | 
                            Where-Object { 
                                    (-Not (Test-Path $_ )) -or
                                    ((Resolve-Path $_).Path -ne (Resolve-Path $moduleRoot).Path) } |
                            Select-Object -Unique
            $cleanedPath = $cleanedPath -Join ';'

            [Environment]::SetEnvironmentVariable("PSModulePath", $cleanedPath, "Machine")

            Write-Host -ForegroundColor Green "done"


            Write-Host -NoNewLine "Removing empty module directory... "
            Remove-Item $moduleRoot
            Write-Host -ForegroundColor Green "done"

        }

        Write-Host "Removal complete."
    }
