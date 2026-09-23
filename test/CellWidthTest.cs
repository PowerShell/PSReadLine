// Copyright (c) Microsoft Corporation.
// Licensed under the 2-Clause BSD License.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public class CellWidthTest
    {
        static CellWidthTest()
        {
            RuntimeHelpers.RunClassConstructor(typeof(ReadLine).TypeHandle);
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("ABC", 3)]
        [InlineData("\u0003", 2)]
        [InlineData("\u754c", 2)]
        [InlineData("\u2764\ufe0f", 2)]
        [InlineData("\u0b95", 1)]
        [InlineData("\u0b95\u0b82", 1)]
        [InlineData("\u0b95\u0bcd", 1)]
        [InlineData("\u0b95\u0bc0", 1)]
        [InlineData("\u0b95\u0bbe", 2)]
        [InlineData("\u0b95\u0bbf", 2)]
        [InlineData("\u0b95\u0bc1", 2)]
        [InlineData("\u001b[31m\u0b95\u0bcd\u001b[0m", 1)]
        [InlineData("\u0bb5\u0ba3\u0b95\u0bcd\u0b95\u0bae\u0bcd \u0b87\u0baa\u0bcd\u0baa\u0b9f\u0bbf \u0b87\u0bb0\u0bc1\u0b95\u0bcd\u0b95\u0bc0\u0b99\u0bcd\u0b95 ?", 21)]
        public void TamilCellWidths(string text, int expected)
        {
            Assert.Equal(expected, PSConsoleReadLine.LengthInBufferCells(text));
            string padded = "x" + text + "y";
            Assert.Equal(expected, PSConsoleReadLine.LengthInBufferCells(padded, 1, padded.Length - 1));
            Assert.Equal(expected, PSConsoleReadLine.LengthInBufferCells(new StringBuilder(padded), 1, padded.Length - 1));
        }

        [Theory]
        [InlineData("\u0b95\u0bcdX", 0, 1, false, 2)]
        [InlineData("\u0b95\u0bc0\u0b82X", 0, 1, false, 3)]
        [InlineData("X\u0b95\u0bcdY", 1, 1, false, 2)]
        [InlineData("\u0b95\u0bcd", 0, 0, false, 0)]
        [InlineData("\u0b95\u0bcd", 1, 0, true, 0)]
        [InlineData("\u0b95\u0bcd", 1, 1, true, 2)]
        [InlineData("X\u0b95\u0bcdY", 2, 1, true, 2)]
        [InlineData("\u754c\u0bcd", 1, 1, true, 0)]
        [InlineData("\u754c\u0bcdX", 2, 2, true, 1)]
        [InlineData("ABC", 0, 2, false, 2)]
        [InlineData("ABC", 2, 2, true, 2)]
        public void CellSubstringKeepsAttachedMarks(string text, int start, int cells, bool fromEnd, int expected)
        {
            string name = fromEnd ? "SubstringLengthByCellsFromEnd" : "SubstringLengthByCells";
            var method = typeof(PSConsoleReadLine).GetMethod(
                name, BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(int), typeof(int) }, null);
            Assert.NotNull(method);
            Assert.Equal(expected, (int)method.Invoke(null, new object[] { text, start, cells }));
        }

        [Theory]
        [InlineData("abc\u0b95\u0bcd", 1, 4)]
        [InlineData("abc\u0b95\u0bcdX", 2, 1)]
        [InlineData("abc\u0b95\u001b[31m\u0bcd\u001b[0m", 1, 4)]
        public void AttachedMarkDoesNotWrapToAnotherPhysicalLine(string text, int lines, int lastLineWidth)
        {
            var data = new RenderedLineData(text, isFirstLogicalLine: true);
            Assert.Equal(lines, data.PhysicalLineCount(4, 0, out int actualLastLineWidth));
            Assert.Equal(lastLineWidth, actualLastLineWidth);
        }
    }
}
