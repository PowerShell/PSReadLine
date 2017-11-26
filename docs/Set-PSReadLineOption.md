---
schema: 2.0.0
external help file: Microsoft.PowerShell.PSReadLine.dll-help.xml
---

# Set-PSReadLineOption

## SYNOPSIS

Customizes the behavior of command line editing in PSReadLine.

## SYNTAX

### OptionsSet

```
Set-PSReadLineOption
 [-EditMode <EditMode>]
 [-HistorySavePath <String>]
 [-HistorySaveStyle <HistorySaveStyle>]
 [-HistoryNoDuplicates]
 [-HistorySearchCaseSensitive]
 [-PromptText <string>]
 [-ExtraPromptLineCount <Int32>]
 [-AddToHistoryHandler <Func[String, Boolean]>]
 [-CommandValidationHandler <Action[CommandAst]>]
 [-ContinuationPrompt <String>]
 [-ContinuationPromptColor <ConsoleColor|string>]
 [-EmphasisColor <ConsoleColor|string>]
 [-ErrorColor <ConsoleColor|string>]
 [-HistorySearchCursorMovesToEnd]
 [-MaximumHistoryCount <Int32>]
 [-MaximumKillRingCount <Int32>]
 [-ResetTokenColors]
 [-ShowToolTips]
 [-DingTone <Int32>]
 [-DingDuration <Int32>]
 [-BellStyle <BellStyle>]
 [-CompletionQueryItems <Int32>]
 [-WordDelimiters <string>]
 [-AnsiEscapeTimeout <int>]
 [-ViModeIndicator <ViModeStyle>]
```

### ColorSet

```
Set-PSReadLineOption [-TokenKind] <TokenClassification> [[-Color] <ConsoleColor|string>]
```

## DESCRIPTION

The Set-PSReadLineOption cmdlet is used to customize the behavior of the PSReadLine module when editing the command line.

Color parameters accept either a ConsoleColor or a string.

If a ConsoleColor is specified, it specifies the foreground color.

If a string is specified, it should be a valid escape sequence that could specify the foreground or background color or both.
The escape sequence could also specify another attribute like bold or inverse.
A valid sequence will depend on which terminal you use.

## EXAMPLES

## PARAMETERS

### -EditMode

Specifies the command line editing mode.
This will reset any key bindings set by Set-PSReadLineKeyHandler.

Valid values are:

-- Windows: Key bindings emulate PowerShell/cmd with some bindings emulating Visual Studio.

-- Emacs: Key bindings emulate Bash or Emacs.

-- Vi: Key bindings emulate Vi.

```yaml
Type: EditMode
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: Windows
Accept pipeline input: false
Accept wildcard characters: False
```

### -PromptText

When there is a parse error, PSReadLine changes a part of the prompt red.
PSReadLine analyzes your prompt function to determine how it can change just the color of part of your prompt,
but this analysis cannot be 100% reliable.

Use this option if PSReadLine is changing your prompt in surprising ways,
be sure to include any trailing whitespace.

For example, if my prompt function looked like:

	function prompt {
		Write-Host -NoNewLine -ForegroundColor Yellow "$pwd"
		return "# "
	}

Then set:

	Set-PSReadLineOption -PromptText "# "


```yaml
Type: String
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: >
Accept pipeline input: false
Accept wildcard characters: False
```

### -ContinuationPrompt

Specifies the string displayed at the beginning of the second and subsequent lines when multi-line input is being entered.
Defaults to '\>\> '.
The empty string is valid.

```yaml
Type: String
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: >>
Accept pipeline input: false
Accept wildcard characters: False
```

### -ContinuationPromptColor

Specifies the color of the continuation prompt.

```yaml
Type: ConsoleColor or string
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -EmphasisColor

Specifies the color used for emphasis, e.g. to highlight search text.

```yaml
Type: ConsoleColor or string
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: Cyan
Accept pipeline input: false
Accept wildcard characters: False
```

### -ErrorColor

Specifies the color used for errors.

```yaml
Type: ConsoleColor or string
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: Red
Accept pipeline input: false
Accept wildcard characters: False
```

### -HistoryNoDuplicates

Repeated commands will usually be added to history to preserve ordering during recall,
but typically you don't want to see the same command multiple times when recalling or searching the history.

This option controls the recall behavior - duplicates will are still added to the history file,
but if this option is set, only the most recent invocation will appear when recalling commands.


```yaml
Type: switch
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -AddToHistoryHandler

Specifies a ScriptBlock that can be used to control which commands get added to PSReadLine history.

The ScriptBlock is passed the command line.
If the ScriptBlock returns `$true`, the command line is added to history, otherwise it is not.

```yaml
Type: Func[String, Boolean]
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -CommandValidationHandler

Specifies a ScriptBlock that is called from ValidateAndAcceptLine.
If an exception is thrown, validation fails and the error is reported.

`ValidateAndAcceptLine` is used to avoid cluttering your history with commands that can't work, e.g. specifying parameters that do not exist.

```yaml
Type: Action[CommandAst]
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -HistorySearchCursorMovesToEnd

