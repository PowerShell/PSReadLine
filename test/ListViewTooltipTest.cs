using System;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void List_Item_Tooltip_4_Lines()
        {
            // Set the terminal height to 22 and width to 60, so the metadata line will be fully rendered
            // and maximum 4 lines can be used for tooltip for a selected list item.
            int listWidth = 60;
            TestSetup(new TestConsole(keyboardLayout: _, width: listWidth, height: 22), KeyMode.Cmd);

            // The font effect sequences of 'dim' and 'italic' used in list view metadata line
            // are ignored in the mock console, so only the white color will be left.
            var dimmedColors = Tuple.Create(ConsoleColor.White, _console.BackgroundColor);
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
            using var disp = SetPrediction(PredictionSource.HistoryAndPlugin, PredictionViewStyle.ListView);
            _mockedMethods.ClearPredictionFields();

            SetHistory("tooltip -history");
            Test("tooltip NO2", Keys(
                "tooltip", CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "tooltip",
                        NextLine,
                        TokenClassification.ListPrediction, "<-/3>",
                        TokenClassification.None, new string(' ', listWidth - 28), // 28 is the length of '<-/3>' plus '<History(1) Tooltip(2)>'.
                        dimmedColors, "<History(1) Tooltip(2)>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " -history",
                        TokenClassification.None, new string(' ', listWidth - 27), // 27 is the length of '> tooltip -history' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO1",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO1' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO2",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO2' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.DownArrow,
                     CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-history",
                        NextLine,
                        TokenClassification.ListPrediction, "<1/3>",
                        TokenClassification.None, new string(' ', listWidth - 30), // 30 is the length of '<1/3>' plus '<History(1/1) Tooltip(2)>'.
                        dimmedColors, '<',
                        TokenClassification.ListPrediction, "History(1/1) ",
                        dimmedColors, "Tooltip(2)>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.ListPredictionSelected, " -history",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 27), // 27 is the length of '> tooltip -history' plus '[History]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO1",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO1' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO2",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO2' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.DownArrow,
                    CheckThat(() => AssertScreenIs(10,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, " NO1",
                        NextLine,
                        TokenClassification.ListPrediction, "<2/3>",
                        TokenClassification.None, new string(' ', listWidth - 30), // 30 is the length of '<2/3>' plus '<History(1) Tooltip(1/2)>'.
                        dimmedColors, "<History(1) ",
                        TokenClassification.ListPrediction, "Tooltip(1/2)",
                        dimmedColors, '>',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " -history",
                        TokenClassification.None, new string(' ', listWidth - 27), // 27 is the length of '> tooltip -history' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.ListPredictionSelected, " NO1",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO1' plus '[Tooltip]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        dimmedColors, "   >> Hello",  NextLine,
                        dimmedColors, "      Binary", NextLine,
                        dimmedColors, "      World",  NextLine,
                        dimmedColors, "      PowerShell is a task automation an… ",
                        TokenClassification.ListPrediction, "(<F4> to view all)",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO2",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO2' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.DownArrow,
                    CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, " NO2",
                        NextLine,
                        TokenClassification.ListPrediction, "<3/3>",
                        TokenClassification.None, new string(' ', listWidth - 30), // 30 is the length of '<3/3>' plus '<History(1) Tooltip(2/2)>'.
                        dimmedColors, "<History(1) ",
                        TokenClassification.ListPrediction, "Tooltip(2/2)",
                        dimmedColors, '>',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " -history",
                        TokenClassification.None, new string(' ', listWidth - 27), // 27 is the length of '> tooltip -history' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO1",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO1' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.ListPredictionSelected, " NO2",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO2' plus '[Tooltip]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        dimmedColors, "   >> Hello", NextLine,
                        dimmedColors, "      World",
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),

                // Once accepted, the list should be cleared.
                _.Enter, CheckThat(() => AssertScreenIs(2,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, " NO2",
                        NextLine,
                        NextLine))
            ));
        }

        [SkippableFact]
        public void List_Item_Tooltip_2_Lines()
        {
            // Set the terminal height to 15 and width to 60, so the metadata line will be fully rendered
            // and maximum 2 lines can be used for tooltip for a selected list item.
            int listWidth = 60;
            TestSetup(new TestConsole(keyboardLayout: _, width: listWidth, height: 15), KeyMode.Cmd);

            // The font effect sequences of 'dim' and 'italic' used in list view metadata line
            // are ignored in the mock console, so only the white color will be left.
            var dimmedColors = Tuple.Create(ConsoleColor.White, _console.BackgroundColor);
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
            using var disp = SetPrediction(PredictionSource.HistoryAndPlugin, PredictionViewStyle.ListView);
            _mockedMethods.ClearPredictionFields();

            SetHistory("tooltip -history");
            Test("tooltip NO2", Keys(
                "tooltip", CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "tooltip",
                        NextLine,
                        TokenClassification.ListPrediction, "<-/3>",
                        TokenClassification.None, new string(' ', listWidth - 28), // 28 is the length of '<-/3>' plus '<History(1) Tooltip(2)>'.
                        dimmedColors, "<History(1) Tooltip(2)>",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " -history",
                        TokenClassification.None, new string(' ', listWidth - 27), // 27 is the length of '> tooltip -history' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO1",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO1' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO2",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO2' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.DownArrow, _.DownArrow,
                    CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, " NO1",
                        NextLine,
                        TokenClassification.ListPrediction, "<2/3>",
                        TokenClassification.None, new string(' ', listWidth - 30), // 30 is the length of '<2/3>' plus '<History(1) Tooltip(1/2)>'.
                        dimmedColors, "<History(1) ",
                        TokenClassification.ListPrediction, "Tooltip(1/2)",
                        dimmedColors, '>',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " -history",
                        TokenClassification.None, new string(' ', listWidth - 27), // 27 is the length of '> tooltip -history' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.ListPredictionSelected, " NO1",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO1' plus '[Tooltip]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        dimmedColors, "   >> Hello", NextLine,
                        dimmedColors, "      Binary … ",
                        TokenClassification.ListPrediction, "(<F4> to view all)",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO2",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO2' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),
                _.F4,
                    CheckThat(() => Assert.Equal(
                        "Hello\nBinary\nWorld\nPowerShell is a task automation and configuration management program from Microsoft",
                        _mockedMethods.helpContentRendered)),
                _.DownArrow,
                    CheckThat(() => AssertScreenIs(8,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, " NO2",
                        NextLine,
                        TokenClassification.ListPrediction, "<3/3>",
                        TokenClassification.None, new string(' ', listWidth - 30), // 30 is the length of '<3/3>' plus '<History(1) Tooltip(2/2)>'.
                        dimmedColors, "<History(1) ",
                        TokenClassification.ListPrediction, "Tooltip(2/2)",
                        dimmedColors, '>',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " -history",
                        TokenClassification.None, new string(' ', listWidth - 27), // 27 is the length of '> tooltip -history' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO1",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO1' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.ListPredictionSelected, " NO2",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO2' plus '[Tooltip]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        dimmedColors, "   >> Hello", NextLine,
                        dimmedColors, "      World",
                        // List view is done, no more list item following.
                        NextLine,
                        NextLine
                     )),

                // Once accepted, the list should be cleared.
                _.Enter, CheckThat(() => AssertScreenIs(2,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, " NO2",
                        NextLine,
                        NextLine))
            ));
        }

        [SkippableFact]
        public void List_Item_Tooltip_1_Line()
        {
            // Set the terminal height to 6 and width to 60, so the metadata line will be fully rendered
            // and maximum 2 lines can be used for tooltip for a selected list item.
            int listWidth = 60;
            TestSetup(new TestConsole(keyboardLayout: _, width: listWidth, height: 6), KeyMode.Cmd);

            // The font effect sequences of 'dim' and 'italic' used in list view metadata line
            // are ignored in the mock console, so only the white color will be left.
            var dimmedColors = Tuple.Create(ConsoleColor.White, _console.BackgroundColor);
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
            using var disp = SetPrediction(PredictionSource.HistoryAndPlugin, PredictionViewStyle.ListView);
            _mockedMethods.ClearPredictionFields();

            SetHistory("tooltip -history");
            Test("tooltip NO2", Keys(
                "tooltip",  _.DownArrow, _.DownArrow,
                    CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, " NO1",
                        NextLine,
                        TokenClassification.ListPrediction, "<2/3>",
                        TokenClassification.None, new string(' ', listWidth - 30), // 30 is the length of '<2/3>' plus '<History(1) Tooltip(1/2)>'.
                        dimmedColors, "<History(1) ",
                        TokenClassification.ListPrediction, "Tooltip(1/2)",
                        dimmedColors, '>',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " -history",
                        TokenClassification.None, new string(' ', listWidth - 27), // 27 is the length of '> tooltip -history' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.ListPredictionSelected, " NO1",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO1' plus '[Tooltip]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        dimmedColors, "   >> Hello … ",
                        TokenClassification.ListPrediction, "(<F4> to view all)",
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO2",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO2' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        // List view is done, no more list item following.
                        NextLine
                     )),
                _.F4,
                    CheckThat(() => Assert.Equal(
                        "Hello\nBinary\nWorld\nPowerShell is a task automation and configuration management program from Microsoft",
                        _mockedMethods.helpContentRendered)),
                _.DownArrow,
                    CheckThat(() => AssertScreenIs(6,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, " NO2",
                        NextLine,
                        TokenClassification.ListPrediction, "<3/3>",
                        TokenClassification.None, new string(' ', listWidth - 30), // 30 is the length of '<3/3>' plus '<History(1) Tooltip(2/2)>'.
                        dimmedColors, "<History(1) ",
                        TokenClassification.ListPrediction, "Tooltip(2/2)",
                        dimmedColors, '>',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " -history",
                        TokenClassification.None, new string(' ', listWidth - 27), // 27 is the length of '> tooltip -history' plus '[History]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "History",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.None, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.None, " NO1",
                        TokenClassification.None, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO1' plus '[Tooltip]'.
                        TokenClassification.None, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.None, ']',
                        NextLine,
                        TokenClassification.ListPrediction, '>',
                        TokenClassification.ListPredictionSelected, ' ',
                        emphasisColors, "tooltip",
                        TokenClassification.ListPredictionSelected, " NO2",
                        TokenClassification.ListPredictionSelected, new string(' ', listWidth - 22), // 22 is the length of '> tooltip NO2' plus '[Tooltip]'.
                        TokenClassification.ListPredictionSelected, '[',
                        TokenClassification.ListPrediction, "Tooltip",
                        TokenClassification.ListPredictionSelected, ']',
                        NextLine,
                        dimmedColors, "   >> Hello … ",
                        TokenClassification.ListPrediction, "(<F4> to view all)",
                        // List view is done, no more list item following.
                        NextLine
                     )),
                _.F4,
                    CheckThat(() => Assert.Equal(
                        "Hello\nWorld",
                        _mockedMethods.helpContentRendered)),

                // Once accepted, the list should be cleared.
                _.Enter, CheckThat(() => AssertScreenIs(2,
                        TokenClassification.Command, "tooltip",
                        TokenClassification.None, " NO2",
                        NextLine,
                        NextLine))
            ));
        }
    }
}
