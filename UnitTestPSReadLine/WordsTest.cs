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
        public void TestBackwardWord()
        {
            TestSetup(KeyMode.Cmd);

            const string input = "  aaa  bbb  ccc  ";
            Test(input, Keys(
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(12)),
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(7)),
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(2)),
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(0))));

            // Test with digit arguments
            Test(input, Keys(
                _.Alt3, _.Alt3, _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.Alt2, _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(7)),
                _.Alt1, _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(2)),
                _.Alt0, _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(2)),
                _.AltMinus, _.Alt1, _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(7)),
                _.Alt2, _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(0))));
        }

        [TestMethod]
        public void TestEmacsBackwardWord()
        {
            TestSetup(KeyMode.Emacs);

            const string input = "  aaa  bbb  ccc  ";
            Test(input, Keys(
                _.AltB, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.AltB, CheckThat(() => AssertCursorLeftIs(12)),
                _.AltB, CheckThat(() => AssertCursorLeftIs(7)),
                _.AltB, CheckThat(() => AssertCursorLeftIs(2)),
                _.AltB, CheckThat(() => AssertCursorLeftIs(0))));

            // Test with digit arguments
            Test(input, Keys(
                _.Alt2, _.AltB, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.Alt1, _.AltB, CheckThat(() => AssertCursorLeftIs(12)),
                _.Alt2, _.AltB, CheckThat(() => AssertCursorLeftIs(2)),
                _.AltMinus, _.Alt2, _.AltB, CheckThat(() => AssertCursorLeftIs(10)),
                _.Alt4, _.AltB, CheckThat(() => AssertCursorLeftIs(0))));
        }

        [TestMethod]
        public void TestForwardWord()
        {
            TestSetup(KeyMode.Emacs);

            Test("", Keys(_.AltF));

            var input = "echo   abc  def  ghi  jkl";
            Test(input, Keys(
                 input, _.Home, _.AltF,
                 CheckThat(() => AssertCursorLeftIs(4)),
                 _.RightArrow, _.AltF,
                 CheckThat(() => AssertCursorLeftIs(10))
                ));

            // Test with digit arguments
            Test(input, Keys(
                input, _.Home,
                _.Alt4, _.AltF, CheckThat(() => AssertCursorLeftIs(20)),
                _.Alt3, _.AltMinus, _.AltF, CheckThat(() => AssertCursorLeftIs(7))));
        }

        [TestMethod]
        public void TestShellBackwardWord()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+LeftArrow", PSConsoleReadLine.ShellBackwardWord));

            Test("", Keys(_.CtrlLeftArrow));

            var input = "echo a\\b[c]:dd \"a $b c $d e\" 42";
            Test(input, Keys(
                input,
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(29)),
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(15)),
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(5))));

            // Test with digit arguments
            Test(input, Keys(
                input,
                _.Alt3, _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.Alt1, _.AltMinus, _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(15))));
        }

        [TestMethod]
        public void TestShellNextWord()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+RightArrow", PSConsoleReadLine.ShellNextWord));

            Test("aaa  bbb  ccc", Keys(
                _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(0)),
                "aaa  bbb  ccc",
                _.Home, CheckThat(() => AssertCursorLeftIs(0)),
                _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.LeftArrow, _.LeftArrow, _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.LeftArrow, _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(10)),
                _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(13))));

            var input = "echo a\\b[c]:dd \"a $b c $d e\" 42";
            Test(input, Keys(
                input, _.Home, Enumerable.Repeat(_.RightArrow, 5),
                _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(15)),
                _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(18)),
                _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(23))));

            // Test with digit arguments
            Test(input, Keys(
                input, _.Home,
                _.Alt3, _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(18)),
                _.Home,
                _.Alt4, _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(23)),
                _.AltMinus, _.Alt3, _.CtrlRightArrow, CheckThat(() => AssertCursorLeftIs(5))
                ));
        }

        [TestMethod]
        public void TestShellForwardWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+F", PSConsoleReadLine.ShellForwardWord));

            string input = "aaa  bbb  ccc";
            Test(input, Keys(
                _.AltF, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.CtrlA, CheckThat(() => AssertCursorLeftIs(0)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(3)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(8)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(13))));

            input = "echo a\\b[c]:dd \"a $b c $d e\" 42";
            Test(input, Keys(
                input, _.Home,
                Enumerable.Repeat(_.RightArrow, 5),
                _.AltF, CheckThat(() => AssertCursorLeftIs(14)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(28)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(31))));

            // Test with digit arguments
            Test(input, Keys(
                input, _.Home,
                _.Alt2, _.AltF, CheckThat(() => AssertCursorLeftIs(14)),
                _.RightArrow, _.Alt2, _.AltF, CheckThat(() => AssertCursorLeftIs(25)),
                _.AltMinus, _.Alt4, _.AltF, CheckThat(() => AssertCursorLeftIs(5))));
        }
    }
}
