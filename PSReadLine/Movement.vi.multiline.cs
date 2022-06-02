using System;

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    /// <summary>
    ///     Moves the cursor to the beginning of the first logical line
    ///     of a multi-line buffer.
    /// </summary>
    /// <param name="key" />
    /// <param name="arg" />
    public void MoveToFirstLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!_renderer.LineIsMultiLine())
        {
            Ding(key, arg);
            return;
        }

        var currentLine = _renderer.GetLogicalLineNumber();

        var offset = _renderer.Current;
        var pos = _renderer.ConvertOffsetToPoint(offset);

        pos.Y -= currentLine - 1;

        var newCurrent = _renderer.ConvertLineAndColumnToOffset(pos);
        var position = GetBeginningOfLinePos(newCurrent);

        _renderer.MoveCursor(position);
    }

    /// <summary>
    ///     Moves the cursor to the beginning of the last logical logical line.
    ///     of a multi-line buffer.
    /// </summary>
    /// <param name="key" />
    /// <param name="arg" />
    public void MoveToLastLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        var count = _renderer.GetLogicalLineCount();
        if (count == 1)
        {
            Ding(key, arg);
            return;
        }

        var currentLine = _renderer.GetLogicalLineNumber();

        var offset = _renderer.Current;
        var pos = _renderer.ConvertOffsetToPoint(offset);

        pos.Y += count - currentLine;

        var newCurrent = _renderer.ConvertLineAndColumnToOffset(pos);
        var position = GetBeginningOfLinePos(newCurrent);

        _renderer.MoveCursor(position);
    }

    private void ViMoveToLine(int lineOffset)
    {
        // When moving up or down in a buffer in VI mode
        // the cursor wants to be positioned at a desired column number, which is:
        // - either a specified column number, the 0-based offset from the start of the logical line.
        // - or the end of the line
        //
        // Only one of those desired position is available at any given time.
        //
        // If the desired column number is specified, the cursor will be positioned at
        // the specified offset in the target logical line, or at the end of the line as appropriate.
        // The fact that a logical line is shorter than the desired column number *does not*
        // change its value. If a subsequent move to another logical line is performed, the
        // desired column number will take effect.
        //
        // If the desired column number is the end of the line, the cursor will be positioned at
        // the end of the target logical line.

        const int endOfLine = int.MaxValue;

        _moveToLineCommandCount += 1;

        // if this is the first "move to line" command
        // record the desired column number from the current position
        // on the logical line

        if (_moveToLineCommandCount == 1 && _moveToLineDesiredColumn == -1)
        {
            var startOfLine = GetBeginningOfLinePos(_renderer.Current);
            _moveToLineDesiredColumn = _renderer.Current - startOfLine;
        }

        // Nothing needs to be done when:
        //  - actually not moving the line, or
        //  - moving the line down when it's at the end of the last logical line.
        if (lineOffset == 0 || lineOffset > 0 && _renderer.Current == buffer.Length) return;

        int targetLineOffset;

        var currentLineIndex = _renderer.GetLogicalLineNumber() - 1;

        if (lineOffset < 0)
        {
            targetLineOffset = Math.Max(0, currentLineIndex + lineOffset);
        }
        else
        {
            var lastLineIndex = _renderer.GetLogicalLineCount() - 1;
            targetLineOffset = Math.Min(lastLineIndex, currentLineIndex + lineOffset);
        }

        var startOfTargetLinePos = GetBeginningOfNthLinePos(targetLineOffset);
        var endOfTargetLinePos = GetEndOfLogicalLinePos(startOfTargetLinePos);

        var newCurrent = _moveToLineDesiredColumn == endOfLine
            ? endOfTargetLinePos
            : Math.Min(startOfTargetLinePos + _moveToLineDesiredColumn, endOfTargetLinePos);

        _renderer.MoveCursor(newCurrent);
    }
}