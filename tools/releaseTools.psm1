#requires -Version 6.0
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

class CommitNode {
    [string] $Hash
    [string[]] $Parents
    [string] $AuthorName
    [string] $AuthorGitHubLogin
    [string] $AuthorEmail
    [string] $Subject
    [string] $Body
    [string] $PullRequest
    [string] $ChangeLogMessage
    [string] $ThankYouMessage

    CommitNode($hash, $parents, $name, $email, $subject, $body) {
        $this.Hash = $hash
        $this.Parents = $parents
        $this.AuthorName = $name
        $this.AuthorEmail = $email
        $this.Subject = $subject
        $this.Body = $body

        if ($subject -match "\(#(\d+)\)$") {
            $this.PullRequest = $Matches[1]
        }
    }
}

# These powershell team members don't use 'microsoft.com' for Github email or choose to not show their emails.
# We have their names in this array so that we don't need to query GitHub to find out if they are powershell team members.
$Script:powershell_team = @(
    "Robert Holt"
    "Travis Plunk"
    "Joey Aiello"
)

# They are very active contributors, so we keep their email-login mappings here to save a few queries to Github.
$Script:community_login_map = @{
    "springcomp@users.noreply.github.com" = "springcomp"
}

# Ignore dependency bumping bot (Dependabot):
$Script:attribution_ignore_list = @(
    'dependabot[bot]@users.noreply.github.com'
)

##############################
#.SYNOPSIS
#In the release workflow, the release branch will be merged back to master after the release is done,
#and a merge commit will be created as the child of the release tag commit.
#This cmdlet takes a release tag or the corresponding commit hash, find its child merge commit, and
#return its metadata in this format: <merge-commit-hash>|<parent-commit-hashes>
#
#.PARAMETER LastReleaseTag
#The last release tag
#
#.PARAMETER CommitHash
#The commit hash of the last release tag
#
#.OUTPUTS
#Return the metadata of the child merge commit, in this format: <merge-commit-hash>|<parent-commit-hashes>
##############################
function Get-ChildMergeCommit
{
    [CmdletBinding(DefaultParameterSetName="TagName")]
    param(
        [Parameter(Mandatory, ParameterSetName="TagName")]
        [string]$LastReleaseTag,

        [Parameter(Mandatory, ParameterSetName="CommitHash")]
        [string]$CommitHash
    )

    $tag_hash = $CommitHash
    if ($PSCmdlet.ParameterSetName -eq "TagName") { $tag_hash = git rev-parse "$LastReleaseTag^0" }

    ## Get the merge commits that are reachable from 'HEAD' but not from the release tag
    $merge_commits_not_in_release_branch = git --no-pager log --merges "$tag_hash..HEAD" --format='%H||%P'
    ## Find the child merge commit, whose parent-commit-hashes contains the release tag hash
    $child_merge_commit = $merge_commits_not_in_release_branch | Select-String -SimpleMatch $tag_hash
    return $child_merge_commit.Line
}

##############################
#.SYNOPSIS
#Create a CommitNode instance to represent a commit.
#
#.PARAMETER CommitMetadata
#The commit metadata. It's in this format:
#<commit-hash>|<parent-hashes>|<author-name>|<author-email>|<commit-subject>
#
#.PARAMETER CommitMetadata
#The commit metadata, in this format:
#<commit-hash>|<parent-hashes>|<author-name>|<author-email>|<commit-subject>
#
#.OUTPUTS
#Return the 'CommitNode' object
##############################
function New-CommitNode
{
    param(
        [Parameter(ValueFromPipeline)]
        [ValidatePattern("^.+\|.+\|.+\|.+\|.+$")]
        [string]$CommitMetadata
    )

    Process {
        $hash, $parents, $name, $email, $subject = $CommitMetadata.Split("||")
        $body = (git --no-pager show $hash -s --format=%b) -join "`n"
        return [CommitNode]::new($hash, $parents, $name, $email, $subject, $body)
    }
}

