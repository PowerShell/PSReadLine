using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestShellNextWord()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+RightArrow", PSConsoleReadLine.ShellNextWord));

            Test("aaa  bbb  ccc", Keys(
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(0)),
                "aaa  bbb  ccc",
                _.Home,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(5)),
                _.LeftArrow,
                _.LeftArrow,
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(5)),
                _.LeftArrow,
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(5)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(10)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(13))));

            Test("echo \"a $b c $d e\" 42", Keys(
                "echo \"a $b c $d e\" 42",
                _.Home,
                Enumerable.Repeat(_.RightArrow, 5),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(8)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(13)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(19))));
        }

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
        }

        [TestMethod]
        public void TestEmacsShellForwardWord()
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

            input = "echo \"a $b c $d e\" 42";
            Test(input, Keys(
                input, _.Home,
                Enumerable.Repeat(_.RightArrow, 5),
                _.AltF, CheckThat(() => AssertCursorLeftIs(10)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(15)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(18))));
        }

    }
}
