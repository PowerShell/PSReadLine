---
external help file: Microsoft.PowerShell.PSReadLine.dll-Help.xml
keywords: powershell,cmdlet
locale: en-us
Module Name: PSReadLine
ms.date: 12/07/2018
online version: http://go.microsoft.com/fwlink/?LinkId=821453
schema: 2.0.0
title: Set-PSReadLineOption
---

# Set-PSReadLineOption

## SYNOPSIS
Customizes the behavior of command line editing in PSReadLine.

## SYNTAX

### OptionsSet

```
Set-PSReadLineOption
 [-EditMode <EditMode>]
 [-ContinuationPrompt <String>]
 [-HistoryNoDuplicates]
 [-AddToHistoryHandler <Func[String, Boolean]>]
 [-CommandValidationHandler <Action[CommandAst]>]
 [-HistorySearchCursorMovesToEnd]
 [-MaximumHistoryCount <Int32>]
 [-MaximumKillRingCount <Int32>]
 [-ShowToolTips]
 [-ExtraPromptLineCount <Int32>]
 [-DingTone <Int32>]
 [-DingDuration <Int32>]
 [-BellStyle <BellStyle>]
 [-CompletionQueryItems <Int32>]
 [-WordDelimiters <String>]
 [-HistorySearchCaseSensitive]
 [-HistorySaveStyle <HistorySaveStyle>]
 [-HistorySavePath <String>]
 [-AnsiEscapeTimeout <Int32>]
 [-PromptText <String>]
 [-ViModeIndicator <ViModeStyle>]
 [-Colors <Hashtable>]
 [<CommonParameters>]
```

## DESCRIPTION

The `Set-PSReadLineOption` cmdlet customizes the behavior of the PSReadLine module when you are editing the command line.

## EXAMPLES

### Example 1: Set color values for multiple types

This example shows three different methods for how to set the color of tokens displayed in PSReadLine.

```powershell
Set-PSReadLineOption -Colors @{
 # Use a ConsoleColor enum
 "Error" = [ConsoleColor]::DarkRed

 # 24 bit color escape sequence
 "String" = "$([char]0x1b)[38;5;100m"

 # RGB value
 "Command" = "#8181f7"
}
```

### Example 2: Set bell style

This cmdlet instructs PSReadLine to respond to errors or conditions that require user attention
by emitting an audible beep at 1221 Hz for 60 ms.

```powershell
Set-PSReadLineOption -BellStyle Audible -DingTone 1221 -DingDuration 60
```

### Example 3: Set multiple options

```powershell
$PSReadLineOptions = @{
    EditMode = "Emacs"
    HistoryNoDuplicates = $true
    HistorySearchCursorMovesToEnd = $true
    Colors = @{
        "Command" = "#8181f7"
    }
}
Set-PSReadLineOption @PSReadLineOptions
```

## PARAMETERS

### -AddToHistoryHandler

Specifies a **ScriptBlock** that controls which commands get added to PSReadLine history.

The **ScriptBlock** receives the command line as input. If the ScriptBlock returns `$true`, the
command line is added to the history.

```yaml
Type: Func[String, Boolean]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AnsiEscapeTimeout

This option is specific to Windows when input is redirected, for example, when running under `tmux`
or `screen`.

With redirected input on Windows, many keys are sent as a sequence of characters starting with the
escape character. It's impossible to distinguish between a single escape character followed by
more characters and a valid escape sequence.

The assumption is that the terminal can send the characters faster than a user types. PSReadLine
waits for this timeout before concluding that it has received a complete escape sequence.

If you see random or unexpected characters when you type, you can adjust this timeout.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 100
Accept pipeline input: False
Accept wildcard characters: False
```

### -BellStyle

Specifies how PSReadLine responds to various error and ambiguous conditions.

Valid values are:

- Audible: A short beep
- Visual: Text flashes briefly
- None: No feedback

```yaml
Type: BellStyle
Parameter Sets: (All)
Aliases:
Accepted values: None, Visual, Audible

Required: False
Position: Named
Default value: Audible
Accept pipeline input: False
Accept wildcard characters: False
```

### -Colors

The Colors parameter is used to specify various colors used by PSReadLine.

The argument is a Hashtable where the keys specify which element and the values specify the color.

Colors can be either a value from ConsoleColor, for example `[ConsoleColor]::Red`, or a valid
escape sequence. Valid escape sequences depend on your terminal. In Windows PowerShell, an example
escape sequence is `$([char]0x1b)[91m`. In PowerShell 6, the same escape sequence is `e[91m`. You
can specify other escape sequences including:

- 256 color
- 24-bit color
- Foreground, background, or both
- Inverse, bold

The valid keys include:

- ContinuationPrompt: The color of the continuation prompt.
- Emphasis: The emphasis color. For example, the matching text when searching history.
- Error: The error color. For example, in the prompt.
- Selection: The color to highlight the menu selection or selected text.
- Default: The default token color.
- Comment: The comment token color.
- Keyword: The keyword token color.
- String: The string token color.
- Operator: The operator token color.
- Variable: The variable token color.
- Command: The command token color.
- Parameter: The parameter token color.
- Type: The type token color.
- Number: The number token color.
- Member: The member name token color.

```yaml
Type: Hashtable
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CommandValidationHandler

Specifies a **ScriptBlock** that is called from **ValidateAndAcceptLine**. If an exception is
thrown, validation fails and the error is reported.

Before throwing an exception, the validation handler can place the cursor at the point of the error
to make it easier to fix. A validation handler can also change the command line, such as to correct
common typographical errors.

**ValidateAndAcceptLine** is used to avoid cluttering your history with commands that can't work.

```yaml
Type: Action[CommandAst]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CompletionQueryItems

