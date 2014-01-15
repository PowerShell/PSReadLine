using System.Collections.Generic;
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
        public void TestYankPop()
        {
            TestSetup(KeyMode.Emacs);

            var killedText = new List<string>();

            Test("z", Keys(_.CtrlY, _.AltY, _.Z));

            // Fill the kill ring plus some extra.
            for (int i = 0; i < PSConsoleReadlineOptions.DefaultMaximumKillRingCount + 2; i++)
            {
                var c = (char)('a' + i);
                killedText.Add(c + "zz");
                Test("", Keys(c, "zz", _.CtrlU));
            }

            int killRingIndex = killedText.Count - 1;
            Test(killedText[killRingIndex], Keys(_.CtrlY));

            Test(killedText[killRingIndex] + killedText[killRingIndex],
                Keys(_.CtrlY, _.CtrlY));

            killRingIndex -= 1;
            Test(killedText[killRingIndex], Keys(_.CtrlY, _.AltY));

            // Test wrap around.  We need 1 yank and n-1 yankpop to wrap around once, plus enter.
            Test(killedText[killRingIndex],
                Keys(_.CtrlY, Enumerable.Repeat(_.AltY, PSConsoleReadlineOptions.DefaultMaximumKillRingCount)));

            // Make sure an empty kill doesn't end up in the kill ring
            Test("a", Keys("a", _.CtrlU, _.CtrlU, "b", _.CtrlU, _.CtrlY, _.AltY));
        }

        [TestMethod]
        public void TestKillLine()
        {
            TestSetup(KeyMode.Emacs);

            // Kill whole line
            Test("", Keys("dir", _.CtrlA, _.CtrlK));
            Test("dir", Keys(_.CtrlY));

            // Kill partial line
            Test("dir ", Keys("dir foo", _.AltB, _.CtrlK));
            Test("foo", Keys(_.CtrlY));
        }

        [TestMethod]
        public void TestBackwardKillLine()
        {
            TestSetup(KeyMode.Emacs);

            // Kill whole line
            // Check killed text by yanking
            Test("ls", Keys("dir", _.CtrlU, "ls"));
            Test("dir", Keys(_.CtrlY));

            // Kill whole line with second key binding
            Test("def", Keys("abc", _.CtrlX, _.Backspace, "def"));
            Test("abc", Keys(_.CtrlY));

            // Kill partial line
            Test("foo", Keys("dir foo", _.AltB, _.CtrlU));
            Test("dir ", Keys(_.CtrlY));
        }

        [TestMethod]
        public void TestKillAppend()
        {
            TestSetup(KeyMode.Emacs);

            Test(" abcdir", Keys(
                " abcdir", _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.CtrlK, // Kill 'dir'
                _.CtrlU, // Kill append ' abc'
                _.CtrlY)); // Yank ' abcdir'

            // Test empty kill doesn't affect kill append
            Test("ab", Keys("ab", _.LeftArrow, _.CtrlK, _.CtrlK, _.CtrlU, _.CtrlY));
        }

        [TestMethod]
        public void TestShellKillWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+D", PSConsoleReadLine.ShellKillWord));

            Test("echo  defabc", Keys(
                _.AltD, // Test on empty input
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 7),
                _.AltD, // Kill 'abc'
                _.End, _.CtrlY)); // Yank 'abc' at end of line
        }

        [TestMethod]
        public void TestShellBackwardKillWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+Backspace", PSConsoleReadLine.ShellBackwardKillWord));

            Test("echo defabc ", Keys(
                _.AltBackspace, // Test on empty line
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 3),
                _.AltBackspace,    // Kill 'abc '
                _.End, _.CtrlY));  // Yank 'abc ' at the end
        }

        [TestMethod]
        public void TestExchangePointAndMark()
        {
            TestSetup(KeyMode.Emacs,
                      new KeyHandler("Ctrl+Z", PSConsoleReadLine.ExchangePointAndMark));

            var exchangePointAndMark = _.CtrlZ;
            var setMark = _.CtrlAt;

            Test("abcde", Keys(
                "abcde",
                exchangePointAndMark,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.RightArrow,
                setMark,
                _.RightArrow,
                _.RightArrow,
                CheckThat(() => AssertCursorLeftIs(3)),
                exchangePointAndMark,
                CheckThat(() => AssertCursorLeftIs(1))
                ));

            Test("abc", Keys(
                "abc",
                exchangePointAndMark,
                CheckThat(() => AssertCursorLeftIs(0))
                ));
        }
    }
}
