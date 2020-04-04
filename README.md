[![appveyor-build-status][]][appveyor-build-site]

[appveyor-build-status]: https://ci.appveyor.com/api/projects/status/9mygtkr9fkov47xv/branch/master?svg=true
[appveyor-build-site]: https://ci.appveyor.com/project/PowerShell/PSReadLine?branch=master

<!--
[![azure-build-status][]][azure-build-site]
[azure-build-status]: https://lzybkr.visualstudio.com/AzurePipelines/_apis/build/status/PSReadLine%20Azure%20Pipeline
[azure-build-site]: https://lzybkr.visualstudio.com/AzurePipelines/_build/latest?definitionId=6
-->

# PSReadLine

This module replaces the command line editing experience of PowerShell for versions 3 and up.
It provides:

* Syntax coloring
* Simple syntax error notification
* A good multi-line experience (both editing and history)
* Customizable key bindings
* Cmd and emacs modes (neither are fully implemented yet, but both are usable)
* Many configuration options
* Bash style completion (optional in Cmd mode, default in Emacs mode)
* Bash/zsh style interactive history search (CTRL-R)
* Emacs yank/kill ring
* PowerShell token based "word" movement and kill
* Undo/redo
* Automatic saving of history, including sharing history across live sessions
* "Menu" completion (somewhat like Intellisense, select completion with arrows) via Ctrl+Space

The "out of box" experience is meant to be very familiar to PowerShell users - there should be no need to learn any new key strokes.

