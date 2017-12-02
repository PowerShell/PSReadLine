---
schema: 2.0.0
external help file: Microsoft.PowerShell.PSReadLine2.dll-help.xml
---

# Set-PSReadLineKeyHandler

## SYNOPSIS

Binds or rebinds keys to user defined or PSReadLine provided key handlers.

## SYNTAX

### Set 1

```
Set-PSReadLineKeyHandler [-Chord] <String[]> [-ScriptBlock] <ScriptBlock> [-BriefDescription <String>]
 [-Description <String>] [-ViMode <ViMode>]
```

### Set 2

```
Set-PSReadLineKeyHandler [-Chord] <String[]> [-Function] <String> [-ViMode <ViMode>]
```

## DESCRIPTION

This cmdlet is used to customize what happens when a particular key or sequence of keys is pressed while PSReadLine is reading input.

With user defined key bindings, you can do nearly anything that is possible from a PowerShell script.
Typically you might just edit the command line in some novel way, but because the handlers are just PowerShell scripts, you can do interesting things like change directories, launch programs, etc.

## EXAMPLES

### --------------  Example 1  --------------

```
PS C:\> Set-PSReadLineKeyHandler -Key UpArrow -Function HistorySearchBackward
```

This command binds the up arrow key to the function HistorySearchBackward which will use the currently entered command line as the beginning of the search string when searching through history.

### --------------  Example 2  --------------

```
PS C:\> Set-PSReadLineKeyHandler -Chord Shift+Ctrl+B -ScriptBlock {
>>    [Microsoft.PowerShell.PSConsoleReadLine]::RevertLine()
>>    [Microsoft.PowerShell.PSConsoleReadLine]::Insert('msbuild')
>>    [Microsoft.PowerShell.PSConsoleReadLine]::AcceptLine()
}
```

This example binds the key Ctrl+Shift+B to a script block that clears the line, inserts build, then accepts the line.
This example shows how a single key can be used to execute a command.

## PARAMETERS

### -Chord

The key or sequence of keys to be bound to a Function or ScriptBlock.
A single binding is specified with a single string.
If the binding is a sequence of keys, the keys are separated with a comma, e.g. "Ctrl+X,Ctrl+X".
Note that this parameter accepts multiple strings.
Each string is a separate binding, not a sequence of keys for a single binding.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -ScriptBlock

The ScriptBlock is called when the Chord is entered.
The ScriptBlock is passed one or sometimes two arguments.
The first argument is the key pressed (a ConsoleKeyInfo.)  The second argument could be any object depending on the context.

```yaml
Type: ScriptBlock
Parameter Sets: Set 1
Aliases:

Required: True
Position: 1
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -BriefDescription

A brief description of the key binding.
Used in the output of cmdlet Get-PSReadLineKeyHandler.

```yaml
Type: String
Parameter Sets: Set 1
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -Description

A more verbose description of the key binding.
Used in the output of the cmdlet Get-PSReadLineKeyHandler.

```yaml
Type: String
Parameter Sets: Set 1
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -Function

The name of an existing key handler provided by PSReadLine.
This parameter allows one to rebind existing key bindings or to bind a handler provided by PSReadLine that is currently unbound.

Using the ScriptBlock parameter, one can achieve equivalent functionality by calling the method directly from the ScriptBlock.
This parameter is preferred though - it makes it easier to determine which functions are bound and unbound.

```yaml
Type: String
Parameter Sets: Set 2
Aliases:

Required: True
Position: 1
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

### -ViMode

Specify which vi mode the binding applies to.

Valid values are:

-- Insert

-- Command

```yaml
Type: ViMode
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value:
Accept pipeline input: false
Accept wildcard characters: False
```

## INPUTS

### None

You cannot pipe objects to Set-PSReadLineKeyHandler

## OUTPUTS

### None

This cmdlet does not generate any output.

## NOTES

## RELATED LINKS

[about_PSReadLine]()
