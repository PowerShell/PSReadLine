using System;
using Microsoft.PowerShell;
using Xunit;

namespace Test;

public partial class ReadLine
{
    // The source of truth is defined in 'Microsoft.PowerShell.PSConsoleReadLine+PredictionListView'.
    // Make sure the values are in sync.
    private const int MinWindowWidth = 54;
    private const int MinWindowHeight = 15;
    private const int ListMaxWidth = 100;
    private const int SourceMaxWidth = 15;

    private int CheckWindowSize()
    {
        // The buffer/window size of 'TestConsole' is currently fixed to be width 60 and height 1000.
        // This is a precaution check, just in case that things change.
        var winWidth = _console.WindowWidth;
        var winHeight = _console.WindowHeight;
        Assert.True(winWidth >= 54,
            $"list-view prediction requires minimum window width {MinWindowWidth}. Make sure the TestConsole's width is set properly.");
        Assert.True(winHeight >= 15,
            $"list-view prediction requires minimum window height {MinWindowHeight}. Make sure the TestConsole's height is set properly.");

        var listWidth = winWidth > ListMaxWidth ? ListMaxWidth : winWidth;
        return listWidth;
    }

    private Disposable SetPrediction(PredictionSource source, PredictionViewStyle view)
    {
        var options = PSConsoleReadLine.GetOptions();
        var oldSource = options.PredictionSource;
        var oldView = options.PredictionViewStyle;

        PSConsoleReadLine.SetOptions(
            new SetPSReadLineOption {PredictionSource = source, PredictionViewStyle = view});
        return new Disposable(() => PSConsoleReadLine.SetOptions(
            new SetPSReadLineOption {PredictionSource = oldSource, PredictionViewStyle = oldView}));
    }

    private void AssertDisplayedSuggestions(int count, Guid predictorId, uint session, int countOrIndex)
    {
        Assert.Equal(count, _mockedMethods.displayedSuggestions.Count);
        _mockedMethods.displayedSuggestions.TryGetValue(predictorId, out var tuple);
        Assert.NotNull(tuple);
        Assert.Equal(session, tuple.Item1);
        Assert.Equal(countOrIndex, tuple.Item2);
    }

    [SkippableFact]
    public void List_RenderSuggestion_NoMatching_DefaultUpArrowDownArrow()
    {
        TestSetup(KeyMode.Cmd);
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

        // No matching history entry
        SetHistory("echo -bar", "eca -zoo");
        Test("s", Keys(
            's', CheckThat(() => AssertScreenIs(1, TokenClassification.Command, 's')),
            _.UpArrow, CheckThat(() => AssertLineIs("eca -zoo")),
            _.UpArrow, CheckThat(() => AssertLineIs("echo -bar")),
            _.DownArrow, _.DownArrow,
            CheckThat(() => AssertLineIs("s")),
            CheckThat(() => AssertCursorLeftIs(1))
        ));
    }

    [SkippableFact]
    public void List_RenderSuggestion_NoMatching_HistorySearchBackwardForward()
    {
        TestSetup(KeyMode.Cmd,
            new KeyHandler("Ctrl+p", PSConsoleReadLine.HistorySearchBackward),
            new KeyHandler("Ctrl+l", PSConsoleReadLine.HistorySearchForward));
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

        // No matching history entry
        SetHistory("echo -bar", "eca -zoo");
        Test(string.Empty, Keys(
            _.Ctrl_p, CheckThat(() => AssertLineIs("eca -zoo")),
            _.Ctrl_p, CheckThat(() => AssertLineIs("echo -bar")),
            _.Ctrl_l, _.Ctrl_l,
            CheckThat(() => AssertLineIs(string.Empty)),
            CheckThat(() => AssertCursorLeftIs(0))
        ));
    }

