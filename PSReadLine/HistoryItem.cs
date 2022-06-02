using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.PowerShell.PSReadLine;

/// <summary>
///     History details including the command line, source, and start and approximate execution time.
/// </summary>
[DebuggerDisplay("{" + nameof(CommandLine) + "}")]
public class HistoryItem
{
    internal int _editGroupStart;
    internal List<EditItem> _edits;

    internal bool _saved;
    internal bool _sensitive;
    internal int _undoEditIndex;

    /// <summary>
    ///     The command line, or if multiple lines, the lines joined
    ///     with a newline.
    /// </summary>
    public string CommandLine { get; internal set; }

    /// <summary>
    ///     The time at which the command was added to history in UTC.
    /// </summary>
    public DateTime StartTime { get; internal set; }

    /// <summary>
    ///     The approximate elapsed time (includes time to invoke Prompt).
    ///     The value can be 0 ticks if if accessed before PSReadLine
    ///     gets a chance to set it.
    /// </summary>
    public TimeSpan ApproximateElapsedTime { get; internal set; }

    /// <summary>
    ///     True if the command was from another running session
    ///     (as opposed to read from the history file at startup.)
    /// </summary>
    public bool FromOtherSession { get; internal set; }

    /// <summary>
    ///     True if the command was read in from the history file at startup.
    /// </summary>
    public bool FromHistoryFile { get; internal set; }
}