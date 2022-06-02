/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    internal void WriteBlankLines(int count)
    {
        Renderer.Console.BlankRestOfLine();
        for (var i = 1; i < count; i++)
        {
            Renderer.Console.Write("\n");
            Renderer.Console.BlankRestOfLine();
        }
    }

    internal void WriteBlankLines(int top, int count)
    {
        var savedCursorLeft = Renderer.Console.CursorLeft;
        var savedCursorTop = Renderer.Console.CursorTop;

        Renderer.Console.SetCursorPosition(0, top);
        WriteBlankLines(count);
        Renderer.Console.SetCursorPosition(savedCursorLeft, savedCursorTop);
    }

    internal void WriteBlankRestOfLine(int left, int top)
    {
        var savedCursorLeft = Renderer.Console.CursorLeft;
        var savedCursorTop = Renderer.Console.CursorTop;

        Renderer.Console.SetCursorPosition(left, top);
        Renderer.Console.BlankRestOfLine();
        Renderer.Console.SetCursorPosition(savedCursorLeft, savedCursorTop);
    }

    internal static string Spaces(int cnt)
    {
        return cnt < Renderer.SpacesArr.Length
            ? Renderer.SpacesArr[cnt] ?? (Renderer.SpacesArr[cnt] = new string(' ', cnt))
            : new string(' ', cnt);
    }

    private static string SubstringByCells(string text, int countOfCells)
    {
        return SubstringByCells(text, 0, countOfCells);
    }

    private static string SubstringByCells(string text, int start, int countOfCells)
    {
        var length = SubstringLengthByCells(text, start, countOfCells);
        return length == 0 ? string.Empty : text.Substring(start, length);
    }

    private static int SubstringLengthByCells(string text, int countOfCells)
    {
        return SubstringLengthByCells(text, 0, countOfCells);
    }

    private static int SubstringLengthByCells(string text, int start, int countOfCells)
    {
        var cellLength = 0;
        var charLength = 0;

        for (var i = start; i < text.Length; i++)
        {
            cellLength += _renderer.LengthInBufferCells(text[i]);

            if (cellLength > countOfCells) return charLength;

            charLength++;

            if (cellLength == countOfCells) return charLength;
        }

        return charLength;
    }

    private static int SubstringLengthByCellsFromEnd(string text, int start, int countOfCells)
    {
        var cellLength = 0;
        var charLength = 0;

        for (var i = start; i >= 0; i--)
        {
            cellLength += _renderer.LengthInBufferCells(text[i]);

            if (cellLength > countOfCells) return charLength;

            charLength++;

            if (cellLength == countOfCells) return charLength;
        }

        return charLength;
    }
}