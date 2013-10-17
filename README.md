PSReadLine
==========

This module replaces the command line editing experience in PowerShell.exe.
It provides:

* Syntax coloring
* Simple syntax error notification
* A better multi-line experience (both editing and history)
* Customizable key bindings
* Cmd and emacs modes (neither are fully implemented yet, but both are usable)
* Many configuration options
* Bash style completion (optional in Cmd mode, default in Emacs mode)
* Emacs yank/kill ring
* PowerShell token based "word" movement and kill
* Undo/redo

Many planned features are not yet implemented, but in it's current state, the module is very usable.

The "out of box" experience is meant to be very familiar to PowerShell.exe users - there should be no need to learn any new key strokes.

Installation
============

1. Extract the `PSReadline.zip` into your `C:\Users\[User]\Documents\WindowsPowerShell\modules\PSReadline` folder. (You may have to create these directories if they don't exist)

2. Edit your `C:\Users\[User]\Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1` file (or create if not exists) and include the commands you want to load:

```
Import-Module PSReadLine  
Set-PSReadlineOption -EditMode Emacs
```

Usage
=====

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
    [PSConsoleUtilities.PSConsoleReadline]::GetBufferState([ref]$line, [ref]$cursor)

    if ($line[$cursor] -eq $key.KeyChar) {
        # Just move the cursor
        [PSConsoleUtilities.PSConsoleReadline]::SetCursorPosition($cursor + 1)
    }
    else {
        # Insert matching quotes, move cursor to be in between the quotes
        [PSConsoleUtilities.PSConsoleReadline]::Insert("$($key.KeyChar)" * 2)
        [PSConsoleUtilities.PSConsoleReadline]::GetBufferState([ref]$line, [ref]$cursor)
        [PSConsoleUtilities.PSConsoleReadline]::SetCursorPosition($cursor - 1)
    }
}
```

In this example, when you type a single quote or double quote, there are two things that can happen.  If the character following the cursor is not the quote typed, then a matched pair of quotes is inserted and the cursor is placed inside the the matched quotes.  If the character following the cursor is the quote typed, the cursor is simply moved past the quote without inserting anything.  If you use Resharper or another smart editor, this experience will be familiar.

Note that with the handler written this way, it correctly handles Undo - both quotes will be undone with one undo.

See the public methods of [PSConsoleUtilities.PSConsoleReadLine] to see what other built-in functionality you can modify.

If you want to change the command line in some unimplmented way in your custom key binding, you can use the methods:

```powershell
    [PSConsoleUtilities.PSConsoleReadLine]::GetBufferState
    [PSConsoleUtilities.PSConsoleReadLine]::Insert
    [PSConsoleUtilities.PSConsoleReadLine]::Replace
    [PSConsoleUtilities.PSConsoleReadLine]::SetCursorPosition
```
