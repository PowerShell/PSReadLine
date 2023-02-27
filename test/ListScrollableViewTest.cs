using System;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void List_MetaLine_And_Paging_Navigation()
        {
            int listWidth = 100;
            TestSetup(new TestConsole(keyboardLayout: _, width: listWidth, height: 15), KeyMode.Cmd);

            // The font effect sequences of the dimmed color used in list view metadata line
            // are ignored in the mock console, so only the white color will be left.
            var dimmedColors = Tuple.Create(ConsoleColor.White, _console.BackgroundColor);
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);

            // Using the 'HistoryAndPlugin' source will make PSReadLine get prediction from both history and plugin.
            using var disp = SetPrediction(PredictionSource.HistoryAndPlugin, PredictionViewStyle.ListView);
            _mockedMethods.ClearPredictionFields();

            SetHistory("metadata-line -zoo");
            Test("SOME TEXT BEFORE metadata-line", Keys(
                "metadata-line", CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "metadata-line",
                        NextLine,
                        TokenClassification.ListPrediction, "<-/5>",
                        TokenClassification.None, new string(' ', listWidth - 55), // 55 is the length of '<-/5>' plus '<History(1) TestPredictor(2) LongNamePredic…(1) …>'.
                        dimmedColors, "<History(1) TestPredictor(2) LongNamePredic…(1) …>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " -zoo",
                        TokenClassification.None, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.DownArrow, _.PageDown,
                     CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " NEW TEXT",
                        NextLine,
                        TokenClassification.ListPrediction, "<5/5>",
                        TokenClassification.None, new string(' ', listWidth - 58), // 58 is the length of '<5/5>' plus '<… TestPredictor(2) LongNamePredic…(1) Metadata(1/1)>'.
                        dimmedColors, "<… TestPredictor(2) LongNamePredic…(1) ",
                        TokenClassification.ListPrediction, "Metadata(1/1)",
                        dimmedColors, '>',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " -zoo",
                        TokenClassification.None, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME NEW TEXT",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.ListPredictionSelected, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.PageUp, CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "metadata-line",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo",
                        NextLine,
                        TokenClassification.ListPrediction, "<1/5>",
                        TokenClassification.None, new string(' ', listWidth - 57), // 57 is the length of '<1/5>' plus '<History(1/1) TestPredictor(2) LongNamePredic…(1) …>'.
                        dimmedColors, '<',
                        TokenClassification.ListPrediction, "History(1/1) ",
                        dimmedColors, "TestPredictor(2) LongNamePredic…(1) …>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.ListPredictionSelected, " -zoo",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.Ctrl_PageDown, CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " TEXT BEFORE metadata-line",
                        NextLine,
                        TokenClassification.ListPrediction, "<2/5>",
                        TokenClassification.None, new string(' ', listWidth - 57), // 57 is the length of '<1/5>' plus '<History(1) TestPredictor(1/2) LongNamePredic…(1) …>'.
                        dimmedColors, "<History(1) ",
                        TokenClassification.ListPrediction, "TestPredictor(1/2) ",
                        dimmedColors, "LongNamePredic…(1) …>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " -zoo",
                        TokenClassification.None, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.Ctrl_PageDown, CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " NEW TEXT",
                        NextLine,
                        TokenClassification.ListPrediction, "<4/5>",
                        TokenClassification.None, new string(' ', listWidth - 58), // 58 is the length of '<4/5>' plus '<… TestPredictor(2) LongNamePredic…(1/1) Metadata(1)>'.
                        dimmedColors, "<… TestPredictor(2) ",
                        TokenClassification.ListPrediction, "LongNamePredic…(1/1) ",
                        dimmedColors, "Metadata(1)>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " -zoo",
                        TokenClassification.None, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME NEW TEXT",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.Ctrl_PageDown, CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " NEW TEXT",
                        NextLine,
                        TokenClassification.ListPrediction, "<5/5>",
                        TokenClassification.None, new string(' ', listWidth - 58), // 58 is the length of '<5/5>' plus '<… TestPredictor(2) LongNamePredic…(1) Metadata(1/1)>'.
                        dimmedColors, "<… TestPredictor(2) LongNamePredic…(1) ",
                        TokenClassification.ListPrediction, "Metadata(1/1)",
                        dimmedColors, '>',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " -zoo",
                        TokenClassification.None, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME NEW TEXT",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.ListPredictionSelected, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.Ctrl_PageDown, CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "metadata-line",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo",
                        NextLine,
                        TokenClassification.ListPrediction, "<1/5>",
                        TokenClassification.None, new string(' ', listWidth - 57), // 57 is the length of '<1/5>' plus '<History(1/1) TestPredictor(2) LongNamePredic…(1) …>'.
                        dimmedColors, '<',
                        TokenClassification.ListPrediction, "History(1/1) ",
                        dimmedColors, "TestPredictor(2) LongNamePredic…(1) …>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.ListPredictionSelected, " -zoo",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.Ctrl_PageUp, CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " NEW TEXT",
                        NextLine,
                        TokenClassification.ListPrediction, "<5/5>",
                        TokenClassification.None, new string(' ', listWidth - 58), // 58 is the length of '<5/5>' plus '<… TestPredictor(2) LongNamePredic…(1) Metadata(1/1)>'.
                        dimmedColors, "<… TestPredictor(2) LongNamePredic…(1) ",
                        TokenClassification.ListPrediction, "Metadata(1/1)",
                        dimmedColors, '>',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " -zoo",
                        TokenClassification.None, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME NEW TEXT",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.ListPredictionSelected, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.Ctrl_PageUp, _.Ctrl_PageUp,
                    CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " TEXT BEFORE metadata-line",
                        NextLine,
                        TokenClassification.ListPrediction, "<2/5>",
                        TokenClassification.None, new string(' ', listWidth - 57), // 57 is the length of '<1/5>' plus '<History(1) TestPredictor(1/2) LongNamePredic…(1) …>'.
                        dimmedColors, "<History(1) ",
                        TokenClassification.ListPrediction, "TestPredictor(1/2) ",
                        dimmedColors, "LongNamePredic…(1) …>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " -zoo",
                        TokenClassification.None, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),

                // Once accepted, the list should be cleared.
                _.Enter, CheckThat(() => AssertScreenIs(2,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " TEXT BEFORE metadata-line",
                        NextLine,
                        NextLine))
            ));
        }

        [SkippableFact]
        public void ListView_AdapteTo_ConsoleSize()
        {
            // Console size is very small (h: 6, w: 50), and thus the list view will adjust to use 3-line height,
            // and the metadata line will be reduced to only show the (index/total) info.
            int listWidth = 50;
            TestSetup(new TestConsole(keyboardLayout: _, width: listWidth, height: 6), KeyMode.Cmd);
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);

            // Using the 'HistoryAndPlugin' source will make PSReadLine get prediction from both history and plugin.
            using var disp = SetPrediction(PredictionSource.HistoryAndPlugin, PredictionViewStyle.ListView);
            _mockedMethods.ClearPredictionFields();

            SetHistory("metadata-line -zoo");
            Test("metadata-line -zoo", Keys(
                "metadata-line", CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "metadata-line",
                        NextLine,
                        TokenClassification.None, "  ",
                        TokenClassification.ListPrediction, "<-/5>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " -zoo",
                        TokenClassification.None, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.UpArrow, CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " NEW TEXT",
                        NextLine,
                        TokenClassification.None, "  ",
                        TokenClassification.ListPrediction, "<5/5>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME NEW TEXT",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.ListPredictionSelected, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.UpArrow, CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " NEW TEXT",
                        NextLine,
                        TokenClassification.None, "  ",
                        TokenClassification.ListPrediction, "<4/5>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME NEW TEXT",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.Ctrl_PageUp, CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " TEXT BEFORE metadata-line",
                        NextLine,
                        TokenClassification.None, "  ",
                        TokenClassification.ListPrediction, "<2/5>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.PageUp, CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "metadata-line",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo",
                        NextLine,
                        TokenClassification.None, "  ",
                        TokenClassification.ListPrediction, "<1/5>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.ListPredictionSelected, " -zoo",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.PageDown, CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        NextLine,
                        TokenClassification.None, "  ",
                        TokenClassification.ListPrediction, "<3/5>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " -zoo",
                        TokenClassification.None, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.ListPredictionSelected, " SOME TEXT AFTER",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.ListPredictionSelected, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.PageDown, CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "SOME",
                        TokenClassification.None, " NEW TEXT",
                        NextLine,
                        TokenClassification.None, "  ",
                        TokenClassification.ListPrediction, "<5/5>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, " SOME NEW TEXT",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.ListPredictionSelected, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.DownArrow, CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "metadata-line",
                        NextLine,
                        TokenClassification.None, "  ",
                        TokenClassification.ListPrediction, "<-/5>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePredic…]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "LongNamePredic…",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME NEW TEXT",
                        TokenClassification.None, new string(' ', listWidth - 25), // 25 is the length of '> SOME NEW TEXT' plus '[Metadata]'
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Metadata",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.DownArrow, CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "metadata-line",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo",
                        NextLine,
                        TokenClassification.None, "  ",
                        TokenClassification.ListPrediction, "<1/5>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.ListPredictionSelected, " -zoo",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 29), // 29 is the length of '> metadata-line -zoo' plus '[History]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, " SOME TEXT BEFORE ",
                        emphasisColors, "metadata-line",
                        TokenClassification.None, new string(' ', listWidth - 47), // 47 is the length of '> SOME TEXT BEFORE metadata-line' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "metadata-line",
                        TokenClassification.None, " SOME TEXT AFTER",
                        TokenClassification.None, new string(' ', listWidth - 46), // 46 is the length of '> metadata-line SOME TEXT AFTER' plus '[TestPredictor]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "TestPredictor",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),

                // Once accepted, the list should be cleared.
                _.Enter, CheckThat(() => AssertScreenIs(2,
                        TokenClassification.Command, "metadata-line",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo",
                        NextLine,
                        NextLine))
            ));
        }

        [SkippableFact]
        public void ListView_TermSize_Warning()
        {
            // Console size is very small (h: 6, w: 50), and thus the list view will adjust to use 3-line height,
            // and the metadata line will be reduced to only show the (index/total) info.
            int listWidth = 40;
            TestSetup(new TestConsole(keyboardLayout: _, width: listWidth, height: 4), KeyMode.Cmd);
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

            Test("git", Keys(
                _.F2, // Switch to the list view, then test the warning message.
                'g', CheckThat(() => AssertScreenIs(4,
                        TokenClassification.Command, "g",
                        NextLine,
                        TokenClassification.ListPrediction, "! terminal size too small to show the li",
                        NextLine,
                        TokenClassification.ListPrediction, "st view",
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                    )),
                'i', CheckThat(() => AssertScreenIs(4,
                        TokenClassification.Command, "gi",
                        NextLine,
                        TokenClassification.ListPrediction, "! terminal size too small to show the li",
                        NextLine,
                        TokenClassification.ListPrediction, "st view",
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                    )),

                // Escape should clear the warning as well.
                _.Escape, CheckThat(() => AssertScreenIs(3,
                        NextLine,
                        NextLine,
                        NextLine
                    )),
                "git", CheckThat(() => AssertScreenIs(4,
                        TokenClassification.Command, "git",
                        NextLine,
                        TokenClassification.ListPrediction, "! terminal size too small to show the li",
                        NextLine,
                        TokenClassification.ListPrediction, "st view",
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                    )),

                // Once accepted, the list should be cleared.
                _.Enter, CheckThat(() => AssertScreenIs(3,
                        TokenClassification.Command, "git",
                        NextLine,
                        NextLine,
                        NextLine))
            ));
        }
    }
}
