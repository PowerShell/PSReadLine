---
schema: 2.0.0
external help file: Microsoft.PowerShell.PSReadLine2.dll-help.xml
---

# Set-PSReadLineOption

## SYNOPSIS

Customizes the behavior of command line editing in PSReadLine.

## SYNTAX

```
Set-PSReadLineOption
 [-EditMode <EditMode>]
 [-HistorySavePath <String>]
 [-HistorySaveStyle <HistorySaveStyle>]
 [-HistoryNoDuplicates]
 [-HistorySearchCaseSensitive]
 [-PromptText <string>]
 [-ExtraPromptLineCount <Int32>]
 [-Colors <Hashtable>]
 [-AddToHistoryHandler <Func[String, Object]>]
 [-CommandValidationHandler <Action[CommandAst]>]
 [-ContinuationPrompt <String>]
 [-HistorySearchCursorMovesToEnd]
 [-MaximumHistoryCount <Int32>]
 [-MaximumKillRingCount <Int32>]
 [-ShowToolTips]
 [-DingTone <Int32>]
 [-DingDuration <Int32>]
 [-BellStyle <BellStyle>]
 [-CompletionQueryItems <Int32>]
 [-WordDelimiters <string>]
 [-AnsiEscapeTimeout <int>]
 [-ViModeIndicator <ViModeStyle>]
 [-ViModeChangeHandler <ScriptBlock>]
```

## DESCRIPTION

The Set-PSReadLineOption cmdlet is used to customize the behavior of the PSReadLine module when editing the command line.

## EXAMPLES

### --------------  Example 1  --------------

This example sets some different colors.

```
PS C:\> Set-PSReadLineOption -Colors @{
    # Use a ConsoleColor enum
    "Error" = [ConsoleColor]::DarkRed

    # 24 bit color escape sequence
    "String" = "$([char]0x1b)[38;5;100m"

    # RGB value
    "Command" = "#8181f7"
}
```

### --------------  Example 2  --------------

This example demonstrates something almost like a theme using splatting:

```
PS C:\> $PSReadLineOptions = @{
    EditMode = "Emacs"
    HistoryNoDuplicates = $true
    HistorySearchCursorMovesToEnd = $true
    Colors = @{
        "Command" = "#8181f7"
    }
}
PS C:\> Set-PSReadLineOption @PSReadLineOptions
```

### --------------  Example 3  --------------

This example emits a cursor change VT escape in response to a vi mode change:

```
PS C:\> function OnViModeChange {
    if ($args[0] -eq 'Command') {
        # Set the cursor to a blinking block.
        Write-Host -NoNewLine "`e[1 q"
    } else {
        # Set the cursor to a blinking line.
        Write-Host -NoNewLine "`e[5 q"
    }
}
PS C:\> Set-PSReadLineOption -ViModeIndicator Script -ViModeChangeHandler OnViModeChange
```

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
Parameter Sets: (All)
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

    function prompt { Write-Host -NoNewLine -ForegroundColor Yellow "$pwd"; return "# " }

Then set:

    Set-PSReadLineOption -PromptText "# "


```yaml
Type: String
Parameter Sets: (All)
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
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: >>
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
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -AddToHistoryHandler

Specifies a ScriptBlock that can be used to control which commands get added to PSReadLine history,
and whether they should be saved to the history file.

The ScriptBlock is passed the command line, and it is expected to return either a Boolean value,
or an enum value of the type `[Microsoft.PowerShell.AddToHistoryOption]`.
The enum type `AddToHistoryOption` has 3 members: `SkipAdding`, `MemoryOnly`, and `MemoryAndFile`.

If the ScriptBlock returns `$true`, it's equivalent to `AddToHistoryOption.MemoryAndFile`.
The command line is added to the in-memory history queue and saved to the history file.
If the ScriptBlock returns `$false`, it's equivalent to `AddToHistoryOption.SkipAdding`,
and the command line is not added to history at all.

If the ScriptBlock returns `AddToHistoryOption.MemoryOnly`, then the command line is added to the in-memory history queue,
but will not be saved to the history file.
This usually indicates the command line has sensitive content that should not be written to disk.

PSReadLine provides a default handler to this option.
The default handler attempts to detect sensitive information in a command line by matching with a simple regex pattern:
    `"password|asplaintext|token|key|secret"`
When successfully matched, the command line is considered to contain sensitive content, and `MemoryOnly` is returned.
Otherwise, `MemoryAndFile` is returned.

To turn off the default handler, just set this option to `$null`.


```yaml
Type: Func[String, Object]
Parameter Sets: (All)
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
Parameter Sets: (All)
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
Parameter Sets: (All)
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
Parameter Sets: (All)
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
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 10
Accept pipeline input: false
Accept wildcard characters: False
```

### -ShowToolTips

When displaying possible completions, show tooltips in the list of completions.

This option was not enabled by default in earliers versions of PSReadLine, but is enabled by default now.
To disable, set this option to `$false`.

```yaml
Type: switch
Parameter Sets: (All)
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
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 0
Accept pipeline input: false
Accept wildcard characters: False
```

### -Colors

The Colors parameter is used to specify various colors used by PSReadLine.

The argument is a Hashtable where the keys specify which element and the values specify the color.

Colors can be either a value from ConsoleColor, e.g. [ConsoleColor]::Red, or a valid escape sequence.
Valid escape sequences depend on your terminal, e.g. "$([char]0x1b)[91m" (Windows PowerShell) or
"`e[91m" (PowerShell 6.0) specifies Red in most terminals.
You can specify other escape sequences as well, including but not limited to:

-- 256 color
-- 24 bit color
-- Foreground, background, or both
-- Inverse, bold

The valid keys include:

-- ContinuationPrompt: The color of the continuation prompt.

-- Emphasis: The emphasis color, e.g. the matching text when searching history.

-- Error: The error color, e.g. in the prompt.

-- Selection: The color to highlight the menu selection or selected text.

-- Default: The default token color.

-- Comment: The comment token color.

-- Keyword: The keyword token color.

-- String: The string token color.

-- Operator: The operator token color.

-- Variable: The variable token color.

-- Command: The command token color.

-- Parameter: The parameter token color.

-- Type: The type token color.

-- Number: The number token color.

-- Member: The member name token color.

```yaml
Type: Hashtable
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -DingTone

When BellStyle is set to Audible, specifies the tone of the beep.

```yaml
Type: Int32
Parameter Sets: (All)
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
Parameter Sets: (All)
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
Parameter Sets: (All)
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
Parameter Sets: (All)
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
Parameter Sets: (All)
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
Parameter Sets: (All)
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
Parameter Sets: (All)
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
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: A file named $($host.Name)_history.txt in $env:APPDATA\Microsoft\Windows\PowerShell\PSReadLine on Windows and $env:XDG_DATA_HOME/powershell/PSReadLine or $env:HOME/.local/share/powershell/PSReadLine on non-Windows platforms
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
Parameter Sets: (All)
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

-- Script - user-specified text is printed

```yaml
Type: ViModeStyle
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -ViModeChangeHandler

When the `ViModeIndicator` is set to `Script`, the script block provided will be invoked every time the mode changes. The script block is provided one argument of type `ViMode`. Example usage is shown in Example 3 in this document.

```yaml
Type: ScriptBlock
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: false
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
