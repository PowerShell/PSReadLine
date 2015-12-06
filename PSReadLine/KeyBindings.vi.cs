using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        public static class BackgroundColorMapper
        {
            private static Dictionary<ConsoleColor, ConsoleColor> map = new Dictionary<ConsoleColor, ConsoleColor>();
            static BackgroundColorMapper()
            {
                map.Add(ConsoleColor.Black, ConsoleColor.DarkGray);
                map.Add(ConsoleColor.Blue, ConsoleColor.DarkBlue);
                map.Add(ConsoleColor.Cyan, ConsoleColor.DarkCyan);
                map.Add(ConsoleColor.DarkBlue, ConsoleColor.Black);
                map.Add(ConsoleColor.DarkCyan, ConsoleColor.Black);
                map.Add(ConsoleColor.DarkGray, ConsoleColor.Black);
                map.Add(ConsoleColor.DarkGreen, ConsoleColor.Black);
                map.Add(ConsoleColor.DarkMagenta, ConsoleColor.Black);
                map.Add(ConsoleColor.DarkRed, ConsoleColor.Black);
                map.Add(ConsoleColor.DarkYellow, ConsoleColor.Black);
                map.Add(ConsoleColor.Gray, ConsoleColor.White);
                map.Add(ConsoleColor.Green, ConsoleColor.DarkGreen);
                map.Add(ConsoleColor.Magenta, ConsoleColor.DarkMagenta);
                map.Add(ConsoleColor.Red, ConsoleColor.DarkRed);
                map.Add(ConsoleColor.White, ConsoleColor.Gray);
                map.Add(ConsoleColor.Yellow, ConsoleColor.DarkYellow);
            }
            public static ConsoleColor AlternateBackground(ConsoleColor bg)
            {
                return map[bg];
            }
        }

        private int _normalCursorSize = 10;
        private ConsoleColor _normalBackground = ConsoleColor.Black;

        private static KeyHandler MakeViKeyHandler(Action<ConsoleKeyInfo?, object> action, string briefDescription, string longDescription = null)
        {
            if (string.IsNullOrWhiteSpace(longDescription))
            {
                longDescription = PSReadLineResources.ResourceManager.GetString(briefDescription + "Description");
            }

            return new KeyHandler
            {
                Action = action,
                BriefDescription = briefDescription,
                LongDescription = longDescription
            };
        }

        #region KeyBindings
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viInsKeyMap = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.Enter,           MakeViKeyHandler(AcceptLine,             "AcceptLine" ) },
                { Keys.CtrlD,           MakeViKeyHandler(ViAcceptLineOrExit,     "ViAcceptLineOrExit" ) },
                { Keys.ShiftEnter,      MakeViKeyHandler(AddLine,                "AddLine") },
                { Keys.Escape,          MakeViKeyHandler(ViCommandMode,          "ViCommandMode") },
                { Keys.LeftArrow,       MakeViKeyHandler(BackwardChar,           "BackwardChar") },
                { Keys.RightArrow,      MakeViKeyHandler(ForwardChar,            "ForwardChar") },
                { Keys.CtrlLeftArrow,   MakeViKeyHandler(BackwardWord,           "BackwardWord") },
                { Keys.CtrlRightArrow,  MakeViKeyHandler(NextWord,               "NextWord") },
                { Keys.UpArrow,         MakeViKeyHandler(PreviousHistory,        "PreviousHistory") },
                { Keys.DownArrow,       MakeViKeyHandler(NextHistory,            "NextHistory") },
                { Keys.Home,            MakeViKeyHandler(BeginningOfLine,        "BeginningOfLine") },
                { Keys.End,             MakeViKeyHandler(EndOfLine,              "EndOfLine") },
                { Keys.Delete,          MakeViKeyHandler(DeleteChar,             "DeleteChar") },
                { Keys.Backspace,       MakeViKeyHandler(BackwardDeleteChar,     "BackwardDeleteChar") },
                { Keys.CtrlSpace,       MakeViKeyHandler(PossibleCompletions,    "PossibleCompletions") },
                { Keys.Tab,             MakeViKeyHandler(ViTabCompleteNext,      "ViTabCompleteNext") },
                { Keys.ShiftTab,        MakeViKeyHandler(ViTabCompletePrevious,  "ViTabCompletePrevious") },
                { Keys.CtrlV,           MakeViKeyHandler(Paste,                  "Paste") },
                { Keys.VolumeDown,      MakeViKeyHandler(Ignore,                 "Ignore") },
                { Keys.VolumeUp,        MakeViKeyHandler(Ignore,                 "Ignore") },
                { Keys.VolumeMute,      MakeViKeyHandler(Ignore,                 "Ignore") },
                { Keys.CtrlC,           MakeViKeyHandler(CancelLine,             "CancelLine") },
                { Keys.CtrlL,           MakeViKeyHandler(ClearScreen,            "ClearScreen") },
                { Keys.CtrlY,           MakeViKeyHandler(Redo,                   "Redo") },
                { Keys.CtrlZ,           MakeViKeyHandler(Undo,                   "Undo") },
                { Keys.CtrlBackspace,   MakeViKeyHandler(BackwardKillWord,       "BackwardKillWord") },
                { Keys.CtrlDelete,      MakeViKeyHandler(KillWord,               "KillWord") },
                { Keys.CtrlEnd,         MakeViKeyHandler(ForwardDeleteLine,      "ForwardDeleteLine") },
                { Keys.CtrlHome,        MakeViKeyHandler(BackwardDeleteLine,     "BackwardDeleteLine") },
                { Keys.CtrlRBracket,    MakeViKeyHandler(GotoBrace,              "GotoBrace") },
                { Keys.F3,              MakeViKeyHandler(CharacterSearch,        "CharacterSearch") },
                { Keys.ShiftF3,         MakeViKeyHandler(CharacterSearchBackward,"CharacterSearchBackward") },
                { Keys.CtrlAltQuestion, MakeViKeyHandler(ShowKeyBindings,        "ShowKeyBindings") }
            };
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viCmdKeyMap = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.Enter,           MakeViKeyHandler(ViAcceptLine,         "ViAcceptLine") },
                { Keys.CtrlD,           MakeViKeyHandler(ViAcceptLineOrExit,   "ViAcceptLineOrExit") },
                { Keys.ShiftEnter,      MakeViKeyHandler(AddLine,              "AddLine") },
                { Keys.Escape,          MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.LeftArrow,       MakeViKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.RightArrow,      MakeViKeyHandler(ForwardChar,          "ForwardChar") },
                { Keys.Space,           MakeViKeyHandler(ForwardChar,          "ForwardChar") },
                { Keys.CtrlLeftArrow,   MakeViKeyHandler(BackwardWord,         "BackwardWord") },
                { Keys.CtrlRightArrow,  MakeViKeyHandler(NextWord,             "NextWord") },
                { Keys.UpArrow,         MakeViKeyHandler(PreviousHistory,      "PreviousHistory") },
                { Keys.DownArrow,       MakeViKeyHandler(NextHistory,          "NextHistory") },
                { Keys.Home,            MakeViKeyHandler(BeginningOfLine,      "BeginningOfLine") },
                { Keys.End,             MakeViKeyHandler(MoveToEndOfLine,      "MoveToEndOfLine") },
                { Keys.Delete,          MakeViKeyHandler(DeleteChar,           "DeleteChar") },
                { Keys.Backspace,       MakeViKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.CtrlSpace,       MakeViKeyHandler(PossibleCompletions,  "PossibleCompletions") },
                { Keys.Tab,             MakeViKeyHandler(TabCompleteNext,      "TabCompleteNext") },
                { Keys.ShiftTab,        MakeViKeyHandler(TabCompletePrevious,  "TabCompletePrevious") },
                { Keys.CtrlV,           MakeViKeyHandler(Paste,                "Paste") },
                { Keys.VolumeDown,      MakeViKeyHandler(Ignore,               "Ignore") },
                { Keys.VolumeUp,        MakeViKeyHandler(Ignore,               "Ignore") },
                { Keys.VolumeMute,      MakeViKeyHandler(Ignore,               "Ignore") },
                { Keys.CtrlC,           MakeViKeyHandler(CancelLine,           "CancelLine") },
                { Keys.CtrlL,           MakeViKeyHandler(ClearScreen,          "ClearScreen") },
                { Keys.CtrlT,           MakeViKeyHandler(SwapCharacters,       "SwapCharacters") },
                { Keys.CtrlU,           MakeViKeyHandler(BackwardDeleteLine,   "BackwardDeleteLine") },      
                { Keys.CtrlW,           MakeViKeyHandler(BackwardDeleteWord,   "BackwardDeleteWord") },
                { Keys.CtrlY,           MakeViKeyHandler(Redo,                 "Redo") },
                { Keys.CtrlZ,           MakeViKeyHandler(Undo,                 "Undo") },
                { Keys.CtrlBackspace,   MakeViKeyHandler(BackwardKillWord,     "BackwardKillWord") },
                { Keys.CtrlDelete,      MakeViKeyHandler(KillWord,             "KillWord") },
                { Keys.CtrlEnd,         MakeViKeyHandler(ForwardDeleteLine,    "ForwardDeleteLine") },
                { Keys.CtrlHome,        MakeViKeyHandler(BackwardDeleteLine,   "BackwardDeleteLine") },
                { Keys.CtrlRBracket,    MakeViKeyHandler(GotoBrace,            "GotoBrace") },
                { Keys.F3,              MakeViKeyHandler(CharacterSearch,      "CharacterSearch") },
                { Keys.ShiftF3,         MakeViKeyHandler(CharacterSearchBackward, "CharacterSearchBackward") },
                { Keys.A,               MakeViKeyHandler(ViInsertWithAppend,   "ViInsertWithAppend") },
                { Keys.B,               MakeViKeyHandler(ViBackwardWord,       "ViBackwardWord") },
                { Keys.C,               MakeViKeyHandler(ViChord,                "ChordFirstKey") },
                { Keys.D,               MakeViKeyHandler(ViChord,                "ChordFirstKey") },
                { Keys.E,               MakeViKeyHandler(NextWordEnd,          "NextWordEnd") },
                { Keys.F,               MakeViKeyHandler(SearchChar,           "SearchChar") },
                { Keys.G,               MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.H,               MakeViKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.I,               MakeViKeyHandler(ViInsertMode,         "ViInsertMode") },
                { Keys.J,               MakeViKeyHandler(NextHistory,          "NextHistory") },
                { Keys.K,               MakeViKeyHandler(PreviousHistory,      "PreviousHistory") },
                { Keys.L,               MakeViKeyHandler(ForwardChar,          "ForwardChar") },
                { Keys.M,               MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.N,               MakeViKeyHandler(RepeatSearch,         "RepeatSearch") },
                { Keys.O,               MakeViKeyHandler(ViAppendLine,         "ViAppendLine") },
                { Keys.P,               MakeViKeyHandler(PasteAfter,           "PasteAfter") },
                { Keys.Q,               MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.R,               MakeViKeyHandler(ReplaceCharInPlace,   "ReplaceCharInPlace") },
                { Keys.S,               MakeViKeyHandler(ViInsertWithDelete,   "ViInsertWithDelete") },
                { Keys.T,               MakeViKeyHandler(SearchCharWithBackoff,"SearchCharWithBackoff") },
                { Keys.U,               MakeViKeyHandler(Undo,                 "Undo") },
                { Keys.V,               MakeViKeyHandler(ViEditVisually,       "ViEditVisually") },
                { Keys.W,               MakeViKeyHandler(ViNextWord,           "ViNextWord") },
                { Keys.X,               MakeViKeyHandler(DeleteChar,           "DeleteChar") },
                { Keys.Y,               MakeViKeyHandler(ViChord,                "ChordFirstKey") },
                { Keys.Z,               MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.ucA,             MakeViKeyHandler(ViInsertAtEnd,        "ViInsertAtEnd") },
                { Keys.ucB,             MakeViKeyHandler(ViBackwardGlob,       "ViBackwardGlob") },
                { Keys.ucC,             MakeViKeyHandler(ViReplaceToEnd,       "ViReplaceToEnd") },
                { Keys.ucD,             MakeViKeyHandler(DeleteToEnd,          "DeleteToEnd") },
                { Keys.ucE,             MakeViKeyHandler(ViEndOfGlob,          "ViEndOfGlob") },
                { Keys.ucF,             MakeViKeyHandler(SearchCharBackward,   "SearchCharBackward") },
                { Keys.ucG,             MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.ucH,             MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.ucI,             MakeViKeyHandler(ViInsertAtBegining,   "ViInsertAtBegining") },
                { Keys.ucJ,             MakeViKeyHandler(ViJoinLines,          "ViJoinLines") },
                { Keys.ucK,             MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.ucL,             MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.ucM,             MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.ucN,             MakeViKeyHandler(RepeatSearchBackward, "RepeatSearchBackward") },
                { Keys.ucO,             MakeViKeyHandler(ViInsertLine,         "ViInsertLine") },
                { Keys.ucP,             MakeViKeyHandler(PasteBefore,          "PasteBefore") },
                { Keys.ucQ,             MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.ucR,             MakeViKeyHandler(ViReplaceUntilEsc,    "ViReplaceUntilEsc") },
                { Keys.ucS,             MakeViKeyHandler(ViReplaceLine,        "ViReplaceLine") },
                { Keys.ucT,             MakeViKeyHandler(SearchCharBackwardWithBackoff, "SearchCharBackwardWithBackoff") },
                { Keys.ucU,             MakeViKeyHandler(UndoAll,              "UndoAll") },
                { Keys.ucV,             MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.ucW,             MakeViKeyHandler(ViNextGlob,           "ViNextGlob") },
                { Keys.ucX,             MakeViKeyHandler(BackwardDeleteChar,   "BackwardDeleteChar") },
                { Keys.ucY,             MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys.ucZ,             MakeViKeyHandler(Ding,                 "Ignore") },
                { Keys._0,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._1,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._2,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._3,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._4,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._5,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._6,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._7,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._8,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._9,              MakeViKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Dollar,          MakeViKeyHandler(MoveToEndOfLine,      "MoveToEndOfLine") },
                { Keys.Percent,         MakeViKeyHandler(ViGotoBrace,          "ViGotoBrace") },
                { Keys.Pound,           MakeViKeyHandler(PrependAndAccept,     "PrependAndAccept") },
                { Keys.Pipe,            MakeViKeyHandler(GotoColumn,           "GotoColumn") },
                { Keys.Uphat,           MakeViKeyHandler(GotoFirstNonBlankOfLine, "GotoFirstNonBlankOfLine") },
                { Keys.Tilde,           MakeViKeyHandler(InvertCase,           "InvertCase") },
                { Keys.Slash,           MakeViKeyHandler(SearchBackward,       "SearchBackward") },
                { Keys.CtrlR,           MakeViKeyHandler(SearchCharBackward,   "SearchCharBackward") },
                { Keys.Question,        MakeViKeyHandler(SearchForward,        "SearchForward") },
                { Keys.CtrlS,           MakeViKeyHandler(SearchForward,        "SearchForward") },
                { Keys.Plus,            MakeViKeyHandler(NextHistory,          "NextHistory") },
                { Keys.Minus,           MakeViKeyHandler(PreviousHistory,      "PreviousHistory") },
                { Keys.Period,          MakeViKeyHandler(RepeatLastCommand,    "RepeatLastCommand") },
                { Keys.Semicolon,       MakeViKeyHandler(RepeatLastCharSearch, "RepeatLastCharSearch") },
                { Keys.Comma,           MakeViKeyHandler(RepeatLastCharSearchBackwards, "RepeatLastCharSearchBackwards") }
            };

        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viChordDTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.D,               MakeViKeyHandler( DeleteLine,                   "DeleteLine") },
                { Keys.Dollar,          MakeViKeyHandler( DeleteToEnd,                  "DeleteToEnd") },
                { Keys.B,               MakeViKeyHandler( BackwardDeleteWord,           "BackwardDeleteWord") },
                { Keys.ucB,             MakeViKeyHandler( ViBackwardDeleteGlob,         "ViBackwardDeleteGlob") },
                { Keys.W,               MakeViKeyHandler( DeleteWord,                   "DeleteWord") },
                { Keys.ucW,             MakeViKeyHandler( ViDeleteGlob,                 "ViDeleteGlob") },
                { Keys.E,               MakeViKeyHandler( DeleteEndOfWord,              "DeleteEndOfWord") },
                { Keys.ucE,             MakeViKeyHandler( ViDeleteEndOfGlob,            "ViDeleteEndOfGlob") },
                { Keys.H,               MakeViKeyHandler( BackwardDeleteChar,           "BackwardDeleteChar") },
                { Keys.L,               MakeViKeyHandler( DeleteChar,                   "DeleteChar") },
                { Keys.Space,           MakeViKeyHandler( DeleteChar,                   "DeleteChar") },
                { Keys._0,              MakeViKeyHandler( BackwardDeleteLine,           "BackwardDeleteLine") },
                { Keys.Uphat,           MakeViKeyHandler( DeleteLineToFirstChar,        "DeleteLineToFirstChar") },
                { Keys.Percent,         MakeViKeyHandler( ViDeleteBrace,                "DeleteBrace") },
                { Keys.F,               MakeViKeyHandler( ViDeleteToChar,               "ViDeleteToChar") },
                { Keys.ucF,             MakeViKeyHandler( ViDeleteToCharBackward,       "ViDeleteToCharBackward") },
                { Keys.T,               MakeViKeyHandler( ViDeleteToBeforeChar,         "ViDeleteToBeforeChar") },
                { Keys.ucT,             MakeViKeyHandler( ViDeleteToBeforeCharBackward, "ViDeleteToBeforeCharBackward") },
            };
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viChordCTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.C,               MakeViKeyHandler( ViReplaceLine,                    "ViReplaceLine") },
                { Keys.Dollar,          MakeViKeyHandler( ViReplaceToEnd,                   "ViReplaceToEnd") },
                { Keys.B,               MakeViKeyHandler( ViBackwardReplaceWord,            "ViBackwardReplaceWord") },
                { Keys.ucB,             MakeViKeyHandler( ViBackwardReplaceGlob,            "ViBackwardReplaceGlob") },
                { Keys.W,               MakeViKeyHandler( ViReplaceWord,                    "ViReplaceWord") },
                { Keys.ucW,             MakeViKeyHandler( ViReplaceGlob,                    "ViReplaceGlob") },
                { Keys.E,               MakeViKeyHandler( ViReplaceEndOfWord,               "ViReplaceEndOfWord") },
                { Keys.ucE,             MakeViKeyHandler( ViReplaceEndOfGlob,               "ViReplaceEndOfGlob") },
                { Keys.H,               MakeViKeyHandler( BackwardReplaceChar,              "BackwardReplaceChar") },
                { Keys.L,               MakeViKeyHandler( ReplaceChar,                      "ReplaceChar") },
                { Keys.Space,           MakeViKeyHandler( ReplaceChar,                      "ReplaceChar") },
                { Keys._0,              MakeViKeyHandler( ViBackwardReplaceLine,            "ViBackwardReplaceLine") },
                { Keys.Uphat,           MakeViKeyHandler( ViBackwardReplaceLineToFirstChar, "ViBackwardReplaceLineToFirstChar") },
                { Keys.Percent,         MakeViKeyHandler( ViReplaceBrace,                   "ViReplaceBrace") },
                { Keys.F,               MakeViKeyHandler( ViReplaceToChar,                  "ViReplaceToChar") },
                { Keys.ucF,             MakeViKeyHandler( ViReplaceToCharBackward,          "ViReplaceToCharBackward") },
                { Keys.T,               MakeViKeyHandler( ViReplaceToBeforeChar,            "ViReplaceToBeforeChar") },
                { Keys.ucT,             MakeViKeyHandler( ViReplaceToBeforeCharBackward,    "ViReplaceToBeforeCharBackward") },
            };
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viChordYTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.Y,               MakeViKeyHandler( ViYankLine,            "ViYankLine") },
                { Keys.Dollar,          MakeViKeyHandler( ViYankToEndOfLine,     "ViYankToEndOfLine") },
                { Keys.B,               MakeViKeyHandler( ViYankPreviousWord,    "ViYankPreviousWord") },
                { Keys.ucB,             MakeViKeyHandler( ViYankPreviousGlob,    "ViYankPreviousGlob") },
                { Keys.W,               MakeViKeyHandler( ViYankNextWord,        "ViYankNextWord") },
                { Keys.ucW,             MakeViKeyHandler( ViYankNextGlob,        "ViYankNextGlob") },
                { Keys.E,               MakeViKeyHandler( ViYankEndOfWord,       "ViYankEndOfWord") },
                { Keys.ucE,             MakeViKeyHandler( ViYankEndOfGlob,       "ViYankEndOfGlob") },
                { Keys.H,               MakeViKeyHandler( ViYankLeft,            "ViYankLeft") },
                { Keys.L,               MakeViKeyHandler( ViYankRight,           "ViYankRight") },
                { Keys.Space,           MakeViKeyHandler( ViYankRight,           "ViYankRight") },
                { Keys._0,              MakeViKeyHandler( ViYankBeginningOfLine, "ViYankBeginningOfLine") },
                { Keys.Uphat,           MakeViKeyHandler( ViYankToFirstChar,     "ViYankToFirstChar") },
                { Keys.Percent,         MakeViKeyHandler( ViYankPercent,         "ViYankPercent") },
            };

        private static readonly Dictionary<ConsoleKeyInfo, Dictionary<ConsoleKeyInfo, KeyHandler>> _viCmdChordTable = new Dictionary<ConsoleKeyInfo, Dictionary<ConsoleKeyInfo, KeyHandler>>();
        private static readonly Dictionary<ConsoleKeyInfo, Dictionary<ConsoleKeyInfo, KeyHandler>> _viInsChordTable = new Dictionary<ConsoleKeyInfo, Dictionary<ConsoleKeyInfo, KeyHandler>>();
        #endregion KeyBindings

        /// <summary>
        /// Sets up the key bindings for vi operations.
        /// </summary>
        private void SetDefaultViBindings()
        {
            _dispatchTable = _viInsKeyMap;
            if (_chordDispatchTable == null)
            {
                _chordDispatchTable = _viInsChordTable;
            }
            _viCmdChordTable[Keys.D] = _viChordDTable;
            _viCmdChordTable[Keys.C] = _viChordCTable;
            _viCmdChordTable[Keys.Y] = _viChordYTable;

            _normalCursorSize = _console.CursorSize;
            _normalBackground = _console.BackgroundColor;
        }
    }
}