Keith Hill wrote a great introduction to `PSReadLine` [here](http://rkeithhill.wordpress.com/2013/10/18/psreadline-a-better-line-editing-experience-for-the-powershell-console/).

Ed Wilson (Scripting Guy) wrote a series on `PSReadLine`, starting [here](http://blogs.technet.com/b/heyscriptingguy/archive/2014/06/16/the-search-for-a-better-powershell-console-experience.aspx).

## Installation

There are multiple ways to install `PSReadLine`.

### Install from PowerShellGallery (preferred)

You will need the `1.6.0` or a higher version of [`PowerShellGet`](https://docs.microsoft.com/powershell/scripting/gallery/installing-psget) to install the latest prerelease version of `PSReadLine`.

Windows PowerShell 5.1 ships an older version of `PowerShellGet` which doesn't support installing prerelease modules,
so Windows PowerShell users need to install the latest `PowerShellGet` (if not yet) by running the following commands from an elevated Windows PowerShell session:

```powershell
Install-Module -Name PowerShellGet -Force
Exit
```

After installing `PowerShellGet`, you can simply run `Install-Module PSReadLine -AllowPrerelease -Force` to get the latest prerelease version of `PSReadLine`.
If you only want to get the latest stable version, run `Install-Module PSReadLine`.

>[!NOTE] Prerelease versions will have newer features and bug fixes, but may also introduce new issues.

If you are using Windows PowerShell on Windows 10 or using PowerShell 6+, `PSReadLine` is already installed.
Windows PowerShell on the latest Windows 10 has version `2.0.0-beta2` of `PSReadLine`.
PowerShell 6+ versions have the newer prerelease versions of `PSReadLine`.

### Install from GitHub (deprecated)

With the preview release of PowerShellGet for PowerShell V3/V4, downloads from GitHub are deprecated.
We don't intend to update releases on GitHub, and may remove the release entirely from GitHub at some point.

### Post Installation

If you are using Windows PowerShell V5 or V5.1 versions, or using PowerShell 6+ versions, you are good to go and can skip this section.

Otherwise, you need to edit your profile to import the module.
There are two profile files commonly used and the instructions are slightly different for each.
The file `C:\Users\[User]\Documents\WindowsPowerShell\profile.ps1` is used for all hosts (e.g. the `ISE` and `powershell.exe`).
If you already have this file, then you should add the following:

```powershell
if ($host.Name -eq 'ConsoleHost')
{
    Import-Module PSReadLine
}
```

Alternatively, the file `C:\Users\[User]\Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1` is for `powershell.exe` only.  Using this file, you can simply add:

```powershell
Import-Module PSReadLine
```

In either case, you can create the appropriate file if you don't already have one.

## Upgrading

When running one of the suggested commands below, be sure to exit all instances of `powershell.exe`, `pwsh.exe` or `pwsh`,
including those opened in `VSCode` terminals.

Then, to make sure `PSReadLine` isn't loaded:
- _if you are on Windows_, run the suggested command below from `cmd.exe`, `powershell_ise.exe`, or via the `Win+R` shortcut;
- _if you are on Linux/macOS_, run the suggested command below from the default terminal (like `bash` or `zsh`).


If you are using the version of `PSReadLine` that ships with Windows PowerShell,
you need to run: `powershell -noprofile -command "Install-Module PSReadLine -Force -SkipPublisherCheck -AllowPrerelease"`.
Note: you will need to make sure [PowershellGet is updated](https://github.com/PowerShell/PSReadLine#install-from-powershellgallery-preferred) before running this command.

If you are using the version of `PSReadLine` that ships with PowerShell 6+ versions,
you need to run: `<path-to-pwsh-executable> -noprofile -command "Install-Module PSReadLine -Force -SkipPublisherCheck -AllowPrerelease"`.

If you've installed `PSReadLine` yourself from the PowerShell Gallery,
you can simply run: `powershell -noprofile -command "Update-Module PSReadLine -AllowPrerelease"` or
`<path-to-pwsh-executable> -noprofile -command "Update-Module PSReadLine -AllowPrerelease"`,
depending on the version of PowerShell you are using.

If you get an error like:

```none
Remove-Item : Cannot remove item
C:\Users\{yourName}\Documents\WindowsPowerShell\Modules\PSReadLine\Microsoft.PowerShell.PSReadLine.dll: Access to the path
'C:\Users\{yourName}\Documents\WindowsPowerShell\Modules\PSReadLine\Microsoft.PowerShell.PSReadLine.dll' is denied.
```

or a warning like:

```none
WARNING: The version '2.0.0' of module 'PSReadLine' is currently in use. Retry the operation after closing the applications.
```

Then you didn't kill all the processes that loaded `PSReadLine`.

## Usage

To start using, just import the module:

```powershell
Import-Module PSReadLine
```

To use Emacs key bindings, you can use:

```powershell
Set-PSReadLineOption -EditMode Emacs
```

To view the current key bindings:

```powershell
Get-PSReadLineKeyHandler
```

There are many configuration options, see the options to `Set-PSReadLineOption`.  `PSReadLine` has help for it's cmdlets as well as an `about_PSReadLine` topic - see those topics for more detailed help.

To set your own custom keybindings, use the cmdlet `Set-PSReadLineKeyHandler`. For example, for a better history experience, try:

```powershell
Set-PSReadLineKeyHandler -Key UpArrow -Function HistorySearchBackward
Set-PSReadLineKeyHandler -Key DownArrow -Function HistorySearchForward
```

With these bindings, up arrow/down arrow will work like PowerShell/cmd if the current command line is blank.  If you've entered some text though, it will search the history for commands that start with the currently entered text.

To enable bash style completion without using Emacs mode, you can use:

```powershell
Set-PSReadLineKeyHandler -Key Tab -Function Complete
```

Here is a more interesting example of what is possible:

```powershell
Set-PSReadLineKeyHandler -Chord 'Oem7','Shift+Oem7' `
                         -BriefDescription SmartInsertQuote `
                         -LongDescription "Insert paired quotes if not already on a quote" `
                         -ScriptBlock {
    param($key, $arg)

    $line = $null
    $cursor = $null
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)

    if ($line[$cursor] -eq $key.KeyChar) {
        # Just move the cursor
        [Microsoft.PowerShell.PSConsoleReadLine]::SetCursorPosition($cursor + 1)
    }
    else {
        # Insert matching quotes, move cursor to be in between the quotes
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert("$($key.KeyChar)" * 2)
        [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)
        [Microsoft.PowerShell.PSConsoleReadLine]::SetCursorPosition($cursor - 1)
    }
}
```

In this example, when you type a single quote or double quote, there are two things that can happen.  If the character following the cursor is not the quote typed, then a matched pair of quotes is inserted and the cursor is placed inside the the matched quotes.  If the character following the cursor is the quote typed, the cursor is simply moved past the quote without inserting anything.  If you use Resharper or another smart editor, this experience will be familiar.

Note that with the handler written this way, it correctly handles Undo - both quotes will be undone with one undo.

The [sample profile file](https://github.com/PowerShell/PSReadLine/blob/master/PSReadLine/SamplePSReadLineProfile.ps1) has a bunch of great examples to check out.  This file is included when `PSReadLine` is installed.

See the public methods of `[Microsoft.PowerShell.PSConsoleReadLine]` to see what other built-in functionality you can modify.

If you want to change the command line in some unimplmented way in your custom key binding, you can use the methods:

```powershell
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState
    [Microsoft.PowerShell.PSConsoleReadLine]::Insert
    [Microsoft.PowerShell.PSConsoleReadLine]::Replace
    [Microsoft.PowerShell.PSConsoleReadLine]::SetCursorPosition
```

## Developing and Contributing

Please see the [Contribution Guide][] for how to develop and contribute.

### Building

To build `PSReadLine` on Windows, Linux, or macOS,
you must have the following installed:

* .NET Core SDK 2.1.802 or [a newer version](https://www.microsoft.com/net/download)
* The PowerShell modules `InvokeBuild` and `platyPS`

The build script `build.ps1` can be used to bootstrap, build and test the project.

* Bootstrap: `./build.ps1 -Bootstrap`
* Build:
    * Targeting .NET 4.6.1 (Windows only): `./build.ps1 -Configuration Debug -Framework net461`
    * Targeting .NET Core: `./build.ps1 -Configuration Debug -Framework netcoreapp2.1`
* Test:
    * Targeting .NET 4.6.1 (Windows only): `./build.ps1 -Test -Configuration Debug -Framework net461`
    * Targeting .NET Core: `./build.ps1 -Test -Configuration Debug -Framework netcoreapp2.1`

After build, the produced artifacts can be found at `<your-local-repo-root>/bin/Debug`.

[Contribution Guide]: https://github.com/PowerShell/PSReadLine/blob/master/.github/CONTRIBUTING.md

## Change Log

The change log is available [here](https://github.com/PowerShell/PSReadLine/blob/master/PSReadLine/Changes.txt).

## Licensing

PSReadLine is licensed under the [2-Clause BSD License][].

[2-Clause BSD License]: https://github.com/PowerShell/PSReadLine/blob/master/License.txt

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][conduct-code].
For more information see the [Code of Conduct FAQ][conduct-FAQ] or contact [opencode@microsoft.com][conduct-email] with any additional questions or comments.

[conduct-code]: https://opensource.microsoft.com/codeofconduct/
[conduct-FAQ]: https://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com