When using `HistorySearchBackward` and `HistorySearchForward`, the default behavior leaves the cursor at the end of the search string if any.

To move the cursor to end of the line just like when there is no search string, set this option to `$true`.

```yaml
Type: switch
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -MaximumHistoryCount

Specifies the maximum number of commands to save in PSReadLine history.

Note that PSReadLine history is separate from PowerShell history.

```yaml
Type: Int32
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: 1024
Accept pipeline input: false
Accept wildcard characters: False
```

### -MaximumKillRingCount

Specifies the maximum number of items stored in the kill ring.

```yaml
Type: Int32
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: 10
Accept pipeline input: false
Accept wildcard characters: False
```

### -ResetTokenColors

Restore the token colors to the default settings.

```yaml
Type: switch
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -ShowToolTips

When displaying possible completions, show tooltips in the list of completions.

This option was not enabled by default in earliers versions of PSReadLine, but is enabled by default now.
To disable, set this option to `$false`.

```yaml
Type: switch
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: true
Accept pipeline input: false
Accept wildcard characters: False
```

### -ExtraPromptLineCount

Use this option if your prompt spans more than one line.

This option is needed less than in previous version of PSReadLine, but is useful when the `InvokePrompt` function is used.

```yaml
Type: Int32
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: 0
Accept pipeline input: false
Accept wildcard characters: False
```

### -DingTone

When BellStyle is set to Audible, specifies the tone of the beep.

```yaml
Type: Int32
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: 1221
Accept pipeline input: false
Accept wildcard characters: False
```

### -DingDuration

When BellStyle is set to Audible, specifies the duration of the beep.

```yaml
Type: Int32
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: 50ms
Accept pipeline input: false
Accept wildcard characters: False
```

### -BellStyle

Specifies how PSReadLine should respond to various error and ambiguous conditions.

Valid values are:

-- Audible: a short beep

-- Visible: a brief flash is performed

-- None: no feedback

```yaml
Type: BellStyle
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: Audible
Accept pipeline input: false
Accept wildcard characters: False
```

### -CompletionQueryItems

Specifies the maximum number of completion items that will be shown without prompting.

If the number of items to show is greater than this value, PSReadLine will prompt y/n before displaying the completion items.

```yaml
Type: Int32
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: 100
Accept pipeline input: false
Accept wildcard characters: False
```

### -WordDelimiters

Specifies the characters that delimit words for functions like ForwardWord or KillWord.

```yaml
Type: string
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: ;:,.[]{}()/\|^&*-=+
Accept pipeline input: false
Accept wildcard characters: False
```

### -HistorySearchCaseSensitive

Specifies the searching history is case sensitive in functions like ReverseSearchHistory or HistorySearchBackward.

```yaml
Type: switch
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -HistorySaveStyle

Specifies how PSReadLine should save history.

Valid values are:

-- SaveIncrementally: save history after each command is executed - and share across multiple instances of PowerShell

-- SaveAtExit: append history file when PowerShell exits

-- SaveNothing: don't use a history file

```yaml
Type: HistorySaveStyle
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: SaveIncrementally
Accept pipeline input: false
Accept wildcard characters: False
```

### -HistorySavePath

Specifies the path to the file where history is saved.

```yaml
Type: String
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: On Windows - ~\AppData\Roaming\Microsoft\Windows\PowerShell\PSReadLine\$($host.Name)_history.txt otherwise $XDG_DATA_HOME/powershell/PSReadLine/$($host.Name)_history.txt or $HOME/.local/share//powershell/PSReadLine/$($host.Name)_history.txt
Accept pipeline input: false
Accept wildcard characters: False
```

### -AnsiEscapeTimeout

This option is specific to Windows when input is redirected, e.g. when running under `tmux` or `screen`.

With redirected input on Windows, many keys are sent as a sequence of characters starting with the Escape character,
so it is, in general, impossible to distinguish between a single Escape followed by other key presses.

The assumption is the terminal sends the characters quickly, faster than a user types, so PSReadLine waits for this timeout before concluding it won't see an escape sequence.

You can experiment with this timeout if you see issues or random unexpected characters when you type.

```yaml
Type: int
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value: 100
Accept pipeline input: false
Accept wildcard characters: False
```

### -ViModeIndicator

This option sets the visual indication for the current mode in Vi mode - either insert mode or command mode.

Valid values are:

-- None - there is no indication

-- Prompt - the prompt changes color

-- Cursor - the cursor changes size

```yaml
Type: ViModeStyle
Parameter Sets: OptionsSet
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -TokenKind

Specifies the kind of token when setting token coloring options with the -Color parameter.

```yaml
Type: TokenClassification
Parameter Sets: ColorSet
Aliases:

Required: True
Position: 0
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -Color

Specifies the color for the token kind specified by the parameter -TokenKind.

```yaml
Type: ConsoleColor or string
Parameter Sets: ColorSet
Aliases:

Required: False
Position: 1
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

## INPUTS

### None

You cannot pipe objects to Set-PSReadLineOption

## OUTPUTS

### None

This cmdlet does not generate any output.

## NOTES

## RELATED LINKS

[about_PSReadLine]()
