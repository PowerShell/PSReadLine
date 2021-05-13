using System;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void ViHistory()
        {
            TestSetup(KeyMode.Vi);

            // No history
            SetHistory();
            Test("", Keys(_.UpArrow, _.DownArrow));

            SetHistory("000", "001", "002", "003", "004");
            Test("004", Keys(_.UpArrow));

            SetHistory("000", "001", "002", "003", "004");
            Test("002", Keys(_.UpArrow, _.UpArrow, _.UpArrow));

            SetHistory("000", "001", "002", "003", "004");
            Test("004", Keys(_.UpArrow, _.UpArrow, _.DownArrow));

            SetHistory("000", "001", "002", "003", "004");
            Test("004", Keys(_.Escape, _.UpArrow));

            SetHistory("000", "001", "002", "003", "004");
            Test("002", Keys(_.Escape, _.UpArrow, _.UpArrow, _.UpArrow));

            SetHistory("000", "001", "002", "003", "004");
            Test("004", Keys(_.Escape, _.UpArrow, _.UpArrow, _.DownArrow));

            // For defect lzybkr/PSReadLine #571
            SetHistory("000", "001", "002", "003", "004");
            Test("004", Keys(_.Escape, "jk", _.Escape, "0C", _.UpArrow, _.Escape));

            SetHistory("000", "001", "002", "003", "004");
            Test("003", Keys(_.Escape, "kk"));

            SetHistory("000", "001", "002", "003", "004");
            Test("004", Keys(_.Escape, "kkj"));

            SetHistory("000", "001", "002", "003", "004");
            Test("003", Keys(_.Escape, "--"));

            SetHistory("000", "001", "002", "003", "004");
            Test("003", Keys(_.Escape, "---+"));
        }

        [SkippableFact]
        public void ViInteractiveHistorySearch()
        {
            TestSetup(KeyMode.Vi);

            SetHistory("echo aaa");
            Test("echo aaa", Keys(_.Ctrl_r, 'a'));

            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
            var statusColors = Tuple.Create(_console.ForegroundColor, _console.BackgroundColor);

            // Test entering multiple characters and the line is updated with new matches
            SetHistory("zz1", "echo abc", "zz2", "echo abb", "zz3", "echo aaa", "zz4");
            Test("echo abc", Keys(_.Ctrl_r,
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

            // Test repeated Ctrl+r goes back through multiple matches
            SetHistory("zz1", "echo abc", "zz2", "echo abb", "zz3", "echo aaa", "zz4");
            Test("echo abc", Keys(_.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "aa",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_r, CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bb",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_r, CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_"))));

            // Test that the current match doesn't change when typing
            // additional characters, only emphasis should change.
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo abc", Keys(_.Ctrl_r,
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

            // Test that abort restores line state before Ctrl+r
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo zed", Keys("echo zed", _.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_g,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    TokenClassification.None, "zed",
                    NextLine))));

            // Test that Escape terminates the search leaving the
            // cursor at the point in the match.
            SetHistory("zz1", "echo abzz", "echo abc", "zz2");
            Test("echo yabc", Keys(_.Ctrl_r,
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
            Test("echo aaa", Keys(_.Ctrl_r,
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
            Test("", Keys(_.Ctrl_r,
                'a',
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bc",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bzz",
                    NextLine,
                    statusColors, "bck-i-search: a_")),
                _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    TokenClassification.None, "abzz",
                    NextLine,
                    statusColors, "failed-bck-i-search: a_")),
                _.Ctrl_s,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, 'a',
                    TokenClassification.None, "bzz",
                    NextLine,
                    statusColors, "fwd-i-search: a_")),
                _.Ctrl_g));

            // Test that searching works after a failed search
            SetHistory("echo aa1", "echo bb1", "echo bb2", "echo aa2");
            Test("echo aa1", Keys(_.Ctrl_r, "zz", _.Backspace, _.Backspace, "a1",
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
                _.Ctrl_r,
                "aa",
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "aa",
                    TokenClassification.None, "2",
                    NextLine,
                    statusColors, "bck-i-search: aa_")),
                _.Ctrl_r,
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
                'a', _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    emphasisColors, "aa",
                    TokenClassification.None, "1",
                    NextLine,
                    statusColors, "bck-i-search: aa_")),
                _.Backspace));

            // TODO: long search line
            // TODO: start with Ctrl+s
            // TODO: "fast" typing in search where buffered keys after search is accepted
        }
        [SkippableFact]
        public void ViHistoryRepeat()
        {
            TestSetup(KeyMode.Vi);
            // Clear history in case the above added some history (but it shouldn't)
            SetHistory();
            Test( " ", Keys( ' ', _.UpArrow, _.DownArrow ) );

            SetHistory( "dosomething", "this way", "that way", "anyway", "no way", "yah way" );
            Test( "anyway", Keys(
                _.Escape, _.Slash, "way", _.Enter, CheckThat( () => AssertLineIs( "yah way" ) ),
                "nn", CheckThat( () => AssertLineIs( "anyway" ) ),
                "N", CheckThat( () => AssertLineIs( "no way" ) ),
                "N", CheckThat( () => AssertLineIs( "yah way" ) ),
                "nn"
                ) );

            Test("anyway", Keys(
                _.Escape, _.Slash, "way", _.Tab, CheckThat(() => AssertLineIs("anyway")),
                "nnnn", CheckThat(() => AssertLineIs("that way")),
                "N", CheckThat(() => AssertLineIs("anyway")),
                "N", CheckThat(() => AssertLineIs("no way")),
                "n"
                ));
        }

        [SkippableFact]
        public void ViHistoryCommandMix()
        {
            TestSetup(KeyMode.Vi);

            // Clear history in case the above added some history (but it shouldn't)
            SetHistory();
            Test( " ", Keys( ' ', _.UpArrow, _.DownArrow ) );

            // Mix history search, repeat, and recall.
            // Mix different history commands to verify that the saved current line and
            // the history index stay the same while in a series of history commands.

            SetHistory("bar1", "bar2", "bar3", "bar4", "bar5");
            Test("first", Keys(
                "first", _.Escape, _.Slash, "bar", _.Enter,
                CheckThat(() => AssertLineIs("bar5")),
                _.DownArrow, CheckThat(() => AssertLineIs("first")),
                _.Slash, "bar", _.Enter,
                CheckThat(() => AssertLineIs("bar5")),
                "nn", CheckThat(() => AssertLineIs("bar3")),
                "N", CheckThat(() => AssertLineIs("bar4")),
                "N", CheckThat(() => AssertLineIs("bar5")),
                "nnn", CheckThat(() => AssertLineIs("bar2")),
                _.UpArrow, CheckThat(() => AssertLineIs("bar1")),
                _.DownArrow, CheckThat(() => AssertLineIs("bar2")),
                _.DownArrow, CheckThat(() => AssertLineIs("bar3")),
                _.DownArrow, CheckThat(() => AssertLineIs("bar4")),
                _.DownArrow, CheckThat(() => AssertLineIs("bar5")),
                _.DownArrow));
        }

        [SkippableFact]
        public void ViMovementAfterHistory()
        {
            TestSetup(KeyMode.Vi);
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption { HistorySearchCursorMovesToEnd = true });

            SetHistory("abc def ghi", "012 456 890");

            Test("012 456 890", Keys(
                _.Escape, "k", CheckThat(() => AssertCursorLeftIs(10))
                ));
        }

        [SkippableFact]
        public void ViChangeAfterHistory()
        {
            TestSetup(KeyMode.Vi);

            SetHistory("abc def ghi", "012 456 890");

            Test("xyz", Keys(
                _.Escape, "kj", CheckThat(() => AssertLineIs("")),
                "clxyz", _.Escape
                ));
        }

        [SkippableFact]
        public void ViAppendAfterHistory()
        {
            TestSetup(KeyMode.Vi);

            SetHistory("abc def ghi", "012 456 890");

            Test("xyz", Keys(
                _.Escape, "kj", CheckThat(() => AssertLineIs("")),
                "axyz", _.Escape
                ));

            Test("xyz", Keys(
                _.Escape, "kj", CheckThat(() => AssertLineIs("")),
                "Axyz", _.Escape
                ));
        }

        [SkippableFact]
        public void ViHistoryCursorPosition()
        {
            TestSetup(KeyMode.Vi);
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption { HistorySearchCursorMovesToEnd = false });

            SetHistory("abc def ghi", "012 456 890");

            Test("012 456 890", Keys(
                _.Escape, "k", CheckThat(() => AssertLineIs("012 456 890")),
                CheckThat(() => AssertCursorLeftIs(0))
                ));

        }
    }
}