    [SkippableFact]
    public void List_RenderSuggestion_ListUpdatesWhileTyping()
    {
        TestSetup(KeyMode.Cmd);
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

        // Different matches as more input coming
        SetHistory("echo -bar", "eca -zoo");
        Test("ech", Keys(
            'e', CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            'c', CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "ec",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, "a -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, "ho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            'h', CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "ech",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ech",
                TokenClassification.None, "o -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // Once accepted, the list should be cleared.
            _.Enter, CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "ech",
                NextLine,
                NextLine))
        ));
    }

    [SkippableFact]
    public void List_RenderSuggestion_NavigateInList_DefaultUpArrowDownArrow()
    {
        TestSetup(KeyMode.Cmd);
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

        // Navigate up and down in the list
        SetHistory("echo -bar", "eca -zoo");
        Test("e", Keys(
            'e', CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.DownArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.DownArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "echo",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-bar",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "cho -bar",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']'
            )),
            _.DownArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.UpArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "echo",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-bar",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "cho -bar",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']'
            )),
            _.UpArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.UpArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // Once accepted, the list should be cleared.
            _.Enter, CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "e",
                NextLine,
                NextLine))
        ));
    }

    [SkippableFact]
    public void List_RenderSuggestion_NavigateInList_HistorySearchBackwardForward()
    {
        TestSetup(KeyMode.Cmd,
            new KeyHandler("Ctrl+p", PSConsoleReadLine.HistorySearchBackward),
            new KeyHandler("Ctrl+l", PSConsoleReadLine.HistorySearchForward));
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

        // Navigate up and down in the list
        SetHistory("echo -bar", "eca -zoo");
        Test("e", Keys(
            'e', CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.Ctrl_l,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.Ctrl_l,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "echo",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-bar",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "cho -bar",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']'
            )),
            _.Ctrl_l,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.Ctrl_p,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "echo",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-bar",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "cho -bar",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']'
            )),
            _.Ctrl_p,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.Ctrl_p,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // Once accepted, the list should be cleared.
            _.Enter, CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "e",
                NextLine,
                NextLine))
        ));
    }

    [SkippableFact]
    public void List_RenderSuggestion_Escape()
    {
        TestSetup(KeyMode.Cmd);
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

        // Press 'Escape' without selecting an item.
        SetHistory("echo -bar", "eca -zoo");
        Test("echo -bar", Keys(
            'c', CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'c',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "a -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "ho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // The list should be cleared upon 'Escape'
            _.Escape,
            CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, 'c',
                NextLine,
                TokenClassification.None, new string(' ', listWidth)
            )),
            // Keep typing will trigger the list view again
            'h', CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "ch",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, "ch",
                TokenClassification.None, "o -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.DownArrow,
            CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "echo",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-bar",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, " e",
                emphasisColors, "ch",
                TokenClassification.ListPredictionSelected, "o -bar",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']'
            )),
            // Once accepted, the list should be cleared.
            _.Enter, CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "echo",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-bar",
                NextLine,
                NextLine))
        ));

        // Press 'Escape' after selecting an item.
        SetHistory("echo -bar", "eca -zoo");
        Test("c", Keys(
            'c', CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'c',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "a -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "ho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.DownArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, " e",
                emphasisColors, 'c',
                TokenClassification.ListPredictionSelected, "a -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "ho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // The list should be cleared upon 'Escape'
            _.Escape,
            CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, 'c',
                NextLine,
                NextLine
            )),
            // 'UpArrow' and 'DownArrow' should navigate history after 'Escape' cleared the list view
            _.UpArrow, CheckThat(() => AssertLineIs("eca -zoo")),
            _.UpArrow, CheckThat(() => AssertLineIs("echo -bar")),
            _.DownArrow, _.DownArrow
        ));
    }

    [SkippableFact]
    public void List_RenderSuggestion_DigitArgument()
    {
        TestSetup(KeyMode.Cmd);
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

        SetHistory("echo -bar", "eca -zoo");
        Test("c", Keys(
            'c', CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'c',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "a -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "ho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.Alt_2,
            CheckThat(() => AssertScreenIs(4,
                TokenClassification.Command, 'c',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "a -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "ho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.None, "digit-argument: 2"
            )),
            _.DownArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "echo",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-bar",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "a -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, " e",
                emphasisColors, 'c',
                TokenClassification.ListPredictionSelected, "ho -bar",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']'
            )),
            _.Alt_2,
            CheckThat(() => AssertScreenIs(4,
                TokenClassification.Command, "echo",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-bar",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "a -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, " e",
                emphasisColors, 'c',
                TokenClassification.ListPredictionSelected, "ho -bar",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.None, "digit-argument: 2"
            )),
            _.UpArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'c',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "a -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " e",
                emphasisColors, 'c',
                TokenClassification.None, "ho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // Once accepted, the list should be cleared.
            _.Enter, CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, 'c',
                NextLine,
                NextLine))
        ));
    }

    [SkippableFact]
    public void List_RenderSuggestion_CtrlZ()
    {
        TestSetup(KeyMode.Cmd);
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

        SetHistory("echo -bar", "eca -zoo");
        Test("e", Keys(
            'e', CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.UpArrow, _.UpArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // No matter how many navigation operations were done, 'Ctrl+z' (undo) reverts back to the initial list view state.
            _.Ctrl_z,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // After undo, you can continue to navigate in the list.
            _.DownArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.DownArrow, _.DownArrow,
            // Once accepted, the list should be cleared.
            _.Enter, CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, 'e',
                NextLine,
                NextLine))
        ));
    }

    [SkippableFact]
    public void List_RenderSuggestion_Selection()
    {
        TestSetup(KeyMode.Cmd);
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

        SetHistory("echo -bar", "eca -zoo");
        Test("eca -zoo", Keys(
            'e', CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, 'e',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "ca -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.DownArrow,
            CheckThat(() => AssertCursorLeftIs(8)),
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // Moving cursor won't trigger a new prediction.
            _.LeftArrow, _.LeftArrow,
            CheckThat(() => AssertCursorLeftIs(6)),
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.Ctrl_LeftArrow,
            CheckThat(() => AssertCursorLeftIs(5)),
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // Text selection won't trigger a new prediction.
            _.Shift_LeftArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Selection, '-',
                TokenClassification.Parameter, "zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            _.Ctrl_Shift_LeftArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Selection, "eca -",
                TokenClassification.Parameter, "zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // Once accepted, the list should be cleared.
            _.Enter, CheckThat(() => AssertScreenIs(2,
                TokenClassification.Selection, "eca -",
                TokenClassification.Parameter, "zoo",
                NextLine,
                NextLine))
        ));
    }

    [SkippableFact]
    public void List_HistorySource_NoAcceptanceCallback()
    {
        TestSetup(KeyMode.Cmd);
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);

        // Using the 'History' source will not trigger 'acceptance' callbacks.
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);
        _mockedMethods.ClearPredictionFields();

        SetHistory("echo -bar", "eca -zoo");
        Test("eca -zooa", Keys(
            'e', _.DownArrow,
            CheckThat(() => AssertScreenIs(3,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, 'e',
                TokenClassification.ListPredictionSelected, "ca -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, 'e',
                TokenClassification.None, "cho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']'
            )),
            // Update a selected item won't trigger 'OnSuggestionAccepted' when suggestion comes from history.
            'a', CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "eca",
                TokenClassification.None, ' ',
                TokenClassification.Parameter, "-zooa",
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            CheckThat(() => Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId)),
            CheckThat(() => Assert.Null(_mockedMethods.acceptedSuggestion)),
            CheckThat(() => Assert.Null(_mockedMethods.commandHistory))
        ));

        // 'Enter' won't trigger 'OnCommandLineAccepted' when plugin is not used as a source.
        Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId);
        Assert.Null(_mockedMethods.acceptedSuggestion);
        Assert.Null(_mockedMethods.commandHistory);
    }

    [SkippableFact]
    public void List_PluginSource_Acceptance()
    {
        TestSetup(KeyMode.Cmd);
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);

        // Using the 'Plugin' source will make PSReadLine get prediction from the plugin only.
        using var disp = SetPrediction(PredictionSource.Plugin, PredictionViewStyle.ListView);
        _mockedMethods.ClearPredictionFields();

        SetHistory("echo -bar", "eca -zoo");
        Test("SOME NEW TEX SOME TEXT AFTER", Keys(
            "ec", CheckThat(() => AssertScreenIs(5,
                TokenClassification.Command, "ec",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "ec",
                TokenClassification.None,
                new string(' ',
                    listWidth - 36), // 36 is the length of '> SOME TEXT BEFORE ec' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 35), // 35 is the length of '> ec SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME NEW TEXT",
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should be fired for both predictors.
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_1, MiniSessionId, 2)),
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_2, MiniSessionId, 1)),
            CheckThat(() => _mockedMethods.ClearPredictionFields()),
            _.DownArrow,
            CheckThat(() => AssertScreenIs(5,
                TokenClassification.Command, "SOME",
                TokenClassification.None, " TEXT BEFORE ec",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, " SOME TEXT BEFORE ",
                emphasisColors, "ec",
                TokenClassification.ListPredictionSelected,
                new string(' ',
                    listWidth - 36), // 36 is the length of '> SOME TEXT BEFORE ec' plus '[TestPredictor]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 35), // 35 is the length of '> ec SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME NEW TEXT",
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should not be fired when navigating the list.
            CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
            _.Shift_Home,
            CheckThat(() => AssertScreenIs(5,
                TokenClassification.Selection, "SOME TEXT BEFORE ec",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, " SOME TEXT BEFORE ",
                emphasisColors, "ec",
                TokenClassification.ListPredictionSelected,
                new string(' ',
                    listWidth - 36), // 36 is the length of '> SOME TEXT BEFORE ec' plus '[TestPredictor]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 35), // 35 is the length of '> ec SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME NEW TEXT",
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should not be fired when selecting the input.
            CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
            "j",
            CheckThat(() => AssertScreenIs(5,
                TokenClassification.Command, "j",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "j",
                TokenClassification.None,
                new string(' ',
                    listWidth - 35), // 35 is the length of '> SOME TEXT BEFORE j' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "j",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 34), // 34 is the length of '> j SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME NEW TEXT",
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should be fired for both predictors.
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_1, MiniSessionId, 2)),
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_2, MiniSessionId, 1)),
            CheckThat(() => Assert.Equal(predictorId_1, _mockedMethods.acceptedPredictorId)),
            CheckThat(() => Assert.Equal("SOME TEXT BEFORE ec", _mockedMethods.acceptedSuggestion)),
            CheckThat(() => Assert.Null(_mockedMethods.commandHistory)),
            CheckThat(() => _mockedMethods.ClearPredictionFields()),
            _.DownArrow,
            _.DownArrow,
            _.DownArrow,
            CheckThat(() => AssertScreenIs(5,
                TokenClassification.Command, "SOME",
                TokenClassification.None, " NEW TEXT",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "j",
                TokenClassification.None,
                new string(' ',
                    listWidth - 35), // 35 is the length of '> SOME TEXT BEFORE j' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "j",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 34), // 34 is the length of '> j SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, " SOME NEW TEXT",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.ListPredictionSelected, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should not be fired when navigating the input.
            CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
            _.Backspace,
            CheckThat(() => AssertScreenIs(5,
                TokenClassification.Command, "SOME",
                TokenClassification.None, " NEW TEX",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None,
                new string(' ',
                    listWidth -
                    46), // 46 is the length of '> SOME TEXT BEFORE SOME NEW TEX' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 45), // 45 is the length of '> SOME NEW TEX SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None, 'T',
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should be fired for both predictors.
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_1, MiniSessionId, 2)),
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_2, MiniSessionId, 1)),
            CheckThat(() => Assert.Equal(predictorId_2, _mockedMethods.acceptedPredictorId)),
            CheckThat(() => Assert.Equal("SOME NEW TEXT", _mockedMethods.acceptedSuggestion)),
            CheckThat(() => Assert.Null(_mockedMethods.commandHistory)),
            CheckThat(() => _mockedMethods.ClearPredictionFields()),
            _.UpArrow,
            _.UpArrow,
            CheckThat(() => AssertScreenIs(5,
                TokenClassification.Command, "SOME",
                TokenClassification.None, " NEW TEX SOME TEXT AFTER",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None,
                new string(' ',
                    listWidth -
                    46), // 46 is the length of '> SOME TEXT BEFORE SOME NEW TEX' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, "SOME NEW TEX",
                TokenClassification.ListPredictionSelected, " SOME TEXT AFTER",
                TokenClassification.ListPredictionSelected,
                new string(' ',
                    listWidth - 45), // 45 is the length of '> SOME NEW TEX SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None, 'T',
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should not be fired when navigating the input.
            CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
            // Once accepted, the list should be cleared.
            _.Enter, CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "SOME",
                TokenClassification.None, " NEW TEX SOME TEXT AFTER",
                NextLine,
                NextLine))
        ));

        // `OnSuggestionDisplayed` should not be fired when 'Enter' accepting the input.
        Assert.Empty(_mockedMethods.displayedSuggestions);
        Assert.Equal(predictorId_1, _mockedMethods.acceptedPredictorId);
        Assert.Equal("SOME NEW TEX SOME TEXT AFTER", _mockedMethods.acceptedSuggestion);
        Assert.Equal(3, _mockedMethods.commandHistory.Count);
        Assert.Equal("echo -bar", _mockedMethods.commandHistory[0]);
        Assert.Equal("eca -zoo", _mockedMethods.commandHistory[1]);
        Assert.Equal("SOME NEW TEX SOME TEXT AFTER", _mockedMethods.commandHistory[2]);
    }

    [SkippableFact]
    public void List_HistoryAndPluginSource_Acceptance()
    {
        TestSetup(KeyMode.Cmd);
        var listWidth = CheckWindowSize();
        var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);

        // Using the 'HistoryAndPlugin' source will make PSReadLine get prediction from both history and plugin.
        using var disp = SetPrediction(PredictionSource.HistoryAndPlugin, PredictionViewStyle.ListView);
        _mockedMethods.ClearPredictionFields();

        SetHistory("echo -bar", "java", "eca -zoo");
        Test("SOME NEW TEX SOME TEXT AFTER", Keys(
            "ec", CheckThat(() => AssertScreenIs(7,
                TokenClassification.Command, "ec",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, "a -zoo",
                TokenClassification.None,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, "ho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "ec",
                TokenClassification.None,
                new string(' ',
                    listWidth - 36), // 36 is the length of '> SOME TEXT BEFORE ec' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 35), // 35 is the length of '> ec SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME NEW TEXT",
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should be fired for both predictors.
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_1, MiniSessionId, 2)),
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_2, MiniSessionId, 1)),
            CheckThat(() => _mockedMethods.ClearPredictionFields()),
            _.DownArrow, _.Shift_Home,
            CheckThat(() => AssertScreenIs(7,
                TokenClassification.Selection, "eca -zoo",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, "ec",
                TokenClassification.ListPredictionSelected, "a -zoo",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 19), // 19 is the length of '> eca -zoo' plus '[History]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, "ho -bar",
                TokenClassification.None,
                new string(' ', listWidth - 20), // 20 is the length of '> echo -bar' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "ec",
                TokenClassification.None,
                new string(' ',
                    listWidth - 36), // 36 is the length of '> SOME TEXT BEFORE ec' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "ec",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 35), // 35 is the length of '> ec SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME NEW TEXT",
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should not be fired when navigating the list.
            CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
            'j', CheckThat(() => AssertScreenIs(6,
                TokenClassification.Command, "j",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "j",
                TokenClassification.None, "ava",
                TokenClassification.None,
                new string(' ', listWidth - 15), // 15 is the length of '> java' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "j",
                TokenClassification.None,
                new string(' ',
                    listWidth - 35), // 35 is the length of '> SOME TEXT BEFORE j' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "j",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 34), // 34 is the length of '> j SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME NEW TEXT",
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should be fired for both predictors.
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_1, MiniSessionId, 2)),
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_2, MiniSessionId, 1)),
            // Update the selected item won't trigger 'acceptance' callbacks if the item is from history.
            CheckThat(() => Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId)),
            CheckThat(() => Assert.Null(_mockedMethods.acceptedSuggestion)),
            CheckThat(() => Assert.Null(_mockedMethods.commandHistory)),
            CheckThat(() => _mockedMethods.ClearPredictionFields()),
            _.UpArrow,
            CheckThat(() => AssertScreenIs(6,
                TokenClassification.Command, "SOME",
                TokenClassification.None, " NEW TEXT",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "j",
                TokenClassification.None, "ava",
                TokenClassification.None,
                new string(' ', listWidth - 15), // 15 is the length of '> java' plus '[History]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "History",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "j",
                TokenClassification.None,
                new string(' ',
                    listWidth - 35), // 35 is the length of '> SOME TEXT BEFORE j' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "j",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 34), // 34 is the length of '> j SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, " SOME NEW TEXT",
                TokenClassification.ListPredictionSelected,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.ListPredictionSelected, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should not be fired when navigating the list.
            CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
            _.Backspace,
            CheckThat(() => AssertScreenIs(5,
                TokenClassification.Command, "SOME",
                TokenClassification.None, " NEW TEX",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None,
                new string(' ',
                    listWidth -
                    46), // 46 is the length of '> SOME TEXT BEFORE SOME NEW TEX' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None, " SOME TEXT AFTER",
                TokenClassification.None,
                new string(' ',
                    listWidth - 45), // 45 is the length of '> SOME NEW TEX SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None, 'T',
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should be fired for both predictors.
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_1, MiniSessionId, 2)),
            CheckThat(() => AssertDisplayedSuggestions(2, predictorId_2, MiniSessionId, 1)),
            CheckThat(() => Assert.Equal(predictorId_2, _mockedMethods.acceptedPredictorId)),
            CheckThat(() => Assert.Equal("SOME NEW TEXT", _mockedMethods.acceptedSuggestion)),
            CheckThat(() => Assert.Null(_mockedMethods.commandHistory)),
            CheckThat(() => _mockedMethods.ClearPredictionFields()),
            _.UpArrow,
            _.UpArrow,
            CheckThat(() => AssertScreenIs(5,
                TokenClassification.Command, "SOME",
                TokenClassification.None, " NEW TEX SOME TEXT AFTER",
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, " SOME TEXT BEFORE ",
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None,
                new string(' ',
                    listWidth -
                    46), // 46 is the length of '> SOME TEXT BEFORE SOME NEW TEX' plus '[TestPredictor]'.
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.None, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.ListPredictionSelected, ' ',
                emphasisColors, "SOME NEW TEX",
                TokenClassification.ListPredictionSelected, " SOME TEXT AFTER",
                TokenClassification.ListPredictionSelected,
                new string(' ',
                    listWidth - 45), // 45 is the length of '> SOME NEW TEX SOME TEXT AFTER' plus '[TestPredictor]'.
                TokenClassification.ListPredictionSelected, '[',
                TokenClassification.ListPrediction, "TestPredictor",
                TokenClassification.ListPredictionSelected, ']',
                NextLine,
                TokenClassification.ListPrediction, '>',
                TokenClassification.None, ' ',
                emphasisColors, "SOME NEW TEX",
                TokenClassification.None, 'T',
                TokenClassification.None,
                new string(' ', listWidth - 32), // 32 is the length of '> SOME NEW TEXT' plus '[LongNamePred...]'
                TokenClassification.None, '[',
                TokenClassification.ListPrediction, "LongNamePred...",
                TokenClassification.None, ']',
                // List view is done, no more list item following.
                NextLine,
                NextLine
            )),
            // `OnSuggestionDisplayed` should not be fired when navigating the list.
            CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
            // Once accepted, the list should be cleared.
            _.Enter, CheckThat(() => AssertScreenIs(2,
                TokenClassification.Command, "SOME",
                TokenClassification.None, " NEW TEX SOME TEXT AFTER",
                NextLine,
                NextLine))
        ));

        Assert.Empty(_mockedMethods.displayedSuggestions);
        Assert.Equal(predictorId_1, _mockedMethods.acceptedPredictorId);
        Assert.Equal("SOME NEW TEX SOME TEXT AFTER", _mockedMethods.acceptedSuggestion);
        Assert.Equal(4, _mockedMethods.commandHistory.Count);
        Assert.Equal("echo -bar", _mockedMethods.commandHistory[0]);
        Assert.Equal("java", _mockedMethods.commandHistory[1]);
        Assert.Equal("eca -zoo", _mockedMethods.commandHistory[2]);
        Assert.Equal("SOME NEW TEX SOME TEXT AFTER", _mockedMethods.commandHistory[3]);
    }

    [SkippableFact]
    public void List_NoneSource_ExecutionStatus()
    {
        TestSetup(KeyMode.Cmd);
        using var disp = SetPrediction(PredictionSource.None, PredictionViewStyle.ListView);

        // The last accepted command line would be "yay" after this.
        Test("yay", Keys("yay"));
        _mockedMethods.ClearPredictionFields();

        // We always pass in 'true' as the execution status of the last command line,
        // and that feedback will be reported when
        //  1. the plugin source is in use;
        //  2. the last accepted command is not a whitespace string.
        Test("   ", Keys(
            // Since we set the prediction source to be 'None', this feedback won't be reported.
            CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
            "   "));

        Test("abc", Keys(
            // The prediction source is 'None', and the last accepted command is a whitespace string,
            // so this feedback won't be reported.
            CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
            "abc"));

        Assert.Null(_mockedMethods.lastCommandRunStatus);
    }

    [SkippableFact]
    public void List_HistorySource_ExecutionStatus()
    {
        TestSetup(KeyMode.Cmd);
        using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

        // The last accepted command line would be "yay" after this.
        Test("yay", Keys("yay"));
        _mockedMethods.ClearPredictionFields();

        // We always pass in 'true' as the execution status of the last command line,
        // and that feedback will be reported when
        //  1. the plugin source is in use;
        //  2. the last accepted command is not a whitespace string.
        Test("   ", Keys(
            // Since we set the prediction source to be 'History', this feedback won't be reported.
            CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
            "   "));

        Test("abc", Keys(
            // The prediction source is 'History', and the last accepted command is a whitespace string,
            // so this feedback won't be reported.
            CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
            "abc"));

        Assert.Null(_mockedMethods.lastCommandRunStatus);
    }

    [SkippableFact]
    public void List_PluginSource_ExecutionStatus()
    {
        TestSetup(KeyMode.Cmd);
        using var disp = SetPrediction(PredictionSource.Plugin, PredictionViewStyle.InlineView);

        // The last accepted command line would be an empty string after this.
        Test("", Keys(_.Enter));
        _mockedMethods.ClearPredictionFields();

        // We always pass in 'true' as the execution status of the last command line,
        // and that feedback will be reported when
        //  1. the plugin source is in use;
        //  2. the last accepted command is not a whitespace string.
        Test("yay", Keys(
            // The plugin source is in use, but the last accepted command is an empty string.
            CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
            "yay"));

        Assert.Null(_mockedMethods.lastCommandRunStatus);

        // The last accepted command line would be a whitespace string with 3 space characters after this.
        Test("   ", Keys(
            // The plugin source is in use, and the last accepted command is "yay".
            CheckThat(() => Assert.True(_mockedMethods.lastCommandRunStatus)),
            "   "));

        Assert.True(_mockedMethods.lastCommandRunStatus);
        _mockedMethods.ClearPredictionFields();

        Test("abc", Keys(
            // The plugin source is in use, but the last accepted command is a whitespace string.
            CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
            "abc"));

        Assert.Null(_mockedMethods.lastCommandRunStatus);
    }

    [SkippableFact]
    public void List_HistoryAndPluginSource_ExecutionStatus()
    {
        TestSetup(KeyMode.Cmd);
        using var disp = SetPrediction(PredictionSource.HistoryAndPlugin, PredictionViewStyle.InlineView);

        // The last accepted command line would be an empty string after this.
        Test("", Keys(_.Enter));
        _mockedMethods.ClearPredictionFields();

        // We always pass in 'true' as the execution status of the last command line,
        // and that feedback will be reported when
        //  1. the plugin source is in use;
        //  2. the last accepted command is not a whitespace string.
        Test("yay", Keys(
            // The plugin source is in use, but the last accepted command is an empty string.
            CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
            "yay"));

        Assert.Null(_mockedMethods.lastCommandRunStatus);

        // The last accepted command line would be a whitespace string with 3 space characters after this.
        Test("   ", Keys(
            // The plugin source is in use, and the last accepted command is "yay".
            CheckThat(() => Assert.True(_mockedMethods.lastCommandRunStatus)),
            "   "));

        Assert.True(_mockedMethods.lastCommandRunStatus);
        _mockedMethods.ClearPredictionFields();

        Test("abc", Keys(
            // The plugin source is in use, but the last accepted command is a whitespace string.
            CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
            "abc"));

        Assert.Null(_mockedMethods.lastCommandRunStatus);
    }
}