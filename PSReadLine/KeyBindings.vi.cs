using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        private static KeyHandler MakeViKeyHandler(Action<ConsoleKeyInfo?, object> action, string briefDescription, string longDescription = null, string mode = "Ins")
        {
            if (string.IsNullOrWhiteSpace(longDescription))
            {
                longDescription = PSReadLineResources.ResourceManager.GetString(briefDescription + "Description");
            }

            return new KeyHandler
            {
                Action = action,
                BriefDescription = briefDescription,
                LongDescription = longDescription,
                Mode = mode
            };
        }

        #region KeyBindings
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viInsKeyMap = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.Enter,           MakeViKeyHandler(AcceptLine,             "AcceptLine" ) },
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
                { Keys.Tab,             MakeViKeyHandler(TabCompleteNext,        "TabCompleteNext") },
                { Keys.ShiftTab,        MakeViKeyHandler(TabCompletePrevious,    "TabCompletePrevious") },
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
                { Keys.Enter,           MakeViKeyHandler(ViAcceptLine,         "ViAcceptLine", mode: "Cmd") },
                { Keys.ShiftEnter,      MakeViKeyHandler(AddLine,              "AddLine", mode: "Cmd") },
                { Keys.Escape,          MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.LeftArrow,       MakeViKeyHandler(BackwardChar,         "BackwardChar", mode: "Cmd") },
                { Keys.RightArrow,      MakeViKeyHandler(ForwardChar,          "ForwardChar", mode: "Cmd") },
                { Keys.Space,           MakeViKeyHandler(ForwardChar,          "ForwardChar", mode: "Cmd") },
                { Keys.CtrlLeftArrow,   MakeViKeyHandler(BackwardWord,         "BackwardWord", mode: "Cmd") },
                { Keys.CtrlRightArrow,  MakeViKeyHandler(NextWord,             "NextWord", mode: "Cmd") },
                { Keys.UpArrow,         MakeViKeyHandler(PreviousHistory,      "PreviousHistory", mode: "Cmd") },
                { Keys.DownArrow,       MakeViKeyHandler(NextHistory,          "NextHistory", mode: "Cmd") },
                { Keys.Home,            MakeViKeyHandler(BeginningOfLine,      "BeginningOfLine", mode: "Cmd") },
                { Keys.End,             MakeViKeyHandler(MoveToEndOfLine,      "MoveToEndOfLine", mode: "Cmd") },
                { Keys.Delete,          MakeViKeyHandler(DeleteChar,           "DeleteChar", mode: "Cmd") },
                { Keys.Backspace,       MakeViKeyHandler(BackwardChar,         "BackwardChar", mode: "Cmd") },
                { Keys.CtrlSpace,       MakeViKeyHandler(PossibleCompletions,  "PossibleCompletions", mode: "Cmd") },
                { Keys.Tab,             MakeViKeyHandler(TabCompleteNext,      "TabCompleteNext", mode: "Cmd") },
                { Keys.ShiftTab,        MakeViKeyHandler(TabCompletePrevious,  "TabCompletePrevious", mode: "Cmd") },
                { Keys.CtrlV,           MakeViKeyHandler(Paste,                "Paste", mode: "Cmd") },
                { Keys.VolumeDown,      MakeViKeyHandler(Ignore,               "Ignore", mode: "Cmd") },
                { Keys.VolumeUp,        MakeViKeyHandler(Ignore,               "Ignore", mode: "Cmd") },
                { Keys.VolumeMute,      MakeViKeyHandler(Ignore,               "Ignore", mode: "Cmd") },
                { Keys.CtrlC,           MakeViKeyHandler(CancelLine,           "CancelLine", mode: "Cmd") },
                { Keys.CtrlL,           MakeViKeyHandler(ClearScreen,          "ClearScreen", mode: "Cmd") },
                { Keys.CtrlT,           MakeViKeyHandler(SwapCharacters,       "SwapCharacters", mode: "Cmd") },
                { Keys.CtrlU,           MakeViKeyHandler(BackwardDeleteLine,   "BackwardDeleteLine", mode: "Cmd") },      
                { Keys.CtrlW,           MakeViKeyHandler(BackwardDeleteWord,   "BackwardDeleteWord", mode: "Cmd") },
                { Keys.CtrlY,           MakeViKeyHandler(Redo,                 "Redo", mode: "Cmd") },
                { Keys.CtrlZ,           MakeViKeyHandler(Undo,                 "Undo", mode: "Cmd") },
                { Keys.CtrlBackspace,   MakeViKeyHandler(BackwardKillWord,     "BackwardKillWord", mode: "Cmd") },
                { Keys.CtrlDelete,      MakeViKeyHandler(KillWord,             "KillWord", mode: "Cmd") },
                { Keys.CtrlEnd,         MakeViKeyHandler(ForwardDeleteLine,    "ForwardDeleteLine", mode: "Cmd") },
                { Keys.CtrlHome,        MakeViKeyHandler(BackwardDeleteLine,   "BackwardDeleteLine", mode: "Cmd") },
                { Keys.CtrlRBracket,    MakeViKeyHandler(GotoBrace,            "GotoBrace", mode: "Cmd") },
                { Keys.F3,              MakeViKeyHandler(CharacterSearch,      "CharacterSearch", mode: "Cmd") },
                { Keys.ShiftF3,         MakeViKeyHandler(CharacterSearchBackward, "CharacterSearchBackward", mode: "Cmd") },
                { Keys.A,               MakeViKeyHandler(ViInsertWithAppend,   "ViInsertWithAppend", mode: "Cmd") },
                { Keys.B,               MakeViKeyHandler(BackwardWord,         "BackwardWord", mode: "Cmd") },
                { Keys.C,               MakeViKeyHandler(Chord,                "ChordFirstKey", mode: "Cmd") },
                { Keys.D,               MakeViKeyHandler(Chord,                "ChordFirstKey", mode: "Cmd") },
                { Keys.E,               MakeViKeyHandler(NextWordEnd,          "NextWordEnd", mode: "Cmd") },
                { Keys.F,               MakeViKeyHandler(SearchChar,           "SearchChar", mode: "Cmd") },
                { Keys.G,               MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.H,               MakeViKeyHandler(BackwardChar,         "BackwardChar", mode: "Cmd") },
                { Keys.I,               MakeViKeyHandler(ViInsertMode,         "ViInsertMode", mode: "Cmd") },
                { Keys.J,               MakeViKeyHandler(NextHistory,          "NextHistory", mode: "Cmd") },
                { Keys.K,               MakeViKeyHandler(PreviousHistory,      "PreviousHistory", mode: "Cmd") },
                { Keys.L,               MakeViKeyHandler(ForwardChar,          "ForwardChar", mode: "Cmd") },
                { Keys.M,               MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.N,               MakeViKeyHandler(RepeatSearch,         "RepeatSearch", mode: "Cmd") },
                { Keys.O,               MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.P,               MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.Q,               MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.R,               MakeViKeyHandler(ReplaceCharInPlace,   "ReplaceCharInPlace", mode: "Cmd") },
                { Keys.S,               MakeViKeyHandler(ViInsertWithDelete,   "ViInsertWithDelete", mode: "Cmd") },
                { Keys.T,               MakeViKeyHandler(SearchCharWithBackoff,"SearchCharWithBackoff", mode: "Cmd") },
                { Keys.U,               MakeViKeyHandler(Undo,                 "Undo", mode: "Cmd") },
                { Keys.V,               MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.W,               MakeViKeyHandler(NextWord,             "NextWord", mode: "Cmd") },
                { Keys.X,               MakeViKeyHandler(DeleteChar,           "DeleteChar", mode: "Cmd") },
                { Keys.Y,               MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.Z,               MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucA,             MakeViKeyHandler(ViInsertAtEnd,        "ViInsertAtEnd", mode: "Cmd") },
                { Keys.ucB,             MakeViKeyHandler(BackwardWord,         "BackwardWord", mode: "Cmd") },
                { Keys.ucC,             MakeViKeyHandler(ViReplaceToEnd,       "ViReplaceToEnd", mode: "Cmd") },
                { Keys.ucD,             MakeViKeyHandler(DeleteToEnd,          "DeleteToEnd", mode: "Cmd") },
                { Keys.ucE,             MakeViKeyHandler(NextWordEnd,          "NextWordEnd", mode: "Cmd") },
                { Keys.ucF,             MakeViKeyHandler(SearchCharBackward,   "SearchCharBackward", mode: "Cmd") },
                { Keys.ucG,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucH,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucI,             MakeViKeyHandler(ViInsertAtBegining,   "ViInsertAtBegining", mode: "Cmd") },
                { Keys.ucJ,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucK,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucL,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucM,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucN,             MakeViKeyHandler(RepeatSearchBackward, "RepeatSearchBackward", mode: "Cmd") },
                { Keys.ucO,             MakeViKeyHandler(BeginningOfLine,      "BeginningOfLine", mode: "Cmd") },
                { Keys.ucP,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucQ,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucR,             MakeViKeyHandler(ViReplaceUntilEsc,    "ViReplaceUntilEsc", mode: "Cmd") },
                { Keys.ucS,             MakeViKeyHandler(ViReplaceLine,        "ViReplaceLine", mode: "Cmd") },
                { Keys.ucT,             MakeViKeyHandler(SearchCharBackwardWithBackoff, "SearchCharBackwardWithBackoff", mode: "Cmd") },
                { Keys.ucU,             MakeViKeyHandler(UndoAll,              "UndoAll", mode: "Cmd") },
                { Keys.ucV,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucW,             MakeViKeyHandler(NextWord,             "NextWord", mode: "Cmd") },
                { Keys.ucX,             MakeViKeyHandler(BackwardDeleteChar,   "BackwardDeleteChar", mode: "Cmd") },
                { Keys.ucY,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys.ucZ,             MakeViKeyHandler(Ding,                 "Ignore", mode: "Cmd") },
                { Keys._0,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys._1,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys._2,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys._3,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys._4,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys._5,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys._6,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys._7,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys._8,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys._9,              MakeViKeyHandler(DigitArgument,        "DigitArgument", mode: "Cmd") },
                { Keys.Dollar,          MakeViKeyHandler(MoveToEndOfLine,      "MoveToEndOfLine", mode: "Cmd") },
                { Keys.Percent,         MakeViKeyHandler(GotoBrace,            "GotoBrace", mode: "Cmd") },
                { Keys.Pound,           MakeViKeyHandler(PrependAndAccept,     "PrependAndAccept", mode: "Cmd") },
                { Keys.Pipe,            MakeViKeyHandler(GotoColumn,           "GotoColumn", mode: "Cmd") },
                { Keys.Uphat,           MakeViKeyHandler(GotoFirstNonBlankOfLine, "GotoFirstNonBlankOfLine", mode: "Cmd") },
                { Keys.Tilde,           MakeViKeyHandler(InvertCase,           "InvertCase", mode: "Cmd") },
                { Keys.Slash,           MakeViKeyHandler(SearchBackward,       "SearchBackward", mode: "Cmd") },
                { Keys.CtrlR,           MakeViKeyHandler(SearchCharBackward,   "SearchCharBackward", mode: "Cmd") },
                { Keys.Question,        MakeViKeyHandler(SearchForward,        "SearchForward", mode: "Cmd") },
                { Keys.CtrlS,           MakeViKeyHandler(SearchForward,        "SearchForward", mode: "Cmd") },
                { Keys.Period,          MakeViKeyHandler(RepeatLastCommand,    "RepeatLastCommand", mode: "Cmd") },
                { Keys.Semicolon,       MakeViKeyHandler(RepeatLastCharSearch, "RepeatLastCharSearch", mode: "Cmd") },
                { Keys.Comma,           MakeViKeyHandler(RepeatLastCharSearchBackwards, "RepeatLastCharSearchBackwards", mode: "Cmd") }
            };

        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viChordDTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.D,               MakeViKeyHandler( DeleteLine,           "DeleteLine", mode: "Cmd") },
                { Keys.Dollar,          MakeViKeyHandler( DeleteToEnd,          "DeleteToEnd", mode: "Cmd") },
                { Keys.B,               MakeViKeyHandler( BackwardDeleteWord,   "BackwardDeleteWord", mode: "Cmd") },
                { Keys.ucB,             MakeViKeyHandler( BackwardDeleteWord,   "BackwardDeleteWord", mode: "Cmd") },
                { Keys.W,               MakeViKeyHandler( DeleteWord,           "DeleteWord", mode: "Cmd") },
                { Keys.ucW,             MakeViKeyHandler( DeleteWord,           "DeleteWord", mode: "Cmd") },
                { Keys.E,               MakeViKeyHandler( DeleteToEndOfWord,    "DeleteToEndOfWord", mode: "Cmd") },
                { Keys.ucE,             MakeViKeyHandler( DeleteToEndOfWord,    "DeleteToEndOfWord", mode: "Cmd") },
                { Keys.H,               MakeViKeyHandler( BackwardDeleteChar,   "BackwardDeleteChar", mode: "Cmd") },
                { Keys.L,               MakeViKeyHandler( DeleteChar,           "DeleteChar", mode: "Cmd") },
                { Keys.Space,           MakeViKeyHandler( DeleteChar,           "DeleteChar", mode: "Cmd") },
                { Keys._0,              MakeViKeyHandler( BackwardDeleteLine,   "BackwardDeleteLine", mode: "Cmd") },
                { Keys.Uphat,           MakeViKeyHandler( DeleteLineToFirstChar,"DeleteLineToFirstChar", mode: "Cmd") },
                { Keys.Percent,         MakeViKeyHandler( DeleteBrace,          "DeleteBrace", mode: "Cmd") }
            };
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viChordCTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.C,               MakeViKeyHandler( ViReplaceLine,         "ViReplaceLine", mode: "Cmd") },
                { Keys.Dollar,          MakeViKeyHandler( ViReplaceToEnd,        "ViReplaceToEnd", mode: "Cmd") },
                { Keys.B,               MakeViKeyHandler( ViBackwardReplaceWord, "ViBackwardReplaceWord", mode: "Cmd") },
                { Keys.ucB,             MakeViKeyHandler( ViBackwardReplaceWord, "ViBackwardReplaceWord", mode: "Cmd") },
                { Keys.W,               MakeViKeyHandler( ViReplaceWord,         "ViReplaceWord", mode: "Cmd") },
                { Keys.ucW,             MakeViKeyHandler( ViReplaceWord,         "ViReplaceWord", mode: "Cmd") },
                { Keys.E,               MakeViKeyHandler( ViReplaceWord,         "ViReplaceWord", mode: "Cmd") },
                { Keys.ucE,             MakeViKeyHandler( ViReplaceWord,         "ViReplaceWord", mode: "Cmd") },
                { Keys.H,               MakeViKeyHandler( BackwardReplaceChar,   "BackwardReplaceChar", mode: "Cmd") },
                { Keys.L,               MakeViKeyHandler( ReplaceChar,           "ReplaceChar", mode: "Cmd") },
                { Keys.Space,           MakeViKeyHandler( ReplaceChar,           "ReplaceChar", mode: "Cmd") },
                { Keys._0,              MakeViKeyHandler( ViBackwardReplaceLine, "ViBackwardReplaceLine", mode: "Cmd") },
                { Keys.Uphat,           MakeViKeyHandler( ViBackwardReplaceLineToFirstChar, "ViBackwardReplaceLineToFirstChar", mode: "Cmd") },
                { Keys.Percent,         MakeViKeyHandler( ViReplaceBrace,        "ViReplaceBrace", mode: "Cmd") },
            };
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viChordYTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.D,               MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.Dollar,          MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.B,               MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.ucB,             MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.W,               MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.ucW,             MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.E,               MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.ucE,             MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.H,               MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.L,               MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.Space,           MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys._0,              MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.Uphat,           MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") },
                { Keys.Percent,         MakeViKeyHandler( Ding,        "Ignore", mode: "Cmd") }
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

        }
    }
}
