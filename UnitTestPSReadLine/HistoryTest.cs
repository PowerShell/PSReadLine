using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestHistory()
        {
            TestSetup(KeyMode.Cmd);

            // No history
            Test("", Keys(_.UpArrow, _.DownArrow));

            PSConsoleReadLine.AddToHistory("dir c*");
            PSConsoleReadLine.AddToHistory("ps p*");

            Test("dir c*", Keys(_.UpArrow, _.UpArrow));
            Test("dir c*", Keys(_.UpArrow, _.UpArrow, _.DownArrow));
        }

        [TestMethod]
        public void TestSearchHistory()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            // No history
            Test("", Keys(_.UpArrow, _.DownArrow));

            // Clear history in case the above added some history (but it shouldn't)
            PSConsoleReadLine.ClearHistory();

            Test(" ", Keys(' ', _.UpArrow, _.DownArrow));

            PSConsoleReadLine.AddToHistory("dir c*");
            PSConsoleReadLine.AddToHistory("ps p*");
            PSConsoleReadLine.AddToHistory("dir cd*");

            Test("dir c*", Keys(
                "d",
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(1)),
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(1)),
                _.DownArrow,
                CheckThat(() => AssertCursorLeftIs(1)),
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(1))));

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistorySearchCursorMovesToEnd = true});
            Test("dir cd*", Keys(
                "d",
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(6)),
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(7)),
                _.DownArrow,
                CheckThat(() => AssertCursorLeftIs(6)),
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(7))));
        }

        [TestMethod]
        public void TestInteractiveHistorySearch()
        {
            TestSetup(KeyMode.Emacs);

            PSConsoleReadLine.AddToHistory("echo aaa");
            Test("echo aaa", Keys(_.CtrlR, 'a'));
        }

        [TestMethod]
        public void TestAddToHistoryHandler()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {AddToHistoryHandler = s => s.StartsWith("z")});

            Test("zzzz", Keys("zzzz"));
            Test("azzz", Keys("azzz"));
            Test("zzzz", Keys(_.UpArrow));
        }

        [TestMethod]
        public void TestHistoryDuplicates()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistoryNoDuplicates = false});

            Test("zzzz", Keys("zzzz"));
            Test("aaaa", Keys("aaaa"));
            Test("bbbb", Keys("bbbb"));
            Test("bbbb", Keys("bbbb"));
            Test("cccc", Keys("cccc"));
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 4)));

            // Changing the option should affect existing history.
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistoryNoDuplicates = true});
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 3)));

            PSConsoleReadLine.ClearHistory();
            Test("aaaa", Keys("aaaa"));
            Test("bbbb", Keys("bbbb"));
            Test("bbbb", Keys("bbbb"));
            Test("cccc", Keys("cccc"));
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 3)));
        }

        [TestMethod]
        public void TestHistoryCount()
        {
            TestSetup(KeyMode.Cmd);

            Test("zzzz", Keys("zzzz"));
            Test("aaaa", Keys("aaaa"));
            Test("bbbb", Keys("bbbb"));
            Test("cccc", Keys("cccc"));

            // There should be 4 items in history, the following should remove the
            // oldest history item.
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {MaximumHistoryCount = 3});
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 4)));

            Test("zzzz", Keys("zzzz"));
            Test("aaaa", Keys("aaaa"));
            Test("bbbb", Keys("bbbb"));
            Test("cccc", Keys("cccc"));
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 4)));
        }

    }
}
