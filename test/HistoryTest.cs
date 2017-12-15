﻿using System;
using System.Linq;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class ReadLine
    {
        private void SetHistory(params string[] historyItems)
        {
            PSConsoleReadLine.ClearHistory();
            foreach (var item in historyItems)
            {
                PSConsoleReadLine.AddToHistory(item);
            }
        }

        [Fact]
        public void History()
        {
            TestSetup(KeyMode.Cmd);

            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));

            SetHistory("dir c*", "ps p*");

            Test("dir c*", Keys(_.UpArrow, _.UpArrow));
            Test("dir c*", Keys(_.UpArrow, _.UpArrow, _.DownArrow));
        }

        [Fact]
        public void HistoryRecallCurrentLine()
        {
            TestSetup(KeyMode.Cmd);

            SetHistory("echo foo", "echo bar");
            Test("ec", Keys("ec", _.UpArrow, _.UpArrow, _.DownArrow, _.DownArrow));
        }

        [Fact]
        public void HistorySearchCurrentLine()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            SetHistory("echo foo", "echo bar");
            Test("ec", Keys("ec", _.UpArrow, _.UpArrow, _.DownArrow, _.DownArrow));
        }

        [Fact]
        public void SearchHistory()
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

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistorySearchCursorMovesToEnd = false});
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);

            SetHistory("dosomething", "ps p*", "dir", "echo zzz");
            Test("dosomething", Keys(
                "d",
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(1);
                }),
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(1);
            })));

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistorySearchCursorMovesToEnd = true});
            SetHistory("dosomething", "ps p*", "dir", "echo zzz");
            Test("dosomething", Keys(
                "d",
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(11);
                }),
                _.DownArrow, CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() =>
                {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(11);
                })));
        }

        [Fact]
        public void HistorySearchCursorMovesToEnd()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistorySearchCursorMovesToEnd = true});
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);

            SetHistory("dosomething", "ps p*", "dir", "echo zzz");
            Test("dosomething", Keys(
                "d",
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(11);
                }),
                _.DownArrow, CheckThat(() => {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "ir");
                    AssertCursorLeftIs(3);
                }),
                _.UpArrow,   CheckThat(() =>
                {
                    AssertScreenIs(1,
                        emphasisColors, 'd',
                        TokenClassification.Command, "osomething");
                    AssertCursorLeftIs(11);
                })));
        }

        [Fact]
        public void BeginningOfHistory()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory("echo first", "echo second", "echo third");
            Test("echo first", Keys(_.AltLess));

            SetHistory("echo first", "echo second", "echo third");
            Test("echo second", Keys(_.AltLess, _.DownArrow));
        }

        [Fact]
        public void EndOfHistory()
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

        [Fact]
        public void InteractiveHistorySearch()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory("echo aaa");
            Test("echo aaa", Keys(_.CtrlR, 'a'));

            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
            var statusColors = Tuple.Create(_console.ForegroundColor, _console.BackgroundColor);

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

            // Test that searching works after a failed search
            SetHistory("echo aa1", "echo bb1", "echo bb2", "echo aa2");
            Test("echo aa1", Keys(_.CtrlR, "zz", _.Backspace, _.Backspace, "a1",
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " a",
                    emphasisColors, "a1",
                    NextLine,
                    statusColors, "bck-i-search: a1_"))
                ));

            // Test that searching works after backspace after a successful search
            SetHistory("echo aa1", "echo bb1", "echo bb2", "echo aa2");
            Test("echo aa2", Keys(
                _.CtrlR,
                "aa",
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "aa",
                    TokenClassification.None, "2",
                    NextLine,
                    statusColors, "bck-i-search: aa_")),
                _.CtrlR,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "aa",
                    TokenClassification.None, "1",
                    NextLine,
                    statusColors, "bck-i-search: aa_")),
                _.Backspace,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "a",
                    TokenClassification.None, "a2",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                'a', _.CtrlR,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "aa",
                    TokenClassification.None, "1",
                    NextLine,
                    statusColors, "bck-i-search: aa_")),
                _.Backspace));

            // TODO: long search line
            // TODO: start with Ctrl+S
            // TODO: "fast" typing in search where buffered keys after search is accepted
        }

        [Fact]
        public void AddToHistoryHandler()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {AddToHistoryHandler = s => s.StartsWith("z")});

            SetHistory("zzzz", "azzz");
            Test("zzzz", Keys(_.UpArrow));
        }

        [Fact]
        public void HistoryNoDuplicates()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistoryNoDuplicates = false});

            SetHistory("zzzz", "aaaa", "bbbb", "bbbb", "cccc");
            Assert.Equal(5, PSConsoleReadLine.GetHistoryItems().Length);
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 4)));

            // Changing the option should affect existing history.
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistoryNoDuplicates = true});
            Test("zzzz", Keys(Enumerable.Repeat(_.UpArrow, 4)));

            SetHistory("aaaa", "bbbb", "bbbb", "cccc");
            Assert.Equal(3, PSConsoleReadLine.GetHistoryItems().Length);
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 3)));

            SetHistory("aaaa", "bbbb", "bbbb", "cccc");
            Test("cccc", Keys(
                Enumerable.Repeat(_.UpArrow, 3),
                Enumerable.Repeat(_.DownArrow, 2)));


            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));
        }

        [Fact]
        public void HistorySearchNoDuplicates()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistoryNoDuplicates = true});
            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo aaaa", Keys("echo", Enumerable.Repeat(_.UpArrow, 3)));

            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo cccc", Keys(
                "echo",
                Enumerable.Repeat(_.UpArrow, 3),
                Enumerable.Repeat(_.DownArrow, 2)));
        }

        [Fact]
        public void InteractiveHistorySearchNoDuplicates()
        {
            TestSetup(KeyMode.Emacs);

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {HistoryNoDuplicates = true});
            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo aaaa", Keys(
                _.CtrlR, "echo", _.CtrlR, _.CtrlR));

            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo cccc", Keys(
                _.CtrlR, "echo", _.CtrlR, _.CtrlR, _.CtrlS, _.CtrlS));

            SetHistory("0000", "echo aaaa", "1111", "echo bbbb", "2222", "echo bbbb", "3333", "echo cccc", "4444");
            Test("echo aaaa", Keys(
                _.CtrlR, "echo", _.CtrlR, _.CtrlR, _.CtrlH, _.CtrlR, _.CtrlR));
        }

        [Fact]
        public void HistoryCount()
        {
            TestSetup(KeyMode.Cmd);

            SetHistory("zzzz", "aaaa", "bbbb", "cccc");

            // There should be 4 items in history, the following should remove the
            // oldest history item.
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {MaximumHistoryCount = 3});
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 4)));

            Test("zzzz", Keys("zzzz"));
            Test("aaaa", Keys("aaaa"));
            Test("bbbb", Keys("bbbb"));
            Test("cccc", Keys("cccc"));
            Test("aaaa", Keys(Enumerable.Repeat(_.UpArrow, 4)));
        }
    }
}
