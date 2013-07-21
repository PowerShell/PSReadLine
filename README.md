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

Many planned features are not yet implemented, but in it's current state, the module is very usable.

The "out of box" experience is meant to be very familiar to PowerShell.exe users - there should be no need to learn any new key strokes.

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

There are many configuration options, see the options to Set-PSReadlineOption.  At some point I'll actually write some documentation.

To set your own custom keybindings, use the cmdlet Set-PSReadlineKeyHandler.  For example, for a better history experience, try:

```powershell
Set-PSReadlineKeyHandler -Key UpArrow -BriefDescription HistorySearchBackward -Handler { 
    [PSConsoleUtilities.PSConsoleReadLine]::HistorySearchBackward()
}
Set-PSReadlineKeyHandler -Key DownArrow -BriefDescription HistorySearchForward -Handler { 
    [PSConsoleUtilities.PSConsoleReadLine]::HistorySearchForward()
}
```

With these bindings, up arrow/down arrow will work like PowerShell/cmd if the current command line is blank.  If you've entered some text though, it will search the history for commands that start with the currently entered text.

To enable bash style completion without using Emacs mode, you can use:

```powershell
Set-PSReadlineKeyHandler -Key Tab -BriefDescription Complete -Handler { 
    [PSConsoleUtilities.PSConsoleReadLine]::Complete()
}
```

See the public methods of [PSConsoleUtilities.PSConsoleReadLine] to see what other built-in functionality you can modify.

If you want to change the command line in some unimplmented way in your custom key binding, you can use the methods:

```powershell
[PSConsoleUtilities.PSConsoleReadLine]::GetBufferState
[PSConsoleUtilities.PSConsoleReadLine]::SetBufferState
```
