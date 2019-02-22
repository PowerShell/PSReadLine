---
external help file: Microsoft.PowerShell.PSReadLine.dll-Help.xml
keywords: powershell,cmdlet
locale: en-us
Module Name: PSReadLine
ms.date: 12/07/2018
online version: http://go.microsoft.com/fwlink/?LinkID=821452
schema: 2.0.0
title: Set-PSReadLineKeyHandler
---

# Set-PSReadLineKeyHandler

## SYNOPSIS
Binds keys to user-defined or PSReadLine key handler functions.

## SYNTAX

### ScriptBlock

```
Set-PSReadLineKeyHandler [-ScriptBlock] <ScriptBlock> [-BriefDescription <String>] [-Description <String>]
 [-Chord] <String[]> [-ViMode <ViMode>] [<CommonParameters>]
```

### Function

```
Set-PSReadLineKeyHandler [-Chord] <String[]> [-ViMode <ViMode>] [-Function] <String> [<CommonParameters>]
```

## DESCRIPTION

The `Set-PSReadLineKeyHandler` cmdlet customizes the result when a key or sequence of keys is
pressed. With user-defined key bindings, you can do almost anything that is possible from within a
PowerShell script.

## EXAMPLES

### Example 1: Bind the arrow key to a function

This command binds the up arrow key to the function **HistorySearchBackward**. This function uses
the current contents of the command line as the search string used to search the command history.

```powershell
Set-PSReadLineKeyHandler -Chord UpArrow -Function HistorySearchBackward
```

### Example 2: Bind a key to a script block

This example shows how a single key can be used to run a command. The command binds the key
`Ctrl+Shift+B` to a script block that clears the line, inserts the word "build", and then accepts
the line.

```powershell
Set-PSReadLineKeyHandler -Chord Ctrl+Shift+B -ScriptBlock {
    [Microsoft.PowerShell.PSConsoleReadLine]::RevertLine()
    [Microsoft.PowerShell.PSConsoleReadLine]::Insert('build')
    [Microsoft.PowerShell.PSConsoleReadLine]::AcceptLine()
}
```

## PARAMETERS

### -BriefDescription

A brief description of the key binding. This description is displayed by the
`Get-PSReadLineKeyHandler` cmdlet.

```yaml
Type: String
Parameter Sets: ScriptBlock
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Chord

The key or sequence of keys to be bound to a function or script block. Use a single string to
specify a single binding. If the binding is a sequence of keys, separate the keys by a comma, as in
the following example:

`Ctrl+X,Ctrl+L`

This parameter accepts an array of strings. Each string is a separate binding, not a sequence of
keys for a single binding.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: Key

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Description

Specifies a more detailed description of the key binding that is visible in the output of the
`Get-PSReadLineKeyHandler` cmdlet.

```yaml
Type: String
Parameter Sets: ScriptBlock
Aliases: LongDescription

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Function

Specifies the name of an existing key handler provided by PSReadLine. This parameter lets you
rebind existing key bindings, or bind a handler that is currently unbound.

```yaml
Type: String
Parameter Sets: Function
Aliases:
Accepted values: Abort, AcceptAndGetNext, AcceptLine, AddLine, BackwardChar, BackwardDeleteChar, BackwardDeleteLine, BackwardDeleteWord, BackwardKillLine, BackwardKillWord, BackwardWord, BeginningOfHistory, BeginningOfLine, CancelLine, CaptureScreen, CharacterSearch, CharacterSearchBackward, ClearHistory, ClearScreen, Complete, Copy, CopyOrCancelLine, Cut, DeleteChar, DeleteCharOrExit, DeleteEndOfWord, DeleteLine, DeleteLineToFirstChar, DeleteToEnd, DeleteWord, DigitArgument, EndOfHistory, EndOfLine, ExchangePointAndMark, ForwardChar, ForwardDeleteLine, ForwardSearchHistory, ForwardWord, GotoBrace, GotoColumn, GotoFirstNonBlankOfLine, HistorySearchBackward, HistorySearchForward, InsertLineAbove, InsertLineBelow, InvertCase, InvokePrompt, KillLine, KillRegion, KillWord, MenuComplete, MoveToEndOfLine, NextHistory, NextLine, NextWord, NextWordEnd, Paste, PasteAfter, PasteBefore, PossibleCompletions, PrependAndAccept, PreviousHistory, PreviousLine, Redo, RepeatLastCharSearch, RepeatLastCharSearchBackwards, RepeatLastCommand, RepeatSearch, RepeatSearchBackward, ReverseSearchHistory, RevertLine, ScrollDisplayDown, ScrollDisplayDownLine, ScrollDisplayToCursor, ScrollDisplayTop, ScrollDisplayUp, ScrollDisplayUpLine, SearchChar, SearchCharBackward, SearchCharBackwardWithBackoff, SearchCharWithBackoff, SearchForward, SelectAll, SelectBackwardChar, SelectBackwardsLine, SelectBackwardWord, SelectForwardChar, SelectForwardWord, SelectLine, SelectNextWord, SelectShellBackwardWord, SelectShellForwardWord, SelectShellNextWord, SelfInsert, SetMark, ShellBackwardKillWord, ShellBackwardWord, ShellForwardWord, ShellKillWord, ShellNextWord, ShowKeyBindings, SwapCharacters, TabCompleteNext, TabCompletePrevious, Undo, UndoAll, UnixWordRubout, ValidateAndAcceptLine, ViAcceptLine, ViAcceptLineOrExit, ViAppendLine, ViBackwardDeleteGlob, ViBackwardGlob, ViBackwardWord, ViCommandMode, ViDeleteBrace, ViDeleteEndOfGlob, ViDeleteGlob, ViDigitArgumentInChord, ViEditVisually, ViExit, ViGotoBrace, ViInsertAtBegining, ViInsertAtEnd, ViInsertLine, ViInsertMode, ViInsertWithAppend, ViInsertWithDelete, ViJoinLines, ViNextWord, ViSearchHistoryBackward, ViTabCompleteNext, ViTabCompletePrevious, ViYankBeginningOfLine, ViYankEndOfGlob, ViYankEndOfWord, ViYankLeft, ViYankLine, ViYankNextGlob, ViYankNextWord, ViYankPercent, ViYankPreviousGlob, ViYankPreviousWord, ViYankRight, ViYankToEndOfLine, ViYankToFirstChar, WhatIsKey, Yank, YankLastArg, YankNthArg, YankPop

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ScriptBlock

Specifies a script block value to run when the chord is entered. PSReadLine passes one or two
parameters to this script block. The first parameter is a **ConsoleKeyInfo** object representing
the key pressed. The second argument can be any object depending on the context.

```yaml
Type: ScriptBlock
Parameter Sets: ScriptBlock
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ViMode

Specify which vi mode the binding applies to.

Valid values are:

- Insert
- Command

```yaml
Type: ViMode
Parameter Sets: (All)
Aliases:
Accepted values: Insert, Command

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose,
-WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

You cannot pipe objects to this cmdlet.

## OUTPUTS

### None

## NOTES

## RELATED LINKS

[Get-PSReadLineKeyHandler](Get-PSReadLineKeyHandler.md)

[Remove-PSReadLineKeyHandler](Remove-PSReadLineKeyHandler.md)

[Get-PSReadLineOption](Get-PSReadLineOption.md)

[Set-PSReadLineOption](Set-PSReadLineOption.md)