/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Windows.Forms;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private static void InvertLines(int start, int count)
        {
            var buffer = ReadBufferLines(start, count);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i].ForegroundColor = (ConsoleColor)((int)buffer[i].ForegroundColor ^ 7);
                buffer[i].BackgroundColor = (ConsoleColor)((int)buffer[i].BackgroundColor ^ 7);
            }
            WriteBufferLines(buffer, ref start);
        }

        /// <summary>
        /// Start interactive screen capture - up/down arrows select lines, enter copies
        /// selected text to clipboard as text and html
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public static void CaptureScreen(ConsoleKeyInfo? key = null, object arg = null)
        {
            int selectionTop = Console.CursorTop;
            int selectionHeight = 1;
            int currentY = selectionTop;

            // Current lines starts out selected
            InvertLines(selectionTop, selectionHeight);
            bool done = false;
            while (!done)
            {
                var k = ReadKey();
                switch (k.Key)
                {
                case ConsoleKey.UpArrow:
                    if (currentY > 0)
                    {
                        currentY -= 1;
                        if ((k.Modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift)
                        {
                            if (currentY < selectionTop)
                            {
                                // Extend selection up, only invert newly selected line.
                                InvertLines(currentY, 1);
                                selectionTop = currentY;
                                selectionHeight += 1;
                            }
                            else if (currentY >= selectionTop)
                            {
                                // Selection shortend 1 line, invert unselected line.
                                InvertLines(currentY + 1, 1);
                                selectionHeight -= 1;
                            }
                            break;
                        }
                        goto updateSelectionCommon;
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (currentY < (Console.BufferHeight - 1))
                    {
                        currentY += 1;
                        if ((k.Modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift)
                        {
                            if (currentY == (selectionTop + selectionHeight))
                            {
                                // Extend selection down, only invert newly selected line.
                                InvertLines(selectionTop + selectionHeight, 1);
                                selectionHeight += 1;
                            }
                            else if (currentY == (selectionTop + 1))
                            {
                                // Selection shortend 1 line, invert unselected line.
                                InvertLines(selectionTop, 1);
                                selectionTop = currentY;
                                selectionHeight -= 1;
                            }
                            break;
                        }
                        goto updateSelectionCommon;
                    }
                    break;

                updateSelectionCommon:
                    // Shift not pressed - unselect current selection
                    InvertLines(selectionTop, selectionHeight);
                    selectionTop = currentY;
                    selectionHeight = 1;
                    InvertLines(selectionTop, selectionHeight);
                    break;

                case ConsoleKey.Enter:
                    InvertLines(selectionTop, selectionHeight);
                    DumpScreenToClipboard(selectionTop, selectionHeight);
                    return;

                case ConsoleKey.Escape:
                    done = true;
                    continue;

                case ConsoleKey.C:
                case ConsoleKey.G:
                    if (k.Modifiers == ConsoleModifiers.Control)
                    {
                        done = true;
                        continue;
                    }
                    Ding();
                    break;
                default:
                    Ding();
                    break;
                }
            }
            InvertLines(selectionTop, selectionHeight);
        }

        private const string CmdColorTable = @"
\red0\green0\blue0;
\red0\green0\blue128;
\red0\green128\blue0;
\red0\green128\blue128;
\red128\green0\blue0;
\red128\green0\blue128;
\red128\green128\blue0;
\red192\green192\blue192;
\red128\green128\blue128;
\red0\green0\blue255;
\red0\green255\blue0;
\red0\green255\blue255;
\red255\green0\blue0;
\red255\green0\blue255;
\red255\green255\blue0;
\red255\green255\blue255;
";

        private const string PowerShellColorTable = @"
\red1\green36\blue86;
\red0\green0\blue128;
\red0\green128\blue0;
\red0\green128\blue128;
\red128\green0\blue0;
\red1\green36\blue86;
\red238\green237\blue240;
\red192\green192\blue192;
\red128\green128\blue128;
\red0\green0\blue255;
\red0\green255\blue0;
\red0\green255\blue255;
\red255\green0\blue0;
\red255\green0\blue255;
\red255\green255\blue0;
\red255\green255\blue255;
";

        private static void DumpScreenToClipboard(int top, int count)
        {
            var buffer = ReadBufferLines(top, count);
            var bufferWidth = Console.BufferWidth;

            var dataObject = new DataObject();
            var textBuffer = new StringBuilder(buffer.Length + count);

            var rtfBuffer = new StringBuilder();
            rtfBuffer.Append(@"{\rtf\ansi{\fonttbl{\f0 Consolas;}}");

            // A bit of a hack because I don't know how to find the shortcut used to start
            // the current console.  We assume if the background color is Magenta, then
            // PowerShell's color scheme is being used, otherwise we assume the default scheme.
            var colorTable = Console.BackgroundColor == ConsoleColor.DarkMagenta
                                 ? PowerShellColorTable
                                 : CmdColorTable;
            rtfBuffer.AppendFormat(@"{{\colortbl;{0}}}{1}", colorTable, Environment.NewLine);
            rtfBuffer.Append(@"\f0 \fs18 ");

            var charInfo = buffer[0];
            var fgColor = (int)charInfo.ForegroundColor;
            var bgColor = (int)charInfo.BackgroundColor;
            rtfBuffer.AppendFormat(@"{{\cf{0}\chshdng0\chcbpat{1} ", fgColor + 1, bgColor + 1);
            for (int i = 0; i < count; i++)
            {
                var spaces = 0;
                var rtfSpaces = 0;
                for (int j = 0; j < bufferWidth; j++)
                {
                    charInfo = buffer[i * bufferWidth + j];
                    if ((int)charInfo.ForegroundColor != fgColor || (int)charInfo.BackgroundColor != bgColor)
                    {
                        if (rtfSpaces > 0)
                        {
                            rtfBuffer.Append(' ', rtfSpaces);
                            rtfSpaces = 0;
                        }
                        fgColor = (int)charInfo.ForegroundColor;
                        bgColor = (int)charInfo.BackgroundColor;
                        rtfBuffer.AppendFormat(@"}}{{\cf{0}\chshdng0\chcbpat{1} ", fgColor + 1, bgColor + 1);
                    }

                    var c = (char)charInfo.UnicodeChar;
                    if (c == ' ')
                    {
                        // Trailing spaces are skipped, we'll add them back if we find a non-space
                        // before the end of line
                        ++spaces;
                        ++rtfSpaces;
                    }
                    else
                    {
                        if (spaces > 0)
                        {
                            textBuffer.Append(' ', spaces);
                            spaces = 0;
                        }
                        if (rtfSpaces > 0)
                        {
                            rtfBuffer.Append(' ', rtfSpaces);
                            rtfSpaces = 0;
                        }

                        textBuffer.Append(c);
                        switch (c)
                        {
                        case '\\': rtfBuffer.Append(@"\\"); break;
                        case '\t': rtfBuffer.Append(@"\tab"); break;
                        case '{':  rtfBuffer.Append(@"\{"); break;
                        case '}':  rtfBuffer.Append(@"\}"); break;
                        default:   rtfBuffer.Append(c); break;
                        }
                    }
                }
                rtfBuffer.AppendFormat(@"\shading0 \cbpat{0} \par{1}", bgColor + 1, Environment.NewLine);
                textBuffer.Append(Environment.NewLine);
            }
            rtfBuffer.Append("}}");

            dataObject.SetData(DataFormats.Text, textBuffer.ToString());
            dataObject.SetData(DataFormats.Rtf, rtfBuffer.ToString());
            Clipboard.SetDataObject(dataObject, copy: true);
        }
    }
}
