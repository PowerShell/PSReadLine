using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {

        [TestMethod]
        public void TestCaptureScreen()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+Z", PSConsoleReadLine.CaptureScreen));

            var line  = new [] {"echo alpha", "echo beta", "echo phi", "echo rho"};
            Test(line[0], Keys(line[0], _.CtrlZ, _.Enter, _.Enter));
            AssertScreenCaptureClipboardIs(line[0]);

            var cancelKeys = new[] {_.Escape, _.CtrlC, _.CtrlG};
            for (int i = 0; i < cancelKeys.Length; i++)
            {
                // Start CaptureScreen but cancel
                Test(line[i + 1], Keys(line[i + 1], _.CtrlZ, cancelKeys[i], _.Enter), resetCursor: false);
                // Make sure the clipboard doesn't change
                AssertScreenCaptureClipboardIs(line[0]);
            }

            // Make sure we know where we are on the screen.
            AssertCursorTopIs(4);

            var shiftUpArrow = new ConsoleKeyInfo((char)0, ConsoleKey.UpArrow, true, false, false);
            var shiftDownArrow = new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, true, false, false);

            Test("", Keys(
                // Basic up/down arrows
                _.CtrlZ, _.UpArrow, _.Enter, CheckThat(() => AssertScreenCaptureClipboardIs(line[3])),
                _.CtrlZ, _.UpArrow, _.UpArrow, _.Enter, CheckThat(() => AssertScreenCaptureClipboardIs(line[2])),
                _.CtrlZ, _.UpArrow, _.UpArrow, _.DownArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[3])),

                // Select multiple lines
                _.CtrlZ, _.UpArrow, shiftUpArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[2], line[3])),
                _.CtrlZ, Enumerable.Repeat(_.UpArrow, 10), shiftDownArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[0], line[1])),

                // Select multiple lines, then shorten selection
                _.CtrlZ, _.UpArrow, shiftUpArrow, shiftUpArrow, shiftDownArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[2], line[3])),
                _.CtrlZ, Enumerable.Repeat(_.UpArrow, 10), shiftDownArrow, shiftDownArrow, shiftUpArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[0], line[1])),

                // Test trying to arrow down past end of buffer (arrowing past top of buffer covered above)
                _.CtrlZ, Enumerable.Repeat(_.DownArrow, Console.BufferHeight), _.Escape),
                resetCursor: false);

            // Test that we ding input that doesn't do anything
            TestMustDing("", Keys(_.CtrlZ, 'c', _.Escape));
            TestMustDing("", Keys(_.CtrlZ, 'a', _.Escape));
            TestMustDing("", Keys(_.CtrlZ, 'z', _.Escape));

            // To test:
            // * Selected lines are inverted
            // * Rtf output
            // * Rtf special characters
        }
    }
}
