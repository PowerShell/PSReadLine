---
schema: 2.0.0
external help file: Microsoft.PowerShell.PSReadLine.dll-help.xml
---

# Get-PSReadLineOption

## SYNOPSIS

Returns the values for the options that can be configured.

## SYNTAX

```
Get-PSReadLineOption
```

## DESCRIPTION

Get-PSReadLineOption returns the current state of the settings that can be configured by Set-PSReadLineOption.

The object returned can be used to change PSReadLine options.

This provides a slightly simpler way of setting syntax coloring options for multiple kinds of tokens.

## EXAMPLES

## PARAMETERS

## INPUTS

### None

You cannot pipe objects to Get-PSReadLineOption

## OUTPUTS

### Microsoft.PowerShell.PSConsoleReadlineOptions

An instance of the current options.
Changing the values will update the settings in PSReadLine directly without invoking Set-PSReadLineOption.

## NOTES

## RELATED LINKS

[about_PSReadLine]()
