using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        private void SetHistory(params string[] historyItems)
        {
            PSConsoleReadLine.ClearHistory();
            foreach (var item in historyItems)
            {
                PSConsoleReadLine.AddToHistory(item);
            }
        }

        [TestMethod]
        public void TestHistory()
        {
            TestSetup(KeyMode.Cmd);

            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));

            SetHistory("dir c*", "ps p*");

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
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));

            // Clear history in case the above added some history (but it shouldn't)
            SetHistory();
            Test(" ", Keys(' ', _.UpArrow, _.DownArrow));

            SetHistory("dosomething", "ps p*", "dir", "echo zzz");
            Test("dosomething", Keys(
                "d",
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1, TokenClassification.Command, "dir");
                    AssertCursorLeftIs(1);
                }),
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1, TokenClassification.Command, "dosomething");
                    AssertCursorLeftIs(1);
                }),
                _.DownArrow, CheckThat(() => {
                    AssertScreenIs(1, TokenClassification.Command, "dir");
                    AssertCursorLeftIs(1);
                }),
                _.UpArrow,   CheckThat(() =>
                {
                    AssertScreenIs(1, TokenClassification.Command, "dosomething");
                    AssertCursorLeftIs(1);
                })));
        }

        [TestMethod]
        public void TestHistorySearchCursorMovesToEnd()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistorySearchCursorMovesToEnd = true});

            SetHistory("dosomething", "ps p*", "dir", "echo zzz");
            Test("dosomething", Keys(
                "d",
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1, TokenClassification.Command, "dir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1, TokenClassification.Command, "dosomething");
                    AssertCursorLeftIs(11);
                }),
                _.DownArrow, CheckThat(() => {
                    AssertScreenIs(1, TokenClassification.Command, "dir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() =>
                {
                    AssertScreenIs(1, TokenClassification.Command, "dosomething");
                    AssertCursorLeftIs(11);
                })));
        }

        [TestMethod]
        public void TestBeginningOfHistory()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory("echo first", "echo second", "echo third");
            Test("echo first", Keys(_.AltLess));

            SetHistory("echo first", "echo second", "echo third");
            Test("echo second", Keys(_.AltLess, _.DownArrow));
        }

        [TestMethod]
        public void TestEndOfHistory()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory("echo first", "echo second", "echo third");
            Test("", Keys(_.UpArrow, _.AltGreater));

            // Make sure end of history restores the "current" line if
            // there was anything entered before going through history
            Test("abc", Keys("abc", _.UpArrow, _.AltGreater));

            // Make sure we don't recall the previous "current" line
            // after we accepted it.
            Test("", Keys(_.AltGreater));
        }

        [TestMethod]
        public void TestInteractiveHistorySearch()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory("echo aaa");
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
