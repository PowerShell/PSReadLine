---
schema: 2.0.0
external help file: PSReadline.dll-help.xml
---

# Set-PSReadlineOption
## SYNOPSIS
Customizes the behavior of command line editing in PSReadline.
## SYNTAX

### Set 1
```
Set-PSReadlineOption [-EditMode <EditMode>] [-ContinuationPrompt <String>]
 [-ContinuationPromptForegroundColor <ConsoleColor>] [-ContinuationPromptBackgroundColor <ConsoleColor>]
 [-EmphasisForegroundColor <ConsoleColor>] [-EmphasisBackgroundColor <ConsoleColor>]
 [-ErrorForegroundColor <ConsoleColor>] [-ErrorBackgroundColor <ConsoleColor>] [-HistoryNoDuplicates]
 [-AddToHistoryHandler <Func[String, Boolean]>] [-ValidationHandler <Func[String, Object]>]
 [-HistorySearchCursorMovesToEnd] [-MaximumHistoryCount <Int32>] [-MaximumKillRingCount <Int32>]
 [-ResetTokenColors] [-ShowToolTips] [-ExtraPromptLineCount <Int32>] [-DingTone <Int32>]
 [-DingDuration <Int32>] [-BellStyle <BellStyle>] [-CompletionQueryItems <Int32>] [-WordDelimiters <string>]
 [-HistorySearchCaseSensitive] [-HistorySaveStyle <HistorySaveStyle>] [-HistorySavePath <String>]
```

### Set 2
```
Set-PSReadlineOption [-TokenKind] <TokenClassification> [[-ForegroundColor] <ConsoleColor>]
 [[-BackgroundColor] <ConsoleColor>]
```

## DESCRIPTION
The Set-PSReadlineOption cmdlet is used to customize the behavior of the PSReadline module when editing the command line.
## EXAMPLES

## PARAMETERS

### -EditMode
Specifies the command line editing mode.
This will reset any key bindings set by Set-PSReadlineKeyHandler.

Valid values are:

-- Windows: Key bindings emulate PowerShell/cmd with some bindings emulating Visual Studio.

-- Emacs: Key bindings emulate Bash or Emacs.
```yaml
Type: EditMode
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: Windows
Accept pipeline input: false
Accept wildcard characters: False
```

### -ContinuationPrompt
Specifies the string displayed at the beginning of the second and subsequent lines when multi-line input is being entered.
Defaults to '\>\>\> '.
The empty string is valid.
```yaml
Type: String
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: >>>
Accept pipeline input: false
Accept wildcard characters: False
```

### -ContinuationPromptForegroundColor
Specifies the foreground color of the continuation prompt.
```yaml
Type: ConsoleColor
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -ContinuationPromptBackgroundColor
Specifies the background color of the continuation prompt.
```yaml
Type: ConsoleColor
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -EmphasisForegroundColor
Specifies the foreground color used for emphasis, e.g.
to highlight search text.
```yaml
Type: ConsoleColor
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: Cyan
Accept pipeline input: false
Accept wildcard characters: False
```

### -EmphasisBackgroundColor
Specifies the background color used for emphasis, e.g.
to highlight search text.
```yaml
Type: ConsoleColor
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -ErrorForegroundColor
Specifies the foreground color used for errors.
```yaml
Type: ConsoleColor
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: Red
Accept pipeline input: false
Accept wildcard characters: False
```

### -ErrorBackgroundColor
Specifies the background color used for errors.
```yaml
Type: ConsoleColor
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -HistoryNoDuplicates
Specifies that duplicate commands should not be added to PSReadline history.
```yaml
Type: switch
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -AddToHistoryHandler
Specifies a ScriptBlock that can be used to control which commands get added to PSReadline history.
```yaml
Type: Func[String, Boolean]
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -ValidationHandler
Specifies a ScriptBlock that is called from ValidateAndAcceptLine.
If a non-null object is returned or an exception is thrown, validation fails and the error is reported.
If the object returned/thrown has a Message property, it's value is used in the error message, and if there is an Offset property, the cursor is moved to that offset after reporting the error.
If there is no Message property, the ToString method is called to report the error.
```yaml
Type: Func[String, Object]
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -HistorySearchCursorMovesToEnd
```yaml
Type: switch
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -MaximumHistoryCount
Specifies the maximum number of commands to save in PSReadline history.
Note that PSReadline history is separate from PowerShell history.
```yaml
Type: Int32
Parameter Sets: Set 1
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
Parameter Sets: Set 1
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
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -ShowToolTips
When displaying possible completions, show tooltips in the list of completions.
```yaml
Type: switch
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -ExtraPromptLineCount
Use this option if your prompt spans more than one line and you want the extra lines to appear when PSReadline displays the prompt after showing some output, e.g.
when showing a list of completions.
```yaml
Type: Int32
Parameter Sets: Set 1
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
Parameter Sets: Set 1
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
Parameter Sets: Set 1
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
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: Audible
Accept pipeline input: false
Accept wildcard characters: False
```

### -CompletionQueryItems
Specifies the maximum number of completion items that will be shown without prompting.
If the number of items to show is greater than this value, PSReadline will prompt y/n before displaying the completion items.
```yaml
Type: Int32
Parameter Sets: Set 1
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
Parameter Sets: Set 1
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
Parameter Sets: Set 1
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
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: SaveIncrementally
Accept pipeline input: false
Accept wildcard characters: False
```

### -HistorySavePath
Specifies the path to the history file.
```yaml
Type: String
Parameter Sets: Set 1
Aliases: 

Required: False
Position: Named
Default value: ~\AppData\Roaming\PSReadline\$($host.Name)_history.txt
Accept pipeline input: false
Accept wildcard characters: False
```

### -TokenKind
Specifies the kind of token when setting token coloring options with the -ForegroundColor and -BackgroundColor parameters.
```yaml
Type: TokenClassification
Parameter Sets: Set 2
Aliases: 

Required: True
Position: 0
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -ForegroundColor
Specifies the foreground color for the token kind specified by the parameter -TokenKind.
```yaml
Type: ConsoleColor
Parameter Sets: Set 2
Aliases: 

Required: False
Position: 1
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

### -BackgroundColor
Specifies the background color for the token kind specified by the parameter -TokenKind.
```yaml
Type: ConsoleColor
Parameter Sets: Set 2
Aliases: 

Required: False
Position: 2
Default value: 
Accept pipeline input: false
Accept wildcard characters: False
```

## INPUTS

### None
You cannot pipe objects to Set-PSReadlineOption
## OUTPUTS

### None
This cmdlet does not generate any output.
## NOTES

## RELATED LINKS

[about_PSReadline]()



