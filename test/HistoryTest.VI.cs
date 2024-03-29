﻿using System;
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
        public void ViSearchHistory()
        {
            TestSetup(KeyMode.Vi);

            // Clear history in case the above added some history (but it shouldn't)
            SetHistory();
            Test(" ", Keys(' ', _.UpArrow, _.DownArrow));

            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
            var statusColors = Tuple.Create(_console.ForegroundColor, _console.BackgroundColor);

            SetHistory("dosomething", "this way", "that way", "anyway", "no way", "yah way");

            Test("dosomething", Keys(
                _.Escape, _.Slash, "some", _.Enter, CheckThat(() => AssertLineIs("dosomething")),
                _.Question, "yah", _.Enter, CheckThat(() => AssertLineIs("yah way")),
                _.Slash, "some", _.Enter, 'h'   // need 'h' here to avoid bogus failure
                ));

            SetHistory("someway", "noway", "yahway");

            Test("yahway", Keys(
                // Change to Command mode.
                _.Escape,
                _.Ctrl_r, "way",
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "yah",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "bck-i-search: way_")),
                _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "no",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "bck-i-search: way_")),
                _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "some",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "bck-i-search: way_")),
                _.Ctrl_s,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "no",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "fwd-i-search: way_")),
                _.Ctrl_s,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "yah",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "fwd-i-search: way_")),
                _.Ctrl_s,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "yahway",
                    NextLine,
                    statusColors, "failed-fwd-i-search: way_")),

                // Abort the history search.
                _.Ctrl_g, CheckThat(() => AssertLineIs(string.Empty)),
                // Search again and escape from the search.
                _.Ctrl_r, "yah",
                _.Escape, CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "yahway")),
                // We should not be able to edit the line, because we are in Command mode.
                "nnn"));

            SetHistory("someway", "noway", "yahway");

            Test("nnnyahway", Keys(
                _.Ctrl_r, "way",
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "yah",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "bck-i-search: way_")),
                _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "no",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "bck-i-search: way_")),
                _.Ctrl_r,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "some",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "bck-i-search: way_")),
                _.Ctrl_s,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "no",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "fwd-i-search: way_")),
                _.Ctrl_s,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "yah",
                    emphasisColors, "way",
                    NextLine,
                    statusColors, "fwd-i-search: way_")),
                _.Ctrl_s,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "yahway",
                    NextLine,
                    statusColors, "failed-fwd-i-search: way_")),

                // Abort the history search.
                _.Ctrl_g, CheckThat(() => AssertLineIs(string.Empty)),
                // Search again and escape from the search.
                _.Ctrl_r, "yah",
                _.Escape, CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "yahway")),
                // We should be able to edit the line, because we are in Edit mode.
                "nnn"));
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
