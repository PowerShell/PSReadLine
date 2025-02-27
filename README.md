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

Some good resources about `PSReadLine`:

- Keith Hill wrote a [great introduction](https://rkeithhill.wordpress.com/2013/10/18/psreadline-a-better-line-editing-experience-for-the-powershell-console/) (2013) to `PSReadLine`.
- Ed Wilson (Scripting Guy) wrote a [series](https://devblogs.microsoft.com/scripting/tag/psreadline/) (2014-2015) on `PSReadLine`.
- John Savill has a [video](https://www.youtube.com/watch?v=Q11sSltuTE0) (2021) covering installation, configuration, and tailoring `PSReadLine` to your liking.

## Installation and Upgrading

You will need the `1.6.0` or a higher version of [`PowerShellGet`](https://learn.microsoft.com/en-us/powershell/gallery/powershellget/install-powershellget) to install or upgrade to the latest prerelease version of `PSReadLine`.

PowerShell 6+ already has a higher version of `PowerShellGet` built-in.
However, Windows PowerShell 5.1 ships an older version of `PowerShellGet` which doesn't support installing prerelease modules.
So, Windows PowerShell users need to install the latest `PowerShellGet` (if not yet) by running the following commands from an elevated Windows PowerShell session:

```powershell
Install-Module -Name PowerShellGet -Force; exit
```

After installing `PowerShellGet`, you install or upgrade to the latest prerelease version of `PSReadLine` by running

```powershell
Install-Module PSReadLine -Repository PSGallery -Scope CurrentUser -AllowPrerelease -Force
```

If you only want to get the latest stable version, run:

```powershell
Install-Module PSReadLine -Repository PSGallery -Scope CurrentUser -Force
```

>[!NOTE] Prerelease versions will have newer features and bug fixes, but may also introduce new issues.

## Usage

To use Emacs key bindings, you can use:

```powershell
Set-PSReadLineOption -EditMode Emacs
```

To view the current key bindings:

```powershell
Get-PSReadLineKeyHandler
```

There are many configuration options, see the options to `Set-PSReadLineOption`.
`PSReadLine` has help for its cmdlets as well as an `about_PSReadLine` topic - see those topics for more detailed help.

To set your own custom keybindings, use the cmdlet `Set-PSReadLineKeyHandler`.
For example, for a better history experience, try:

```powershell
Set-PSReadLineKeyHandler -Key UpArrow -Function HistorySearchBackward
Set-PSReadLineKeyHandler -Key DownArrow -Function HistorySearchForward
```

With these bindings, up arrow/down arrow will work like PowerShell/cmd if the current command line is blank.
If you've entered some text though, it will search the history for commands that start with the currently entered text.

To enable bash style completion without using Emacs mode, you can use:

```powershell
Set-PSReadLineKeyHandler -Key Tab -Function Complete
```

Here is a more interesting example of what is possible:

```powershell
Set-PSReadLineKeyHandler -Chord '"',"'" `
                         -BriefDescription SmartInsertQuote `
                         -LongDescription "Insert paired quotes if not already on a quote" `
                         -ScriptBlock {
    param($key, $arg)

    $line = $null
    $cursor = $null
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)

    if ($line.Length -gt $cursor -and $line[$cursor] -eq $key.KeyChar) {
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

In this example, when you type a single quote or double quote, there are two things that can happen.
If the character following the cursor is not the quote typed, then a matched pair of quotes is inserted and the cursor is placed inside the the matched quotes.
If the character following the cursor is the quote typed, the cursor is simply moved past the quote without inserting anything.
If you use `VSCode`, `Resharper`, or another smart editor, this experience will be familiar.

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

* .NET 6.0 or [a newer version](https://www.microsoft.com/net/download)
* The PowerShell modules `InvokeBuild` and `platyPS`

The build script `build.ps1` can be used to bootstrap, build and test the project.

* Bootstrap: `./build.ps1 -Bootstrap`
* Build: `./build.ps1 -Configuration Debug`
* Test:
    * Targeting .NET 4.7.2 (Windows only): `./build.ps1 -Test -Configuration Debug -Framework net472`
    * Targeting .NET 6.0: `./build.ps1 -Test -Configuration Debug -Framework net6.0`

After build, the produced artifacts can be found at `<your-local-repo-root>/bin/Debug`.

In order to isolate your imported module to the one locally built, be sure to run 
`pwsh -NonInteractive -NoProfile` to not automatically load the default PSReadLine module installed.
Then, load the locally built PSReadLine module by `Import-Module <your-local-repo-root>/bin/Debug/PSReadLine/PSReadLine.psd1`.

## Change Log

The change log is available [here](https://github.com/PowerShell/PSReadLine/blob/master/PSReadLine/Changes.txt).

## Licensing

PSReadLine is licensed under the [2-Clause BSD License][].

## Code of Conduct

Please see our [Code of Conduct](.github/CODE_OF_CONDUCT.md) before participating in this project.

## Security Policy

For any security issues, please see our [Security Policy](.github/SECURITY.md).

[Contribution Guide]: https://github.com/PowerShell/PSReadLine/blob/master/.github/CONTRIBUTING.md
[2-Clause BSD License]: https://github.com/PowerShell/PSReadLine/blob/master/License.txt
