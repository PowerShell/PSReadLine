/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Security;
using ConsoleHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;
using System.ComponentModel;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private CHAR_INFO[] _consoleBuffer;
        private int _initialX;
        private int _initialY;
        private int _bufferWidth;
        private ConsoleColor _initialBackgroundColor;
        private ConsoleColor _initialForegroundColor;
        private CHAR_INFO _space;
        private int _current;
        private int _emphasisStart;
        private int _emphasisLength;
        [SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources")]
        private IntPtr _hwnd = (IntPtr)0;
        [SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources")]
        private IntPtr _hDC = (IntPtr)0;
        private uint _codePage;
        private bool _istmInitialized = false;
        private TEXTMETRIC _tm = new TEXTMETRIC();
        private bool _trueTypeInUse = false;

        private readonly Lazy<ConsoleHandle> _outputHandle = new Lazy<ConsoleHandle>(() =>
        {
            // We use CreateFile here instead of GetStdWin32Handle, as GetStdWin32Handle will return redirected handles
            var handle = NativeMethods.CreateFile(
                "CONOUT$",
                (UInt32)(AccessQualifiers.GenericRead | AccessQualifiers.GenericWrite),
                (UInt32)ShareModes.ShareWrite,
                (IntPtr)0,
                (UInt32)CreationDisposition.OpenExisting,
                0,
                (IntPtr)0);

            if (handle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                int err = Marshal.GetLastWin32Error();
                Win32Exception innerException = new Win32Exception(err);
                throw new Exception("Failed to retreive the input console handle.", innerException);
            }

            return new ConsoleHandle(handle, true);
        }
        );

        private class SavedTokenState
        {
            internal Token[] Tokens { get; set; }
            internal int Index { get; set; }
            internal ConsoleColor BackgroundColor { get; set; }
            internal ConsoleColor ForegroundColor { get; set; }
        }

        private void MaybeParseInput()
        {
            if (_tokens == null)
            {
                ParseInput();
            }
        }

        private string ParseInput()
        {
            var text = _buffer.ToString();
            _ast = Parser.ParseInput(text, out _tokens, out _parseErrors);
            return text;
        }

        private void ClearStatusMessage(bool render)
        {
            _statusBuffer.Clear();
            _statusLinePrompt = null;
            _statusIsErrorMessage = false;
            if (render)
            {
                Render();
            }
        }

        private void Render()
        {
            // If there are a bunch of keys queued up, skip rendering if we've rendered
            // recently.
            if (_queuedKeys.Count > 10 && (_lastRenderTime.ElapsedMilliseconds < 50))
            {
                // We won't render, but most likely the tokens will be different, so make
                // sure we don't use old tokens.
                _tokens = null;
                _ast = null;
                return;
            }

            ReallyRender();
        }

        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults")]
        private void ReallyRender()
        {
            var text = ParseInput();
            _codePage = NativeMethods.GetConsoleOutputCP();
            _istmInitialized = false;
            ConsoleHandle consoleHandle = _outputHandle.Value;
            CONSOLE_FONT_INFO_EX fontInfo = GetConsoleFontInfo(consoleHandle);
            int fontType = fontInfo.FontFamily & NativeMethods.FontTypeMask;
            _trueTypeInUse = (fontType & NativeMethods.TrueTypeFont) == NativeMethods.TrueTypeFont;

            int statusLineCount = GetStatusLineCount();
            int j = _initialX + (_bufferWidth * Options.ExtraPromptLineCount);
            var backgroundColor = _initialBackgroundColor;
            var foregroundColor = _initialForegroundColor;
            bool afterLastToken = false;
            int totalBytes = j;
            int bufferWidth = Console.BufferWidth;

            var tokenStack = new Stack<SavedTokenState>();
            tokenStack.Push(new SavedTokenState
            {
                Tokens = _tokens,
                Index = 0,
                BackgroundColor = _initialBackgroundColor,
                ForegroundColor = _initialForegroundColor
            });

            int bufferLineCount;

            try
            {
                bufferLineCount = ConvertOffsetToCoordinates(text.Length).Y - _initialY + 1 + statusLineCount;
                if (_consoleBuffer.Length != bufferLineCount * bufferWidth)
                {
                    var newBuffer = new CHAR_INFO[bufferLineCount * bufferWidth];
                    Array.Copy(_consoleBuffer, newBuffer, _initialX + (Options.ExtraPromptLineCount * _bufferWidth));
                    if (_consoleBuffer.Length > bufferLineCount * bufferWidth)
                    {
                        int consoleBufferOffset = ConvertOffsetToConsoleBufferOffset(text.Length, _initialX + (Options.ExtraPromptLineCount * _bufferWidth));
                        // Need to erase the extra lines that we won't draw again
                        for (int i = consoleBufferOffset; i < _consoleBuffer.Length; i++)
                        {
                            _consoleBuffer[i] = _space;
                        }
                        WriteBufferLines(_consoleBuffer, ref _initialY);
                    }
                    _consoleBuffer = newBuffer;
                }

                for (int i = 0; i < text.Length; i++)
                {
                    SavedTokenState state = null;
                    totalBytes = totalBytes % bufferWidth;
                    if (!afterLastToken)
                    {
                        // Figure out the color of the character - if it's in a token,
                        // use the tokens color otherwise use the initial color.
                        state = tokenStack.Peek();
                        var token = state.Tokens[state.Index];
                        if (i == token.Extent.EndOffset)
                        {
                            if (token == state.Tokens[state.Tokens.Length - 1])
                            {
                                tokenStack.Pop();
                                if (tokenStack.Count == 0)
                                {
                                    afterLastToken = true;
                                    token = null;
                                    foregroundColor = _initialForegroundColor;
                                    backgroundColor = _initialBackgroundColor;
                                }
                                else
                                {
                                    state = tokenStack.Peek();
                                }
                            }

                            if (!afterLastToken)
                            {
                                foregroundColor = state.ForegroundColor;
                                backgroundColor = state.BackgroundColor;

                                token = state.Tokens[++state.Index];
                            }
                        }

                        if (!afterLastToken && i == token.Extent.StartOffset)
                        {
                            GetTokenColors(token, out foregroundColor, out backgroundColor);

                            var stringToken = token as StringExpandableToken;
                            if (stringToken != null)
                            {
                                // We might have nested tokens.
                                if (stringToken.NestedTokens != null && stringToken.NestedTokens.Any())
                                {
                                    var tokens = new Token[stringToken.NestedTokens.Count + 1];
                                    stringToken.NestedTokens.CopyTo(tokens, 0);
                                    // NestedTokens doesn't have an "EOS" token, so we use
                                    // the string literal token for that purpose.
                                    tokens[tokens.Length - 1] = stringToken;

                                    tokenStack.Push(new SavedTokenState
                                    {
                                        Tokens = tokens,
                                        Index = 0,
                                        BackgroundColor = backgroundColor,
                                        ForegroundColor = foregroundColor
                                    });
                                }
                            }
                        }
                    }

                    if (text[i] == '\n')
                    {
                        while ((j % bufferWidth) != 0)
                        {
                            _consoleBuffer[j++] = _space;
                        }

                        for (int k = 0; k < Options.ContinuationPrompt.Length; k++, j++)
                        {
                            _consoleBuffer[j].UnicodeChar = Options.ContinuationPrompt[k];
                            _consoleBuffer[j].ForegroundColor = Options.ContinuationPromptForegroundColor;
                            _consoleBuffer[j].BackgroundColor = Options.ContinuationPromptBackgroundColor;
                        }
                    }
                    else
                    {
                        int size = LengthInBufferCells(text[i]);
                        totalBytes += size;

                        //if there is no enough space for the character at the edge, fill in spaces at the end and 
                        //put the character to next line.
                        int filling = totalBytes > bufferWidth ? (totalBytes - bufferWidth) % size : 0;
                        for (int f = 0; f < filling; f++)
                        {
                            _consoleBuffer[j++] = _space;
                            totalBytes++;
                        }

                        if (char.IsControl(text[i]))
                        {
                            _consoleBuffer[j].UnicodeChar = '^';
                            MaybeEmphasize(ref _consoleBuffer[j++], i, foregroundColor, backgroundColor);
                            _consoleBuffer[j].UnicodeChar = (char)('@' + text[i]);
                            MaybeEmphasize(ref _consoleBuffer[j++], i, foregroundColor, backgroundColor);

                        }
                        else if (size > 1 && IsCJKOutputCodePage() && _trueTypeInUse)
                        {
                            _consoleBuffer[j].UnicodeChar = text[i];
                            _consoleBuffer[j].Attributes = (ushort)((uint)_consoleBuffer[j].Attributes |
                                                           (uint)CHAR_INFO_Attributes.COMMON_LVB_LEADING_BYTE);
                            MaybeEmphasize(ref _consoleBuffer[j++], i, foregroundColor, backgroundColor);
                            _consoleBuffer[j].UnicodeChar = text[i];
                            _consoleBuffer[j].Attributes = (ushort)((uint)_consoleBuffer[j].Attributes |
                                                           (uint)CHAR_INFO_Attributes.COMMON_LVB_TRAILING_BYTE);
                            MaybeEmphasize(ref _consoleBuffer[j++], i, foregroundColor, backgroundColor);
                        }
                        else
                        {
                            _consoleBuffer[j].UnicodeChar = text[i];
                            MaybeEmphasize(ref _consoleBuffer[j++], i, foregroundColor, backgroundColor);
                        }
                    }
                }
            }
            finally
            {
                if (_hwnd != (IntPtr)0 && _hDC != (IntPtr)0)
                {
                    NativeMethods.ReleaseDC(_hwnd, _hDC);
                }
            }

            for (; j < (_consoleBuffer.Length - (statusLineCount * _bufferWidth)); j++)
            {
                _consoleBuffer[j] = _space;
            }

            if (_statusLinePrompt != null)
            {
                foregroundColor = _statusIsErrorMessage ? Options.ErrorForegroundColor : Console.ForegroundColor;
                backgroundColor = _statusIsErrorMessage ? Options.ErrorBackgroundColor : Console.BackgroundColor;

                for (int i = 0; i < _statusLinePrompt.Length; i++, j++)
                {
                    _consoleBuffer[j].UnicodeChar = _statusLinePrompt[i];
                    _consoleBuffer[j].ForegroundColor = foregroundColor;
                    _consoleBuffer[j].BackgroundColor = backgroundColor;
                }
                for (int i = 0; i < _statusBuffer.Length; i++, j++)
                {
                    _consoleBuffer[j].UnicodeChar = _statusBuffer[i];
                    _consoleBuffer[j].ForegroundColor = foregroundColor;
                    _consoleBuffer[j].BackgroundColor = backgroundColor;
                }

                for (; j < _consoleBuffer.Length; j++)
                {
                    _consoleBuffer[j] = _space;
                }
            }

            bool rendered = false;
            if (_parseErrors.Length > 0)
            {
                int promptChar = _initialX - 1 + (_bufferWidth * Options.ExtraPromptLineCount);

                while (promptChar >= 0)
                {
                    var c = (char)_consoleBuffer[promptChar].UnicodeChar;
                    if (char.IsWhiteSpace(c))
                    {
                        promptChar -= 1;
                        continue;
                    }

                    ConsoleColor prevColor = _consoleBuffer[promptChar].ForegroundColor;
                    _consoleBuffer[promptChar].ForegroundColor = ConsoleColor.Red;
                    WriteBufferLines(_consoleBuffer, ref _initialY);
                    rendered = true;
                    _consoleBuffer[promptChar].ForegroundColor = prevColor;
                    break;
                }
            }

            if (!rendered)
            {
                WriteBufferLines(_consoleBuffer, ref _initialY);
            }

            PlaceCursor();

            if ((_initialY + bufferLineCount) > (Console.WindowTop + Console.WindowHeight))
            {
                Console.WindowTop = _initialY + bufferLineCount - Console.WindowHeight;
            }

            _lastRenderTime.Restart();
        }

        private static void WriteBufferLines(CHAR_INFO[] buffer, ref int top)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            int bufferWidth = Console.BufferWidth;
            int bufferLineCount = buffer.Length / bufferWidth;
            if ((top + bufferLineCount) > Console.BufferHeight)
            {
                var scrollCount = (top + bufferLineCount) - Console.BufferHeight;
                ScrollBuffer(scrollCount);
                top -= scrollCount;
            }
            var bufferSize = new COORD
            {
                X = (short) bufferWidth,
                Y = (short) bufferLineCount
            };
            var bufferCoord = new COORD {X = 0, Y = 0};
            var bottom = top + bufferLineCount - 1;
            var writeRegion = new SMALL_RECT
            {
                Top = (short) top,
                Left = 0,
                Bottom = (short) bottom,
                Right = (short) (bufferWidth - 1)
            };
            NativeMethods.WriteConsoleOutput(handle, buffer,
                                             bufferSize, bufferCoord, ref writeRegion);

            // Now make sure the bottom line is visible
            if (bottom >= (Console.WindowTop + Console.WindowHeight))
            {
                Console.CursorTop = bottom;
            }
        }

        private static void WriteBlankLines(int count, int top)
        {
            var blanks = new CHAR_INFO[count * Console.BufferWidth];
            for (int i = 0; i < blanks.Length; i++)
            {
                blanks[i].BackgroundColor = Console.BackgroundColor;
                blanks[i].ForegroundColor = Console.ForegroundColor;
                blanks[i].UnicodeChar = ' ';
            }
            WriteBufferLines(blanks, ref top);
        }

        private static CHAR_INFO[] ReadBufferLines(int top, int count)
        {
            var result = new CHAR_INFO[Console.BufferWidth * count];
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var readBufferSize = new COORD {
                X = (short)Console.BufferWidth,
                Y = (short)count};
            var readBufferCoord = new COORD {X = 0, Y = 0};
            var readRegion = new SMALL_RECT
            {
                Top = (short)top,
                Left = 0,
                Bottom = (short)(top + count),
                Right = (short)(Console.BufferWidth - 1)
            };
            NativeMethods.ReadConsoleOutput(handle, result,
                readBufferSize, readBufferCoord, ref readRegion);
            return result;
        }

        private void GetTokenColors(Token token, out ConsoleColor foregroundColor, out ConsoleColor backgroundColor)
        {
            switch (token.Kind)
            {
            case TokenKind.Comment:
                foregroundColor = _options.CommentForegroundColor;
                backgroundColor = _options.CommentBackgroundColor;
                return;

            case TokenKind.Parameter:
                foregroundColor = _options.ParameterForegroundColor;
                backgroundColor = _options.ParameterBackgroundColor;
                return;

            case TokenKind.Variable:
            case TokenKind.SplattedVariable:
                foregroundColor = _options.VariableForegroundColor;
                backgroundColor = _options.VariableBackgroundColor;
                return;

            case TokenKind.StringExpandable:
            case TokenKind.StringLiteral:
            case TokenKind.HereStringExpandable:
            case TokenKind.HereStringLiteral:
                foregroundColor = _options.StringForegroundColor;
                backgroundColor = _options.StringBackgroundColor;
                return;

            case TokenKind.Number:
                foregroundColor = _options.NumberForegroundColor;
                backgroundColor = _options.NumberBackgroundColor;
                return;
            }

            if ((token.TokenFlags & TokenFlags.CommandName) != 0)
            {
                foregroundColor = _options.CommandForegroundColor;
                backgroundColor = _options.CommandBackgroundColor;
                return;
            }

            if ((token.TokenFlags & TokenFlags.Keyword) != 0)
            {
                foregroundColor = _options.KeywordForegroundColor;
                backgroundColor = _options.KeywordBackgroundColor;
                return;
            }

            if ((token.TokenFlags & (TokenFlags.BinaryOperator | TokenFlags.UnaryOperator | TokenFlags.AssignmentOperator)) != 0)
            {
                foregroundColor = _options.OperatorForegroundColor;
                backgroundColor = _options.OperatorBackgroundColor;
                return;
            }

            if ((token.TokenFlags & TokenFlags.TypeName) != 0)
            {
                foregroundColor = _options.TypeForegroundColor;
                backgroundColor = _options.TypeBackgroundColor;
                return;
            }

            if ((token.TokenFlags & TokenFlags.MemberName) != 0)
            {
                foregroundColor = _options.MemberForegroundColor;
                backgroundColor = _options.MemberBackgroundColor;
                return;
            }

            foregroundColor = _options.DefaultTokenForegroundColor;
            backgroundColor = _options.DefaultTokenBackgroundColor;
        }

        private void GetRegion(out int start, out int length)
        {
            if (_mark < _current)
            {
                start = _mark;
                length = _current - start;
            }
            else
            {
                start = _current;
                length = _mark - start;
            }
        }

        private bool InRegion(int i)
        {
            int start, end;
            if (_mark > _current)
            {
                start = _current;
                end = _mark;
            }
            else
            {
                start = _mark;
                end = _current;
            }
            return i >= start && i < end;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods",
            Justification = "Then the API we pass the handle to will return an error if it is invalid. They are not exposed.")]
        private static CONSOLE_FONT_INFO_EX GetConsoleFontInfo(ConsoleHandle consoleHandle)
        {

            CONSOLE_FONT_INFO_EX fontInfo = new CONSOLE_FONT_INFO_EX();
            fontInfo.cbSize = Marshal.SizeOf(fontInfo);
            bool result = NativeMethods.GetCurrentConsoleFontEx(consoleHandle.DangerousGetHandle(), false, ref fontInfo);

            if (result == false)
            {
                int err = Marshal.GetLastWin32Error();
                Win32Exception innerException = new Win32Exception(err);
                throw new Exception("Failed to get console font information.", innerException);
            }
            return fontInfo;
        }

        private void MaybeEmphasize(ref CHAR_INFO charInfo, int i, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            if (i >= _emphasisStart && i < (_emphasisStart + _emphasisLength))
            {
                backgroundColor = _options.EmphasisBackgroundColor;
                foregroundColor = _options.EmphasisForegroundColor;
            }
            else if (_visualSelectionCommandCount > 0 && InRegion(i))
            {
                // We can't quite emulate real console selection because it inverts
                // based on actual screen colors, our pallete is limited.  The choice
                // to invert only the lower 3 bits to change the color is somewhat
                // but looks best with the 2 default color schemes - starting PowerShell
                // from it's shortcut or from a cmd shortcut.
                foregroundColor = (ConsoleColor)((int)foregroundColor ^ 7);
                backgroundColor = (ConsoleColor)((int)backgroundColor ^ 7);
            }

            charInfo.ForegroundColor = foregroundColor;
            charInfo.BackgroundColor = backgroundColor;
        }

        private static void ScrollBuffer(int lines)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var scrollRectangle = new SMALL_RECT
            {
                Top = (short) lines,
                Left = 0,
                Bottom = (short) (Console.BufferHeight - 1),
                Right = (short)Console.BufferWidth
            };
            var destinationOrigin = new COORD {X = 0, Y = 0};
            var fillChar = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
            NativeMethods.ScrollConsoleScreenBuffer(handle, ref scrollRectangle, IntPtr.Zero, destinationOrigin, ref fillChar);
        }

        private void PlaceCursor(int x, ref int y)
        {
            int statusLineCount = GetStatusLineCount();
            if ((y + statusLineCount) >= Console.BufferHeight)
            {
                ScrollBuffer((y + statusLineCount) - Console.BufferHeight + 1);
                y = Console.BufferHeight - 1;
            }
            Console.SetCursorPosition(x, y);
        }

        private void PlaceCursor()
        {
            var coordinates = ConvertOffsetToCoordinates(_current);
            int y = coordinates.Y;
            PlaceCursor(coordinates.X, ref y);
        }

        private int LengthInBufferCells(char c)
        {
            int length = char.IsControl(c) ? 1 : 0;
            if (c < 256 || !IsAvailableFarEastCodePage())
            {
                return length + 1;
            }
            return length + LengthInBufferCellsFE(c);
        }

        private bool IsAnyDBCSCharSet(uint charSet)
        {
            const uint SHIFTJIS_CHARSET = 128;
            const uint HANGEUL_CHARSET = 129;
            const uint CHINESEBIG5_CHARSET = 136;
            const uint GB2312_CHARSET = 134;
            return charSet == SHIFTJIS_CHARSET || charSet == HANGEUL_CHARSET ||
                charSet == CHINESEBIG5_CHARSET || charSet == GB2312_CHARSET;
        }

        private uint CodePageToCharSet()
        {
            CHARSETINFO csi;
            const uint TCI_SRCCODEPAGE = 2;
            const uint OEM_CHARSET = 255;
            if (!NativeMethods.TranslateCharsetInfo((IntPtr)_codePage, out csi, TCI_SRCCODEPAGE))
            {
                csi.ciCharset = OEM_CHARSET;
            }
            return csi.ciCharset;
        }

        /// <summary>
        /// Check if the output buffer code page is Japanese, Simplified Chinese, Korean, or Traditional Chinese
        /// </summary>
        /// <returns>true if it is CJK code page; otherwise, false.</returns>
        private bool IsCJKOutputCodePage()
        {
            return _codePage == 932 || // Japanese
                _codePage == 936 || // Simplified Chinese
                _codePage == 949 || // Korean
                _codePage == 950;  // Traditional Chinese
        }

        private bool IsAvailableFarEastCodePage()
        {
            uint charSet = CodePageToCharSet();
            return IsAnyDBCSCharSet(charSet);
        }

        private int LengthInBufferCellsFE(char c)
        {
            if (0x20 <= c && c <= 0x7e)
            {
                /* ASCII */
                return 1;
            }
            else if (0x3041 <= c && c <= 0x3094)
            {
                /* Hiragana */
                return 2;
            }
            else if (0x30a1 <= c && c <= 0x30f6)
            {
                /* Katakana */
                return 2;
            }
            else if (0x3105 <= c && c <= 0x312c)
            {
                /* Bopomofo */
                return 2;
            }
            else if (0x3131 <= c && c <= 0x318e)
            {
                /* Hangul Elements */
                return 2;
            }
            else if (0xac00 <= c && c <= 0xd7a3)
            {
                /* Korean Hangul Syllables */
                return 2;
            }
            else if (0xff01 <= c && c <= 0xff5e)
            {
                /* Fullwidth ASCII variants */
                return 2;
            }
            else if (0xff61 <= c && c <= 0xff9f)
            {
                /* Halfwidth Katakana variants */
                return 1;
            }
            else if ((0xffa0 <= c && c <= 0xffbe) ||
                     (0xffc2 <= c && c <= 0xffc7) ||
                     (0xffca <= c && c <= 0xffcf) ||
                     (0xffd2 <= c && c <= 0xffd7) ||
                     (0xffda <= c && c <= 0xffdc))
            {
                /* Halfwidth Hangule variants */
                return 1;
            }
            else if (0xffe0 <= c && c <= 0xffe6)
            {
                /* Fullwidth symbol variants */
                return 2;
            }
            else if (0x4e00 <= c && c <= 0x9fa5)
            {
                /* Han Ideographic */
                return 2;
            }
            else if (0xf900 <= c && c <= 0xfa2d)
            {
                /* Han Compatibility Ideographs */
                return 2;
            }
            else
            {
                /* Unknown character: need to use GDI*/
                if (_hDC == (IntPtr)0)
                {
                    _hwnd = NativeMethods.GetConsoleWindow();
                    if ((IntPtr)0 == _hwnd)
                    {
                        return 1;
                    }
                    _hDC = NativeMethods.GetDC(_hwnd);
                    if ((IntPtr)0 == _hDC)
                    {
                        //Don't throw exception so that output can continue
                        return 1;
                    }
                }
                bool result = true;
                if (!_istmInitialized)
                {
                    result = NativeMethods.GetTextMetrics(_hDC, out _tm);
                    if (!result)
                    {
                        return 1;
                    }
                    _istmInitialized = true;
                }
                int width;
                result = NativeMethods.GetCharWidth32(_hDC, (uint)c, (uint)c, out width);
                if (!result)
                {
                    return 1;
                }
                if (width >= _tm.tmMaxCharWidth)
                {
                    return 2;
                }
            }
            return 1;
        }

        private COORD ConvertOffsetToCoordinates(int offset)
        {
            int x = _initialX;
            int y = _initialY + Options.ExtraPromptLineCount;

            int bufferWidth = Console.BufferWidth;
            var continuationPromptLength = Options.ContinuationPrompt.Length;

            for (int i = 0; i < offset; i++)
            {
                char c = _buffer[i];
                if (c == '\n')
                {
                    y += 1;
                    x = continuationPromptLength;
                }
                else
                {
                    int size = LengthInBufferCells(c);
                    x += size;
                    // Wrap?  No prompt when wrapping
                    if (x >= bufferWidth)
                    {
                        int offsize = x - bufferWidth;
                        if (offsize % size == 0)
                        {
                            x -= bufferWidth;
                        }
                        else
                        {
                            x = size;
                        }
                        y += 1;
                    }
                }
            }

            //if the next character has bigger size than the remain space on this line,
            //the cursor goes to next line where the next character is.
            if (_buffer.Length > offset)
            {
                int size = LengthInBufferCells(_buffer[offset]);
                // next one is Wrapped to next line
                if (x + size > bufferWidth && (x + size - bufferWidth) % size != 0)
                {
                    x = 0;
                    y++;
                }
            }
            
            return new COORD {X = (short)x, Y = (short)y};
        }

        private int ConvertOffsetToConsoleBufferOffset(int offset, int startIndex)
        {
            int j = startIndex;
            for (int i = 0; i < offset; i++)
            {
                char c = _buffer[i];
                if (_buffer[i] == '\n')
                {
                    for (int k = 0; k < Options.ContinuationPrompt.Length; k++)
                    {
                        j++;
                    }
                }
                else if (char.IsControl(_buffer[i]))
                {
                    j += 2;
                }
                else if (LengthInBufferCells(_buffer[i]) > 1 && IsCJKOutputCodePage() && _trueTypeInUse)
                {
                    j += 2;
                }
                else
                {
                    j++;
                }
            }
            return j;
        }

        private int ConvertLineAndColumnToOffset(COORD coord)
        {
            int offset;
            int x = _initialX;
            int y = _initialY + Options.ExtraPromptLineCount;

            int bufferWidth = Console.BufferWidth;
            var continuationPromptLength = Options.ContinuationPrompt.Length;
            for (offset = 0; offset < _buffer.Length; offset++)
            {
                // If we are on the correct line, return when we find
                // the correct column
                if (coord.Y == y && coord.X <= x)
                {
                    return offset;
                }
                char c = _buffer[offset];
                if (c == '\n')
                {
                    // If we are about to move off of the correct line,
                    // the line was shorter than the column we wanted so return.
                    if (coord.Y == y)
                    {
                        return offset;
                    }
                    y += 1;
                    x = continuationPromptLength;
                }
                else
                {
                    int size = LengthInBufferCells(c);
                    x += size;
                    // Wrap?  No prompt when wrapping
                    if (x >= bufferWidth)
                    {
                        int offsize = x - bufferWidth;
                        if (offsize % size == 0)
                        {
                            x -= bufferWidth;
                        }
                        else
                        {
                            x = size;
                        }
                        y += 1;
                    }
                }
            }

            // Return -1 if y is out of range, otherwise the last line was shorter
            // than we wanted, but still in range so just return the last offset.
            return (coord.Y == y) ? offset : -1;
        }

        private bool LineIsMultiLine()
        {
            for (int i = 0; i < _buffer.Length; i++)
            {
                if (_buffer[i] == '\n')
                    return true;
            }
            return false;
        }

        private int GetStatusLineCount()
        {
            if (_statusLinePrompt == null)
                return 0;

            return (_statusLinePrompt.Length + _statusBuffer.Length) / Console.BufferWidth + 1;
        }

        [ExcludeFromCodeCoverage]
        void IPSConsoleReadLineMockableMethods.Ding()
        {
            switch (Options.BellStyle)
            {
            case BellStyle.None:
                break;
            case BellStyle.Audible:
                Console.Beep(Options.DingTone, Options.DingDuration);
                break;
            case BellStyle.Visual:
                // TODO: flash prompt? command line?
                break;
            }
        }

        /// <summary>
        /// Notify the user based on their preference for notification.
        /// </summary>
        public static void Ding()
        {
            _singleton._mockableMethods.Ding();
        }

        private bool PromptYesOrNo(string s)
        {
            _statusLinePrompt = s;
            Render();

            var key = ReadKey();

            _statusLinePrompt = null;
            Render();
            return key.Key == ConsoleKey.Y;
        }

        #region Screen scrolling

        /// <summary>
        /// Scroll the display up one screen.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public static void ScrollDisplayUp(ConsoleKeyInfo? key = null, object arg = null)
        {
            int numericArg;
            TryGetArgAsInt(arg, out numericArg, +1);
            var newTop = Console.WindowTop - (numericArg * Console.WindowHeight);
            if (newTop < 0)
            {
                newTop = 0;
            }
            Console.SetWindowPosition(0, newTop);
        }

        /// <summary>
        /// Scroll the display up one line.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public static void ScrollDisplayUpLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            int numericArg;
            TryGetArgAsInt(arg, out numericArg, +1);
            var newTop = Console.WindowTop - numericArg;
            if (newTop < 0)
            {
                newTop = 0;
            }
            Console.SetWindowPosition(0, newTop);
        }

        /// <summary>
        /// Scroll the display down one screen.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public static void ScrollDisplayDown(ConsoleKeyInfo? key = null, object arg = null)
        {
            int numericArg;
            TryGetArgAsInt(arg, out numericArg, +1);
            var newTop = Console.WindowTop + (numericArg * Console.WindowHeight);
            if (newTop > (Console.BufferHeight - Console.WindowHeight))
            {
                newTop = (Console.BufferHeight - Console.WindowHeight);
            }
            Console.SetWindowPosition(0, newTop);
        }

        /// <summary>
        /// Scroll the display down one line.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public static void ScrollDisplayDownLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            int numericArg;
            TryGetArgAsInt(arg, out numericArg, +1);
            var newTop = Console.WindowTop + numericArg;
            if (newTop > (Console.BufferHeight - Console.WindowHeight))
            {
                newTop = (Console.BufferHeight - Console.WindowHeight);
            }
            Console.SetWindowPosition(0, newTop);
        }

        /// <summary>
        /// Scroll the display to the top.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public static void ScrollDisplayTop(ConsoleKeyInfo? key = null, object arg = null)
        {
            Console.SetWindowPosition(0, 0);
        }

        /// <summary>
        /// Scroll the display to the cursor.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public static void ScrollDisplayToCursor(ConsoleKeyInfo? key = null, object arg = null)
        {
            // Ideally, we'll put the last input line at the bottom of the window
            var coordinates = _singleton.ConvertOffsetToCoordinates(_singleton._buffer.Length);

            var newTop = coordinates.Y - Console.WindowHeight + 1;

            // But if the cursor won't be visible, make sure it is.
            if (newTop > Console.CursorTop)
            {
                // Add 10 for some extra context instead of putting the
                // cursor on the bottom line.
                newTop = Console.CursorTop - Console.WindowHeight + 10;
            }

            // But we can't go past the end of the buffer.
            if (newTop > (Console.BufferHeight - Console.WindowHeight))
            {
                newTop = (Console.BufferHeight - Console.WindowHeight);
            }
            Console.SetWindowPosition(0, newTop);
        }

        #endregion Screen scrolling
    }
}