##############################
#.SYNOPSIS
#Generate the draft change log.
#
#.PARAMETER LastReleaseTag
#The last release tag
#
#.PARAMETER HasCherryPick
#Indicate whether there are any commits in the last release branch that were cherry-picked from the master branch
#
#.PARAMETER Token
#The authentication token to use for retrieving the GitHub user log-in names for external contributors
#
#.OUTPUTS
#The generated change log draft.
##############################
function Get-ChangeLog
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$LastReleaseTag,

        [Parameter(Mandatory = $true)]
        [string]$ThisReleaseTag,

        [Parameter(Mandatory)]
        [string]$Token,

        [Parameter()]
        [switch]$HasCherryPick
    )

    $tag_hash = git rev-parse "$LastReleaseTag^0"
    $format = '%H||%P||%aN||%aE||%s'
    $header = @{"Authorization"="token $Token"}

    # Find the merge commit that merged the release branch to master.
    $child_merge_commit = Get-ChildMergeCommit -CommitHash $tag_hash
    if($child_merge_commit)
    {
        $commit_hash, $parent_hashes = $child_merge_commit.Split("||")
    }
    # Find the other parent of the merge commit, which represents the original head of master right before merging.
    $other_parent_hash = ($parent_hashes -replace $tag_hash).Trim()

    if ($HasCherryPick) {
        ## Sometimes we need to cherry-pick some commits from the master branch to the release branch during the release,
        ## and eventually merge the release branch back to the master branch. This will result in different commit nodes
        ## in master branch that actually represent same set of changes.
        ##
        ## In this case, we cannot simply use the revision range "$tag_hash..HEAD" because it will include the original
        ## commits in the master branch that were cherry-picked to the release branch -- they are reachable from 'HEAD'
        ## but not reachable from the last release tag. Instead, we need to exclude the commits that were cherry-picked,
        ## and only include the commits that are not in the last release into the change log.

        # Find the commits that were only in the original master, excluding those that were cherry-picked to release branch.
        $new_commits_from_other_parent = git --no-pager log --first-parent --cherry-pick --right-only "$tag_hash...$other_parent_hash" --format=$format | New-CommitNode
        # Find the commits that were only in the release branch, excluding those that were cherry-picked from master branch.
        $new_commits_from_last_release = git --no-pager log --first-parent --cherry-pick --left-only "$tag_hash...$other_parent_hash" --format=$format | New-CommitNode
        # Find the commits that are actually duplicate but having different patch-ids due to resolving conflicts during the cherry-pick.
        $duplicate_commits = $null
        if($new_commits_from_last_release -and $new_commits_from_other_parent)
        {
            $duplicate_commits = Compare-Object $new_commits_from_last_release $new_commits_from_other_parent -Property PullRequest -ExcludeDifferent -IncludeEqual -PassThru
        }
        if ($duplicate_commits) {
            $duplicate_pr_numbers = @($duplicate_commits | ForEach-Object -MemberName PullRequest)
            $new_commits_from_other_parent = $new_commits_from_other_parent | Where-Object PullRequest -NotIn $duplicate_pr_numbers
        }

        # Find the commits that were made after the merge commit.
        $new_commits_after_merge_commit = @(git --no-pager log --first-parent "$commit_hash..HEAD" --format=$format | New-CommitNode)
        $new_commits = $new_commits_after_merge_commit + $new_commits_from_other_parent
    } else {
        ## No cherry-pick was involved in the last release branch.
        ## Using a ref rang like "$tag_hash..HEAD" with 'git log' means getting the commits that are reachable from 'HEAD' but not reachable from the last release tag.

        ## We use '--first-parent' for 'git log'. It means for any merge node, only follow the parent node on the master branch side.
        ## In case we merge a branch to master for a PR, only the merge node will show up in this way, the individual commits from that branch will be ignored.
        ## This is what we want because the merge commit itself already represents the PR.

        ## First, we want to get all new commits merged during the last release
        $new_commits_during_last_release = @(git --no-pager log --first-parent "$tag_hash..$other_parent_hash" --format=$format | New-CommitNode)
        ## Then, we want to get all new commits merged after the last release
        $new_commits_after_last_release  = @(git --no-pager log --first-parent "$commit_hash..HEAD" --format=$format | New-CommitNode)
        ## Last, we get the full list of new commits
        $new_commits = $new_commits_during_last_release + $new_commits_after_last_release
    }

    foreach ($commit in $new_commits) {
        Write-Verbose "authorname: $($commit.AuthorName)"
        if ($commit.AuthorEmail.EndsWith("@microsoft.com") -or $powershell_team -contains $commit.AuthorName -or $Script:attribution_ignore_list -contains $commit.AuthorEmail) {
            $commit.ChangeLogMessage = "- {0}" -f (Get-ChangeLogMessage $commit.Subject)
        } else {
            if ($community_login_map.ContainsKey($commit.AuthorEmail)) {
                $commit.AuthorGitHubLogin = $community_login_map[$commit.AuthorEmail]
            } else {
                $uri = "https://api.github.com/repos/PowerShell/PSReadLine/commits/$($commit.Hash)"
                try{
                    $response = Invoke-WebRequest -Uri $uri -Method Get -Headers $header -ErrorAction Ignore
                } catch{}
                if($response)
                {
                    $content = ConvertFrom-Json -InputObject $response.Content
                    $commit.AuthorGitHubLogin = $content.author.login
                    $community_login_map[$commit.AuthorEmail] = $commit.AuthorGitHubLogin
                }
            }
            $commit.ChangeLogMessage = ("- {0} (Thanks @{1}!)" -f (Get-ChangeLogMessage $commit.Subject), $commit.AuthorGitHubLogin)
            $commit.ThankYouMessage = ("@{0}" -f ($commit.AuthorGitHubLogin))
        }
    }

    # Write output

    $version = $ThisReleaseTag.TrimStart('v')
    Write-Output "### [${version}] - $(Get-Date -Format yyyy-MM-dd)`n"

    foreach ($commit in $new_commits) {
        Write-Output $commit.ChangeLogMessage
    }

    Write-Output "`n[${version}]: https://github.com/PowerShell/PSReadLine/compare/${LastReleaseTag}...${ThisReleaseTag}`n"
}

function Get-ChangeLogMessage
{
    param($OriginalMessage)

    switch -regEx ($OriginalMessage)
    {
        '^Merged PR (\d*): ' {
            return $OriginalMessage.replace($Matches.0,'') + " (Internal $($Matches.1))"
        }
        '^Build\(deps\): ' {
            return $OriginalMessage.replace($Matches.0,'')
        }
        default {
            return $OriginalMessage
        }
    }
}

Export-ModuleMember -Function Get-ChangeLog