Specifies the maximum number of completion items that are shown without prompting.

If the number of items to show is greater than this value, PSReadLine prompts "y/n" before
displaying the completion items.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 100
Accept pipeline input: False
Accept wildcard characters: False
```

### -ContinuationPrompt

Specifies the string displayed at the beginning of the subsequent lines when multi-line input is
entered. Defaults to `>> `. The empty string is valid.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: >>
Accept pipeline input: False
Accept wildcard characters: False
```

### -DingDuration

Specifies the duration of the beep when **BellStyle** is set to **Audible**.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 50ms
Accept pipeline input: False
Accept wildcard characters: False
```

### -DingTone

Specifies the tone in Hertz (Hz) of the beep when **BellStyle** is set to **Audible**.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 1221
Accept pipeline input: False
Accept wildcard characters: False
```

### -EditMode

Specifies the command line editing mode. Using this parameter resets any key bindings set by
`Set-PSReadLineKeyHandler`.

Valid values are:

- Windows: Key bindings emulate PowerShell, cmd, and Visual Studio.
- Emacs: Key bindings emulate Bash or Emacs.
- Vi: Key bindings emulate Vi.

```yaml
Type: EditMode
Parameter Sets: (All)
Aliases:
Accepted values: Windows, Emacs, Vi

Required: False
Position: Named
Default value: Windows
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExtraPromptLineCount

Use this option if your prompt spans more than one line.

This option is needed less than in previous version of PSReadLine, but is useful when the
`InvokePrompt` function is used.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -HistoryNoDuplicates

This option controls the recall behavior. Duplicate commands are still added to the history file.
When this option is set, only the most recent invocation appears when recalling commands.

Repeated commands are added to history to preserve ordering during recall. However, you
typically don't want to see the command multiple times when recalling or searching the history.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -HistorySavePath

Specifies the path to the file where history is saved.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: A file named $($host.Name)_history.txt in $env:APPDATA\Microsoft\Windows\PowerShell\PSReadLine on Windows and $env:XDG_DATA_HOME/powershell/PSReadLine or $env:HOME/.local/share/powershell/PSReadLine on non-Windows platforms
Accept pipeline input: False
Accept wildcard characters: False
```

### -HistorySaveStyle

Specifies how PSReadLine saves history.

Valid values are:

- SaveIncrementally: Save history after each command is executed and share across multiple
  instances of PowerShell
- SaveAtExit: Append history file when PowerShell exits
- SaveNothing: Don't use a history file

```yaml
Type: HistorySaveStyle
Parameter Sets: (All)
Aliases:
Accepted values: SaveIncrementally, SaveAtExit, SaveNothing

Required: False
Position: Named
Default value: SaveIncrementally
Accept pipeline input: False
Accept wildcard characters: False
```

### -HistorySearchCaseSensitive

Specifies that history searching is case-sensitive in functions like **ReverseSearchHistory** or **HistorySearchBackward**.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -HistorySearchCursorMovesToEnd

Indicates that the cursor moves to the end of commands that you load from history by using a search.
When this parameter is set to `$False`, the cursor remains at the position it was when you pressed the up or down arrows.

To turn off this option, you can run either of the following commands:

`Set-PSReadLineOption -HistorySearchCursorMovesToEnd:$False`

`(Get-PSReadLineOption).HistorySearchCursorMovesToEnd = $False`

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumHistoryCount

Specifies the maximum number of commands to save in PSReadLine history.

PSReadLine history is separate from PowerShell history.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumKillRingCount

Specifies the maximum number of items stored in the kill ring.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 10
Accept pipeline input: False
Accept wildcard characters: False
```

### -PromptText

When there is a parse error, PSReadLine changes a part of the prompt red. PSReadLine analyzes your
prompt function to determine how to change only the color of part of your prompt. This analysis is
not 100% reliable.

Use this option if PSReadLine is changing your prompt in surprising ways, be sure to include any
trailing whitespace.

For example, if my prompt function looked like:

`function prompt { Write-Host -NoNewLine -ForegroundColor Yellow "$pwd"; return "# " }`

Then set:

`Set-PSReadLineOption -PromptText "# "`

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: >
Accept pipeline input: False
Accept wildcard characters: False
```

### -ShowToolTips

When displaying possible completions,  tooltips are shown in the list of completions.

This option is enabled by default. This option was not enabled by default in prior versions of
PSReadLine. To disable, set this option to `$False`.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: True
Accept pipeline input: False
Accept wildcard characters: False
```

### -ViModeIndicator

This option sets the visual indication for the current mode in Vi mode; either insert mode or
command mode.

Valid values are:

- None - there is no indication
- Prompt - the prompt changes color
- Cursor - the cursor changes size

```yaml
Type: ViModeStyle
Parameter Sets: (All)
Aliases:
Accepted values: None, Prompt, Cursor

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WordDelimiters

Specifies the characters that delimit words for functions like **ForwardWord** or **KillWord**.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: ;:,.[]{}()/\|^&*-=+'"–—―
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

This cmdlet does not generate output.

## NOTES

## RELATED LINKS

[Get-PSReadLineKeyHandler](Get-PSReadLineKeyHandler.md)

[Remove-PSReadLineKeyHandler](Remove-PSReadLineKeyHandler.md)

[Get-PSReadLineOption](Get-PSReadLineOption.md)

[Set-PSReadLineKeyHandler](Set-PSReadLineKeyHandler.md)
