---
schema: 2.0.0
external help file: PSReadline.dll-help.xml
---

# Get-PSReadlineKeyHandler
## SYNOPSIS
Gets the key bindings for the PSReadline module.
## SYNTAX

```
Get-PSReadlineKeyHandler [-Bound] [-Unbound]
```

## DESCRIPTION
Gets the key bindings for the PSReadline module.

If neither -Bound nor -Unbound is specified, returns all bound keys and unbound functions.

If -Bound is specified and -Unbound is not specified, only bound keys are returned.

If -Unound is specified and -Bound is not specified, only unbound keys are returned.

If both -Bound and -Unound are specified, returns all bound keys and unbound functions.
## EXAMPLES

## PARAMETERS

### -Bound
Include functions that are bound.
```yaml
Type: switch
Parameter Sets: (All)
Aliases: 

Required: False
Position: Named
Default value: True
Accept pipeline input: false
Accept wildcard characters: False
```

### -Unbound
Include functions that are unbound.
```yaml
Type: switch
Parameter Sets: (All)
Aliases: 

Required: False
Position: Named
Default value: True
Accept pipeline input: false
Accept wildcard characters: False
```

## INPUTS

### None
You cannot pipe objects to Get-PSReadlineKeyHandler
## OUTPUTS

### Microsoft.PowerShell.KeyHandler
Returns one entry for each key binding (or chord) for bound functions and/or one entry for each unbound function
## NOTES

## RELATED LINKS

[about_PSReadline]()
