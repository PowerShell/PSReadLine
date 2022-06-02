using System;

namespace Microsoft.PowerShell;

public abstract class EditItem
{
    public Action<ConsoleKeyInfo?, object> _instigator;
    public object _instigatorArg;
    public virtual bool Replaceable { get; set; }

    public abstract void Undo();
    public abstract void Redo();
}