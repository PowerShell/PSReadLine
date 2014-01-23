using System;
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

            var options = PSConsoleReadLine.GetOptions();
            var emphasisColors = Tuple.Create(options.EmphasisForegroundColor, options.EmphasisBackgroundColor);
            var statusColors = Tuple.Create(Console.ForegroundColor, Console.BackgroundColor);

            // Test entering multiple characters and the line is updated with new matches
            SetHistory("zz1", "echo abc", "zz2", "echo abb", "zz3", "echo aaa", "zz4");
            Test("echo abc", Keys(_.CtrlR,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "aa",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                'b', CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "ab",
                    TokenClassification.None, 'b',
                    NextLine,
                    statusColors, "bck-i-search: ab_")),
                'c', CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "abc",
                    NextLine,
                    statusColors, "bck-i-search: abc_"))));

            // Test repeated Ctrl+R goes back through multiple matches
            SetHistory("zz1", "echo abc", "zz2", "echo abb", "zz3", "echo aaa", "zz4");
            Test("echo abc", Keys(_.CtrlR,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "aa",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.CtrlR, CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bb",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.CtrlR, CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_"))));

            // Test that the current match doesn't change when typing
            // additional characters, only emphasis should change.
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo abc", Keys(_.CtrlR,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                'b',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "ab",
                    TokenClassification.None, 'c',
                    NextLine,
                    statusColors, "bck-i-search: ab_"))));

            // Test that abort restores line state before Ctrl+R
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo zed", Keys("echo zed", _.CtrlR,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.CtrlG,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    TokenClassification.None, "zed",
                    NextLine))));

            // Test that a random function terminates the search and has an
            // effect on the line found in history
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo zed", Keys(_.CtrlR,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.AltD, "zed"));

            // Test that Escape terminates the search leaving the
            // cursor at the point in the match.
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo yabc", Keys(_.CtrlR,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Escape, "y"));

            // Test entering multiple characters, then backspace, make sure we restore
            // the correct line
            SetHistory("zz1", "echo abc", "zz2", "echo abb", "zz3", "echo aaa", "zz4");
            Test("echo aaa", Keys(_.CtrlR,
                _.Backspace,  // Try backspace on empty search string
                "ab", CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "ab",
                    TokenClassification.None, 'b',
                    NextLine,
                    statusColors, "bck-i-search: ab_")),
                _.Backspace,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "aa",
                    NextLine,
                    statusColors, "bck-i-search: a_"))));

            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("", Keys(_.CtrlR,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.CtrlR,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bzz",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.CtrlR,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    TokenClassification.None, "abzz",
                    NextLine,
                    statusColors, "failed-bck-i-search: a_")),
                _.CtrlS,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bzz",
                    NextLine,
                    statusColors, "fwd-i-search: a_")),
                _.CtrlG));

            // To test:
            // * Type multiple characters and
            //    - search always succeeds
            //    - search fails
            // * Backspace after failed search, back to success
            // * Backspace after success but no change to item found
            // * Backspace after success but change to item found
            // * Finish with:
            //    - Escape
            //    - Something else like cursor movement, make sure it applies
            //    - Abort
            //       - Make sure original line is restored
            //    - Custom bound abort
            //       - Make sure original line is restored
            // * Reverse directions
            // * Start with fwd search, then reverse
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
