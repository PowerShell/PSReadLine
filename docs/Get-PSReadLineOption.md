---
external help file: Microsoft.PowerShell.PSReadLine.dll-Help.xml
keywords: powershell,cmdlet
locale: en-us
Module Name: PSReadLine
ms.date: 12/07/2018
online version: http://go.microsoft.com/fwlink/?LinkId=821450
schema: 2.0.0
title: Get-PSReadLineOption
---

# Get-PSReadLineOption

## SYNOPSIS
Returns the values for the options that can be configured.

## SYNTAX

```
Get-PSReadLineOption [<CommonParameters>]
```

## DESCRIPTION

The `Get-PSReadLineOption` cmdlet returns the current state of the settings that can be configured
by using the `Set-PSReadLineOption` cmdlet. You can use the returned object to change
**PSReadLine** options. This provides a slightly simpler way to set syntax coloring options for
multiple kinds of tokens.

## EXAMPLES

### Example 1: Get options and their values

```powershell
Get-PSReadLineOption
```

```Output
EditMode                               : Windows
AddToHistoryHandler                    :
HistoryNoDuplicates                    : True
HistorySavePath                        : C:\Users\testuser\AppData\Roaming\Microsoft\Windows\PowerShell\PSReadLine\Consol
                                         eHost_history.txt
HistorySaveStyle                       : SaveIncrementally
HistorySearchCaseSensitive             : False
HistorySearchCursorMovesToEnd          : False
MaximumHistoryCount                    : 4096
ContinuationPrompt                     : >>
ExtraPromptLineCount                   : 0
PromptText                             :
BellStyle                              : Audible
DingDuration                           : 50
DingTone                               : 1221
CommandsToValidateScriptBlockArguments : {ForEach-Object, %, Invoke-Command, icm...}
CommandValidationHandler               :
CompletionQueryItems                   : 100
MaximumKillRingCount                   : 10
ShowToolTips                           : True
ViModeIndicator                        : None
WordDelimiters                         : ;:,.[]{}()/\|^&*-=+'"–—―
CommandColor                           : "`e[93m"
CommentColor                           : "`e[32m"
ContinuationPromptColor                : "`e[37m"
DefaultTokenColor                      : "`e[37m"
EmphasisColor                          : "`e[96m"
ErrorColor                             : "`e[91m"
KeywordColor                           : "`e[92m"
MemberColor                            : "`e[97m"
NumberColor                            : "`e[97m"
OperatorColor                          : "`e[90m"
ParameterColor                         : "`e[90m"
SelectionColor                         : "`e[30;47m"
StringColor                            : "`e[36m"
TypeColor                              : "`e[37m"
VariableColor                          : "`e[92m"
```

This command returns the list of available PSReadLine options and their current values.

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose,
-WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

You cannot pipe objects to this cmdlet.

## OUTPUTS

### Microsoft.PowerShell.PSConsoleReadLineOptions

## NOTES

## RELATED LINKS

[Remove-PSReadLineKeyHandler](Remove-PSReadLineKeyHandler.md)

[Get-PSReadLineKeyHandler](Get-PSReadLineKeyHandler.md)

[Set-PSReadLineOption](Set-PSReadLineOption.md)

[Set-PSReadLineKeyHandler](Set-PSReadLineKeyHandler.md)