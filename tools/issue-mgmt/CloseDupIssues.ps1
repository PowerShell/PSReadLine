## Close issues that are duplicate to #1306 and #1315

class issue
{
    [string] $number
    [string] $title
    [string] $author
    [string] $body
}

$repo_name = "PowerShell/PSReadLine"
$root_url = "https://github.com/PowerShell/PSReadLine/issues"
$msg_upgrade = @"
Please upgrade to the [2.1.0 version of PSReadLine](https://www.powershellgallery.com/packages/PSReadLine/2.1.0) from PowerShell Gallery.
See the [upgrading section](https://github.com/PowerShell/PSReadLine#upgrading) for instructions. Please let us know if you run into the same issue with the latest version.
"@

$issue_list = gh issue list -l 'Needs-Triage :mag:' -R $repo_name
$issue_numbers = $issue_list | Where-Object { $_ -notlike '*Issue-Enhancement,*' } | ForEach-Object { $_.Split('OPEN', [System.StringSplitOptions]::TrimEntries)[0] }

$issues = @()
foreach ($number in $issue_numbers)
{
    $issue_detail = gh issue view $number -R $repo_name

    $new_issue = [issue]::new()
    $new_issue.number = $number
    $new_issue.title = $issue_detail[0].Split('title:', [System.StringSplitOptions]::TrimEntries)[1]
    $new_issue.author = $issue_detail[2].Split('author:', [System.StringSplitOptions]::TrimEntries)[1]
    $new_issue.body = $issue_detail[9..$issue_detail.Length] -join "`n"

    $issues += $new_issue
}

foreach ($item in $issues)
{
    $comment = $null
    $number = $item.number
    $title = $item.title
    $body = $item.body
    $author = $item.author

    Write-Host "Issue: $root_url/$number" -ForegroundColor Green

    if ($title.Contains('https://github.com/PowerShell/PSReadLine/issues/new') -or
        $body.Contains('https://github.com/PowerShell/PSReadLine/issues/new') -or
        $body -match 'PSReadLine: 2\.\d\.\d(-\w+)?' -or
        $body -match 'PSReadline version: 2\.\d\.\d(-\w+)?' -or
        $body -match 'PowerShell: 7\.\d\.\d' -or
        $body -match 'PS version: 7\.\d\.\d')
    {
        ## The issue reported a recent version of PSReadLine, so leave it as is.
        continue
    }

    if ($body.Contains('System.ArgumentOutOfRangeException:') -and
        $body.Contains('System.Console.SetCursorPosition(') -and
        $body.Contains('Microsoft.PowerShell.PSConsoleReadLine.ReallyRender('))
    {
        ## The issue either reported an old version of PSReadLine, or provided no
        ## information about the version. In either case, it's likely a duplicate
        ## of #1306 and can be closed.
        $comment = 'This issue was already fixed (see #1306). {0}' -f $msg_upgrade
    }
    elseif ($body.Contains('Ctrl+l') -and
            $body.Contains('System.IO.IOException:') -and
            $body.Contains('Microsoft.PowerShell.PSConsoleReadLine.ProcessOneKey(') -and
            $body.Contains('System.IO.__Error.WinIOError('))
    {
        ## The issue either reported an old version of PSReadLine, or provided no
        ## information about the version. In either case, it's likely a duplicate
        ## of #1315 and can be closed.
        $comment = 'This issue was already fixed (see #1315). {0}' -f $msg_upgrade
    }
    elseif ($title.Contains('https://github.com/lzybkr/PSReadLine/issues/new') -or
            $body.Contains('https://github.com/lzybkr/PSReadLine/issues/new') -or
            $body -match 'PSReadLine: 2.0.0-\w+\d' -or
            $body -match 'PSReadline version: 2.0.0-\w+\d')
    {
        ## The issue reported an old version of PSReadLine. Even though the issue
        ## may not be a duplicate of either #1306 or #1315, we will close it and
        ## ask the author to use the latest stable version.
        $comment = "@$author, you were using a pretty old version of PSReadLine (2.0.0-beta2 or prior), and it's likely that the issue was fixed in a newer version.`n{0}" -f $msg_upgrade
    }

    if ($comment)
    {
        ## Comment the issue and then close it.
        $null = gh api "/repos/$repo_name/issues/$number/comments" --raw-field body=$comment &&
                gh issue close $number -R $repo_name
    }
}
