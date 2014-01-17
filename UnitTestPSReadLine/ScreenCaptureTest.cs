using System;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

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

            var line = "echo foo";
            Test(line, Keys( line, _.CtrlZ, _.Enter, _.Enter));

            Assert.IsTrue(Clipboard.ContainsText());
            var fromClipboard = Clipboard.GetText();
            Assert.AreEqual(line + Environment.NewLine, fromClipboard);

            // To test:
            // * UpArrow, DownArrow
            // * Shift+UpArrow, Shift+DownArrow
            // * Ctrl+C, Ctrl+G
            // * Escape
            // * Any other key
            // * Top of buffer
            // * Bottom of buffer
            // * Selected lines are inverted
            // * Rtf output
            // * Rtf special characters
        }
    }
}
