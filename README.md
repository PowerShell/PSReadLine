[![Build status](https://ci.appveyor.com/api/projects/status/0xu8r817dl6qt0g4?svg=true)](https://ci.appveyor.com/project/lzybkr/psreadline)

# PSReadLine

This module replaces the command line editing experience in PowerShell.exe for versions 3 and up.
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

The "out of box" experience is meant to be very familiar to PowerShell.exe users - there should be no need to learn any new key strokes.

Keith Hill wrote a great introduction to PSReadline [here](http://rkeithhill.wordpress.com/2013/10/18/psreadline-a-better-line-editing-experience-for-the-powershell-console/)

Ed Wilson (Scripting Guy) wrote a series on PSReadline, starting [here](http://blogs.technet.com/b/heyscriptingguy/archive/2014/06/16/the-search-for-a-better-powershell-console-experience.aspx)

## Installation

There are multiple ways to install PSReadline.

### Install from PowerShellGallery (preferred)

You will need PowerShellGet.  It is included in Windows 10 and [WMF5](http://go.microsoft.com/fwlink/?LinkId=398175). If you are using PowerShell V3 or V4, you will need to install [PowerShellGet](https://www.microsoft.com/en-us/download/details.aspx?id=49186).

After installing PowerShellGet, you can simply run `Install-Module PSReadline`.

If you are on Windows 10, PSReadline is already installed. Windows 10 RTM and the November update have version 1.1, later builds have version 1.2 (which includes vi mode). See below for how to upgrade.

### Install from GitHub (deprecated)

With the preview release of PowerShellGet for PowerShell V3/V4, I am deprecating downloads from GitHub.  I don't intend to update releases on GitHub, and may remove the release entirely from GitHub at some point.

To install the build from GitHub, you can install using [PsGet](http://psget.net) (very easy to install), and run `Install-Module PSReadline`.  Note that PsGet and PowerShellGet both have a Install-Module command, but they are very different despite having similar names and commands. PowerShellGet is implemented and supported by the PowerShell team at Microsoft, PsGet is a 3rd party tool that was a source of inspiration for PowerShellGet.

If you'd rather not use PsGet, you can just download the file [PSReadline.zip](https://github.com/lzybkr/PSReadLine/releases/download/Latest/PSReadline.zip) and extract the contents into your `C:\Users\[User]\Documents\WindowsPowerShell\modules\PSReadline` folder. (You may have to create these directories if they don't exist.)

### Post Installation

Edit your profile to import the module. This step is optional with PowerShell V5 and greater. There are two common profile files commonly used and the instructions are slightly different for each.

The file `C:\Users\[User]\Documents\WindowsPowerShell\profile.ps1` is used for all hosts (e.g. the ISE and powershell.exe).  If you already have this file, then you should add the following:

```
if ($host.Name -eq 'ConsoleHost')
{
    Import-Module PSReadline
}
```

Alternatively, the file `C:\Users\[User]\Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1` is for powershell.exe only.  Using this file, you can simply add:

```
Import-Module PSReadLine  
```

In either case, you can create the appropriate file if you don't already have one.

## Upgrading

When running one of the suggested commands below, be sure to exit all instances of powershell.exe, then run the suggested command from cmd.exe, powershell_ise.exe, or via the Win+R shortcut to make sure PSReadline isn't loaded.

If you are using the version of PSReadline that ships with Windows 10, you need to run: `powershell -noprofile -command "Install-Module PSReadline -Force -SkipPublisherCheck"`.

If you've installed PSReadline yourself from the PowerShell Gallery or with `PSGet` (this is less common on Windows 10), you can simply run: `powershell -noprofile -command "Update-Module PSReadline"`.

If you get an error like:

```
Remove-Item : Cannot remove item
C:\Users\{yourName}\Documents\WindowsPowerShell\Modules\PSReadLine\PSReadline.dll: Access to the path
'C:\Users\{yourName}\Documents\WindowsPowerShell\Modules\PSReadLine\PSReadline.dll' is denied.
At C:\Users\{yourName}\Documents\WindowsPowerShell\Modules\PsGet\PsGet.psm1:1009 char:52
```

Then you didn't kill all the processes that loaded PSReadline.

## Usage

To start using, just import the module:

```powershell
Import-Module PSReadLine
```

To use Emacs key bindings, you can use:

```powershell
Set-PSReadlineOption -EditMode Emacs
```

To view the current key bindings:
```powershell
Get-PSReadlineKeyHandler
```

There are many configuration options, see the options to Set-PSReadlineOption.  PSReadline has help for it's cmdlets as well as an about_PSReadline topic - see those topics for more detailed help.

To set your own custom keybindings, use the cmdlet Set-PSReadlineKeyHandler.  For example, for a better history experience, try:

```powershell
Set-PSReadlineKeyHandler -Key UpArrow -Function HistorySearchBackward
Set-PSReadlineKeyHandler -Key DownArrow -Function HistorySearchForward
```

With these bindings, up arrow/down arrow will work like PowerShell/cmd if the current command line is blank.  If you've entered some text though, it will search the history for commands that start with the currently entered text.

To enable bash style completion without using Emacs mode, you can use:

```powershell
Set-PSReadlineKeyHandler -Key Tab -Function Complete
```

Here is a more interesting example of what is possible:

```powershell
Set-PSReadlineKeyHandler -Chord 'Oem7','Shift+Oem7' `
                         -BriefDescription SmartInsertQuote `
                         -LongDescription "Insert paired quotes if not already on a quote" `
                         -ScriptBlock {
    param($key, $arg)

    $line = $null
    $cursor = $null
    [Microsoft.PowerShell.PSConsoleReadline]::GetBufferState([ref]$line, [ref]$cursor)

    if ($line[$cursor] -eq $key.KeyChar) {
        # Just move the cursor
        [Microsoft.PowerShell.PSConsoleReadline]::SetCursorPosition($cursor + 1)
    }
    else {
        # Insert matching quotes, move cursor to be in between the quotes
        [Microsoft.PowerShell.PSConsoleReadline]::Insert("$($key.KeyChar)" * 2)
        [Microsoft.PowerShell.PSConsoleReadline]::GetBufferState([ref]$line, [ref]$cursor)
        [Microsoft.PowerShell.PSConsoleReadline]::SetCursorPosition($cursor - 1)
    }
}
```

In this example, when you type a single quote or double quote, there are two things that can happen.  If the character following the cursor is not the quote typed, then a matched pair of quotes is inserted and the cursor is placed inside the the matched quotes.  If the character following the cursor is the quote typed, the cursor is simply moved past the quote without inserting anything.  If you use Resharper or another smart editor, this experience will be familiar.

Note that with the handler written this way, it correctly handles Undo - both quotes will be undone with one undo.

The [sample profile file](https://github.com/lzybkr/PSReadLine/blob/master/PSReadLine/SamplePSReadlineProfile.ps1) has a bunch of great examples to check out.  This file is included when PSReadline is installed.

See the public methods of [Microsoft.PowerShell.PSConsoleReadLine] to see what other built-in functionality you can modify.

If you want to change the command line in some unimplmented way in your custom key binding, you can use the methods:

```powershell
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState
    [Microsoft.PowerShell.PSConsoleReadLine]::Insert
    [Microsoft.PowerShell.PSConsoleReadLine]::Replace
    [Microsoft.PowerShell.PSConsoleReadLine]::SetCursorPosition
```

## Change Log

The change log is available [here](https://github.com/lzybkr/PSReadLine/blob/master/PSReadLine/Changes.txt).

## License

The license is available [here](https://github.com/lzybkr/PSReadLine/blob/master/PSReadLine/License.txt).
