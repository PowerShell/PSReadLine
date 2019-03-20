using System;
using System.Linq;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        // Disabled because the test is not portable without some mocking that
        // probably not worth the effort.
        //[Fact]
        private void CaptureScreen()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+z", PSConsoleReadLine.CaptureScreen));

            var line  = new [] {"echo alpha", "echo beta", "echo phi", "echo rho"};
            Test(line[0], Keys(line[0], _.Ctrl_z, _.Enter, _.Enter));
            AssertScreenCaptureClipboardIs(line[0]);

            var cancelKeys = new[] {_.Escape, _.Ctrl_c, _.Ctrl_g};
            for (int i = 0; i < cancelKeys.Length; i++)
            {
                // Start CaptureScreen but cancel
                Test(line[i + 1], Keys(line[i + 1], _.Ctrl_z, cancelKeys[i], _.Enter), resetCursor: false);
                // Make sure the clipboard doesn't change
                AssertScreenCaptureClipboardIs(line[0]);
            }

            // Make sure we know where we are on the screen.
            AssertCursorTopIs(4);

            var shiftUpArrow = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, true, false, false);
            var shiftDownArrow = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, true, false, false);

            Test("", Keys(
                // Basic up/down arrows
                _.Ctrl_z, _.UpArrow, _.Enter, CheckThat(() => AssertScreenCaptureClipboardIs(line[3])),
                _.Ctrl_z, _.UpArrow, _.UpArrow, _.Enter, CheckThat(() => AssertScreenCaptureClipboardIs(line[2])),
                _.Ctrl_z, _.UpArrow, _.UpArrow, _.DownArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[3])),

                // Select multiple lines
                _.Ctrl_z, _.UpArrow, shiftUpArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[2], line[3])),
                _.Ctrl_z, Enumerable.Repeat(_.UpArrow, 10), shiftDownArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[0], line[1])),

                // Select multiple lines, then shorten selection
                _.Ctrl_z, _.UpArrow, shiftUpArrow, shiftUpArrow, shiftDownArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[2], line[3])),
                _.Ctrl_z, Enumerable.Repeat(_.UpArrow, 10), shiftDownArrow, shiftDownArrow, shiftUpArrow, _.Enter,
                CheckThat(() => AssertScreenCaptureClipboardIs(line[0], line[1])),

                // Test trying to arrow down past end of buffer (arrowing past top of buffer covered above)
                _.Ctrl_z, Enumerable.Repeat(_.DownArrow, _console.BufferHeight), _.Escape),
                resetCursor: false);

            // Test that we ding input that doesn't do anything
            TestMustDing("", Keys(_.Ctrl_z, 'c', _.Escape));
            TestMustDing("", Keys(_.Ctrl_z, 'a', _.Escape));
            TestMustDing("", Keys(_.Ctrl_z, 'z', _.Escape));

            // To test:
            // * Selected lines are inverted
            // * Rtf output
            // * Rtf special characters
        }
    }
}
