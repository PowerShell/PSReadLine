// Copyright (c) Microsoft Corporation.
// Licensed under the 2-Clause BSD License.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public class ControlCharacterTests
    {
        [DllImport("user32.dll")]
        private static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[] layouts);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint threadId);

        [DllImport("user32.dll")]
        private static extern IntPtr ActivateKeyboardLayout(IntPtr layout, uint flags);

        public static IEnumerable<object[]> ControlLetters()
        {
            for (int code = 1; code <= 26; code++)
            {
                yield return new object[] { code, false };
                yield return new object[] { code, true };
            }
        }

        // Use only an already installed layout, on a disposable worker thread.
        // No foreground-window messages or global input-language changes.
        private static void WithRussianLayout(Action action)
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Requires Windows keyboard layouts.");
            var layouts = new IntPtr[GetKeyboardLayoutList(0, null)];
            GetKeyboardLayoutList(layouts.Length, layouts);
            var russian = Array.Find(layouts, p => (p.ToInt64() & 0xffff) == 0x0419);
            Skip.If(russian == IntPtr.Zero, "Russian keyboard layout is not installed.");
            Exception failure = null;
            var worker = new Thread(() =>
            {
                var original = GetKeyboardLayout(0);
                try
                {
                    Assert.NotEqual(IntPtr.Zero, ActivateKeyboardLayout(russian, 0));
                    // A terminal-owner thread may override the current thread's layout.
                    Skip.If((PlatformWindows.GetConsoleKeyboardLayout().ToInt64() & 0xffff) != 0x0419,
                        "PSReadLine selects another terminal thread's layout.");
                    action();
                }
                catch (Exception ex) { failure = ex; }
                finally { ActivateKeyboardLayout(original, 0); }
            });
            worker.Start();
            worker.Join();
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        [Theory]
        [MemberData(nameof(ControlLetters))]
        public void ControlCharactersRetainTheirMeaning(int code, bool shift)
        {
            var input = new ConsoleKeyInfo((char)code, (ConsoleKey)('A' + code - 1), shift, false, true);
            var key = PSKeyInfo.FromConsoleKeyInfo(input);
            Assert.Equal("Ctrl+" + (char)((shift ? 'A' : 'a') + code - 1), key.ToString());
            var roundTrip = key.AsConsoleKeyInfo();
            Assert.Equal(input.KeyChar, roundTrip.KeyChar);
            Assert.Equal(input.Key, roundTrip.Key);
            Assert.Equal(input.Modifiers, roundTrip.Modifiers);
        }

        [SkippableTheory]
        [InlineData(3, false)]
        [InlineData(3, true)]
        [InlineData(18, false)]
        [InlineData(18, true)]
        [InlineData(22, false)]
        [InlineData(22, true)]
        public void ControlCharactersRetainTheirMeaningWithRussianLayout(int code, bool shift)
        {
            WithRussianLayout(() => ControlCharactersRetainTheirMeaning(code, shift));
        }

        [Theory]
        [InlineData('м', ConsoleKey.V, false, false, "м")]
        [InlineData('м', ConsoleKey.V, true, false, "Ctrl+м")]
        [InlineData('@', ConsoleKey.D2, true, true, "@")]
        [InlineData('ó', ConsoleKey.O, true, true, "ó")]
        public void PrintableCharactersKeepTheirMeaning(char character, ConsoleKey key, bool control, bool alt, string expected)
        {
            Assert.Equal(expected, PSKeyInfo.FromConsoleKeyInfo(
                new ConsoleKeyInfo(character, key, false, alt, control)).ToString());
        }

        [Theory]
        [InlineData('\t', ConsoleKey.Tab, "Ctrl+Tab")]
        [InlineData('\r', ConsoleKey.Enter, "Ctrl+Enter")]
        [InlineData('\b', ConsoleKey.Backspace, "Ctrl+Backspace")]
        [InlineData('\x1b', ConsoleKey.Escape, "Ctrl+Escape")]
        public void NamedKeysKeepTheirMeaning(char character, ConsoleKey key, string expected)
        {
            Assert.Equal(expected, PSKeyInfo.FromConsoleKeyInfo(
                new ConsoleKeyInfo(character, key, false, false, true)).ToString());
        }

        [SkippableFact]
        public void ControlAltAndPrintableInputKeepLayoutDependentMeaning()
        {
            WithRussianLayout(() =>
            {
                Assert.Equal("Ctrl+Alt+м", PSKeyInfo.FromConsoleKeyInfo(
                    new ConsoleKeyInfo('\x16', ConsoleKey.V, false, true, true)).ToString());
                Assert.Equal("м", PSKeyInfo.FromConsoleKeyInfo(
                    new ConsoleKeyInfo('м', ConsoleKey.V, false, false, false)).ToString());
                Assert.Equal("@", PSKeyInfo.FromConsoleKeyInfo(
                    new ConsoleKeyInfo('@', ConsoleKey.D2, false, true, true)).ToString());
                // A missing character, a dead-key-shaped input, and a control character
                // without the Control modifier must still follow the existing path.
                Assert.Equal("Ctrl+к", PSKeyInfo.FromConsoleKeyInfo(
                    new ConsoleKeyInfo('\0', ConsoleKey.R, false, false, true)).ToString());
                Assert.Equal("\0", PSKeyInfo.FromConsoleKeyInfo(
                    new ConsoleKeyInfo('\0', ConsoleKey.Oem7, false, false, false)).ToString());
                Assert.Equal("м", PSKeyInfo.FromConsoleKeyInfo(
                    new ConsoleKeyInfo('\x16', ConsoleKey.V, false, false, false)).ToString());
            });
        }
    }
}
