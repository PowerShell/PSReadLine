/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{
    /// <summary>
    /// A group used for sorting key handlers.
    /// </summary>
    public enum KeyHandlerGroup
    {
        /// <summary>Basic editing functions</summary>
        Basic,
        /// <summary>Cursor movement functions</summary>
        CursorMovement,
        /// <summary>History functions</summary>
        History,
        /// <summary>Completion functions</summary>
        Completion,
        /// <summary>Miscellaneous functions</summary>
        Miscellaneous,
        /// <summary>Selection functions</summary>
        Selection,
        /// <summary>Search functions</summary>
        Search,
        /// <summary>User defined functions</summary>
        Custom
    }

    /// <summary>
    /// The class is used as the output type for the cmdlet Get-PSReadLineKeyHandler
    /// </summary>
    public class KeyHandler
    {
        /// <summary>
        /// The key that is bound or unbound.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The name of the function that a key is bound to, if any.
        /// </summary>
        public string Function { get; set; }

        /// <summary>
        /// A short description of the behavior of the function.
        /// </summary>
        public string Description
        {
            get
            {
                var result = _description;
                if (string.IsNullOrWhiteSpace(result))
                    result = PSReadLineResources.ResourceManager.GetString(Function + "Description");
                if (string.IsNullOrWhiteSpace(result))
                    result = Function;
                return result;
            }
            set => _description = value;
        }
        private string _description;

        /// <summary>
        /// The group that this key handler belongs to.
        /// </summary>
        public KeyHandlerGroup Group { get; set; }

        /// <summary>
        /// Get the description of the group.
        /// </summary>
        public static string GetGroupingDescription(KeyHandlerGroup grouping)
        {
            switch (grouping)
            {
            case KeyHandlerGroup.Basic:
                return PSReadLineResources.BasicGrouping;
            case KeyHandlerGroup.CursorMovement:
                return PSReadLineResources.CursorMovementGrouping;
            case KeyHandlerGroup.History:
                return PSReadLineResources.HistoryGrouping;
            case KeyHandlerGroup.Completion:
                return PSReadLineResources.CompletionGrouping;
            case KeyHandlerGroup.Miscellaneous:
                return PSReadLineResources.MiscellaneousGrouping;
            case KeyHandlerGroup.Selection:
                return PSReadLineResources.SelectionGrouping;
            case KeyHandlerGroup.Search:
                return PSReadLineResources.SearchGrouping;
            case KeyHandlerGroup.Custom:
                return PSReadLineResources.CustomGrouping;
            default: return "";
            }
        }

    }

    public partial class PSConsoleReadLine
    {
        class KeyHandler
        {
            // Each key handler will be passed 2 arguments.  Most will ignore these arguments,
            // but having a consistent signature greatly simplifies dispatch.  Defaults
            // should be included on all handlers that ignore their parameters so they
            // can be called from PowerShell without passing anything.
            //
            // The first argument is the key that caused the action to be called
            // (the second key when it's a 2 key chord).  The default is null (it's nullable)
            // because PowerShell can't handle default(PSKeyInfo) as a default.
            // Most actions will ignore this argument.
            //
            // The second argument is an arbitrary object.  It will usually be either a number
            // (e.g. as a repeat count) or a string.  Most actions will ignore this argument.
            public Action<ConsoleKeyInfo?, object> Action;
            public string BriefDescription;
            public string LongDescription
            {
                get => _longDescription ??
                       (_longDescription =
                           PSReadLineResources.ResourceManager.GetString(BriefDescription + "Description"));
                set => _longDescription = value;
            }
            private string _longDescription;
            public ScriptBlock ScriptBlock;

            public override string ToString()
            {
                return BriefDescription;
            }
        }

        static KeyHandler MakeKeyHandler(Action<ConsoleKeyInfo?, object> action, string briefDescription, string longDescription = null, ScriptBlock scriptBlock = null)
        {
            return new KeyHandler
            {
                Action = action,
                BriefDescription = briefDescription,
                LongDescription = longDescription,
                ScriptBlock = scriptBlock,
            };
        }

        private Dictionary<PSKeyInfo, KeyHandler> _dispatchTable;
        private Dictionary<PSKeyInfo, Dictionary<PSKeyInfo, KeyHandler>> _chordDispatchTable;

        /// <summary>
        /// Helper to set bindings based on EditMode
        /// </summary>
        void SetDefaultBindings(EditMode editMode)
        {
            switch (editMode)
            {
                case EditMode.Emacs:
                    SetDefaultEmacsBindings();
                    break;
                case EditMode.Vi:
                    SetDefaultViBindings();
                    break;
                case EditMode.Windows:
                    SetDefaultWindowsBindings();
                    break;
            }
        }

        void SetDefaultWindowsBindings()
        {
            _dispatchTable = new Dictionary<PSKeyInfo, KeyHandler>
            {
                { Keys.Enter,                  MakeKeyHandler(AcceptLine,                "AcceptLine") },
                { Keys.ShiftEnter,             MakeKeyHandler(AddLine,                   "AddLine") },
                { Keys.CtrlEnter,              MakeKeyHandler(InsertLineAbove,           "InsertLineAbove") },
                { Keys.CtrlShiftEnter,         MakeKeyHandler(InsertLineBelow,           "InsertLineBelow") },
                { Keys.Escape,                 MakeKeyHandler(RevertLine,                "RevertLine") },
                { Keys.LeftArrow,              MakeKeyHandler(BackwardChar,              "BackwardChar") },
                { Keys.RightArrow,             MakeKeyHandler(ForwardChar,               "ForwardChar") },
                { Keys.CtrlLeftArrow,          MakeKeyHandler(BackwardWord,              "BackwardWord") },
                { Keys.CtrlRightArrow,         MakeKeyHandler(NextWord,                  "NextWord") },
                { Keys.ShiftLeftArrow,         MakeKeyHandler(SelectBackwardChar,        "SelectBackwardChar") },
                { Keys.ShiftRightArrow,        MakeKeyHandler(SelectForwardChar,         "SelectForwardChar") },
                { Keys.CtrlShiftLeftArrow,     MakeKeyHandler(SelectBackwardWord,        "SelectBackwardWord") },
                { Keys.CtrlShiftRightArrow,    MakeKeyHandler(SelectNextWord,            "SelectNextWord") },
                { Keys.UpArrow,                MakeKeyHandler(PreviousHistory,           "PreviousHistory") },
                { Keys.DownArrow,              MakeKeyHandler(NextHistory,               "NextHistory") },
                { Keys.Home,                   MakeKeyHandler(BeginningOfLine,           "BeginningOfLine") },
                { Keys.End,                    MakeKeyHandler(EndOfLine,                 "EndOfLine") },
                { Keys.ShiftHome,              MakeKeyHandler(SelectBackwardsLine,       "SelectBackwardsLine") },
                { Keys.ShiftEnd,               MakeKeyHandler(SelectLine,                "SelectLine") },
                { Keys.Delete,                 MakeKeyHandler(DeleteChar,                "DeleteChar") },
                { Keys.Backspace,              MakeKeyHandler(BackwardDeleteChar,        "BackwardDeleteChar") },
                { Keys.Tab,                    MakeKeyHandler(TabCompleteNext,           "TabCompleteNext") },
                { Keys.ShiftTab,               MakeKeyHandler(TabCompletePrevious,       "TabCompletePrevious") },
                { Keys.CtrlA,                  MakeKeyHandler(SelectAll,                 "SelectAll") },
                { Keys.CtrlC,                  MakeKeyHandler(CopyOrCancelLine,          "CopyOrCancelLine") },
                { Keys.CtrlShiftC,             MakeKeyHandler(Copy,                      "Copy") },
                { Keys.CtrlL,                  MakeKeyHandler(ClearScreen,               "ClearScreen") },
                { Keys.CtrlR,                  MakeKeyHandler(ReverseSearchHistory,      "ReverseSearchHistory") },
                { Keys.CtrlS,                  MakeKeyHandler(ForwardSearchHistory,      "ForwardSearchHistory") },
                { Keys.CtrlV,                  MakeKeyHandler(Paste,                     "Paste") },
                { Keys.ShiftInsert,            MakeKeyHandler(Paste,                     "Paste") },
                { Keys.CtrlX,                  MakeKeyHandler(Cut,                       "Cut") },
                { Keys.CtrlY,                  MakeKeyHandler(Redo,                      "Redo") },
                { Keys.CtrlZ,                  MakeKeyHandler(Undo,                      "Undo") },
                { Keys.CtrlBackspace,          MakeKeyHandler(BackwardKillWord,          "BackwardKillWord") },
                { Keys.CtrlHome,               MakeKeyHandler(BackwardDeleteLine,        "BackwardDeleteLine") },
                { Keys.CtrlRBracket,           MakeKeyHandler(GotoBrace,                 "GotoBrace") },
                { Keys.CtrlAltQuestion,        MakeKeyHandler(ShowKeyBindings,           "ShowKeyBindings") },
                { Keys.AltPeriod,              MakeKeyHandler(YankLastArg,               "YankLastArg") },
                { Keys.Alt0,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.Alt1,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.Alt2,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.Alt3,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.Alt4,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.Alt5,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.Alt6,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.Alt7,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.Alt8,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.Alt9,                   MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.AltMinus,               MakeKeyHandler(DigitArgument,             "DigitArgument") },
                { Keys.AltQuestion,            MakeKeyHandler(WhatIsKey,                 "WhatIsKey") },
                { Keys.F3,                     MakeKeyHandler(CharacterSearch,           "CharacterSearch") },
                { Keys.ShiftF3,                MakeKeyHandler(CharacterSearchBackward,   "CharacterSearchBackward") },
                { Keys.F8,                     MakeKeyHandler(HistorySearchBackward,     "HistorySearchBackward") },
                { Keys.ShiftF8,                MakeKeyHandler(HistorySearchForward,      "HistorySearchForward") },
                // Added for xtermjs-based terminals that send different key combinations.
                { Keys.AltD,                   MakeKeyHandler(KillWord,                  "KillWord") },
                { Keys.CtrlAt,                 MakeKeyHandler(MenuComplete,              "MenuComplete") },
                { Keys.CtrlW,                  MakeKeyHandler(BackwardKillWord,          "BackwardKillWord")},
            };

            // Some bindings are not available on certain platforms
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _dispatchTable.Add(Keys.CtrlSpace,  MakeKeyHandler(MenuComplete,      "MenuComplete"));
                _dispatchTable.Add(Keys.AltF7,      MakeKeyHandler(ClearHistory,      "ClearHistory"));
                _dispatchTable.Add(Keys.CtrlDelete, MakeKeyHandler(KillWord,          "KillWord"));
                _dispatchTable.Add(Keys.CtrlEnd,    MakeKeyHandler(ForwardDeleteLine, "ForwardDeleteLine"));
                _dispatchTable.Add(Keys.CtrlH,      MakeKeyHandler(BackwardDeleteChar,"BackwardDeleteChar"));

                // PageUp/PageDown and CtrlPageUp/CtrlPageDown bindings are supported on Windows only because they depend on the
                // API 'Console.SetWindowPosition', which throws 'PlatformNotSupportedException' on unix platforms.
                _dispatchTable.Add(Keys.PageUp,       MakeKeyHandler(ScrollDisplayUp,       "ScrollDisplayUp"));
                _dispatchTable.Add(Keys.PageDown,     MakeKeyHandler(ScrollDisplayDown,     "ScrollDisplayDown"));
                _dispatchTable.Add(Keys.CtrlPageUp,   MakeKeyHandler(ScrollDisplayUpLine,   "ScrollDisplayUpLine"));
                _dispatchTable.Add(Keys.CtrlPageDown, MakeKeyHandler(ScrollDisplayDownLine, "ScrollDisplayDownLine"));
            }

            _chordDispatchTable = new Dictionary<PSKeyInfo, Dictionary<PSKeyInfo, KeyHandler>>();
        }

        void SetDefaultEmacsBindings()
        {
            _dispatchTable = new Dictionary<PSKeyInfo, KeyHandler>
            {
                { Keys.Backspace,       MakeKeyHandler(BackwardDeleteChar,   "BackwardDeleteChar") },
                { Keys.Enter,           MakeKeyHandler(AcceptLine,           "AcceptLine") },
                { Keys.ShiftEnter,      MakeKeyHandler(AddLine,              "AddLine") },
                { Keys.LeftArrow,       MakeKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.RightArrow,      MakeKeyHandler(ForwardChar,          "ForwardChar") },
                { Keys.ShiftLeftArrow,  MakeKeyHandler(SelectBackwardChar,   "SelectBackwardChar") },
                { Keys.ShiftRightArrow, MakeKeyHandler(SelectForwardChar,    "SelectForwardChar") },
                { Keys.UpArrow,         MakeKeyHandler(PreviousHistory,      "PreviousHistory") },
                { Keys.DownArrow,       MakeKeyHandler(NextHistory,          "NextHistory") },
                { Keys.AltLess,         MakeKeyHandler(BeginningOfHistory,   "BeginningOfHistory") },
                { Keys.AltGreater,      MakeKeyHandler(EndOfHistory,         "EndOfHistory") },
                { Keys.Home,            MakeKeyHandler(BeginningOfLine,      "BeginningOfLine") },
                { Keys.End,             MakeKeyHandler(EndOfLine,            "EndOfLine") },
                { Keys.ShiftHome,       MakeKeyHandler(SelectBackwardsLine,  "SelectBackwardsLine") },
                { Keys.ShiftEnd,        MakeKeyHandler(SelectLine,           "SelectLine") },
                { Keys.Escape,          MakeKeyHandler(Chord,                "ChordFirstKey") },
                { Keys.Delete,          MakeKeyHandler(DeleteChar,           "DeleteChar") },
                { Keys.Tab,             MakeKeyHandler(Complete,             "Complete") },
                { Keys.CtrlA,           MakeKeyHandler(BeginningOfLine,      "BeginningOfLine") },
                { Keys.CtrlB,           MakeKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.CtrlC,           MakeKeyHandler(CopyOrCancelLine,     "CopyOrCancelLine") },
                { Keys.CtrlD,           MakeKeyHandler(DeleteCharOrExit,     "DeleteCharOrExit") },
                { Keys.CtrlE,           MakeKeyHandler(EndOfLine,            "EndOfLine") },
                { Keys.CtrlF,           MakeKeyHandler(ForwardChar,          "ForwardChar") },
                { Keys.CtrlG,           MakeKeyHandler(Abort,                "Abort") },
                { Keys.CtrlL,           MakeKeyHandler(ClearScreen,          "ClearScreen") },
                { Keys.CtrlK,           MakeKeyHandler(KillLine,             "KillLine") },
                { Keys.CtrlM,           MakeKeyHandler(ValidateAndAcceptLine,"ValidateAndAcceptLine") },
                { Keys.CtrlN,           MakeKeyHandler(NextHistory,          "NextHistory") },
                { Keys.CtrlO,           MakeKeyHandler(AcceptAndGetNext,     "AcceptAndGetNext") },
                { Keys.CtrlP,           MakeKeyHandler(PreviousHistory,      "PreviousHistory") },
                { Keys.CtrlR,           MakeKeyHandler(ReverseSearchHistory, "ReverseSearchHistory") },
                { Keys.CtrlS,           MakeKeyHandler(ForwardSearchHistory, "ForwardSearchHistory") },
                { Keys.CtrlT,           MakeKeyHandler(SwapCharacters,       "SwapCharacters") },
                { Keys.CtrlU,           MakeKeyHandler(BackwardKillLine,     "BackwardKillLine") },
                { Keys.CtrlX,           MakeKeyHandler(Chord,                "ChordFirstKey") },
                { Keys.CtrlW,           MakeKeyHandler(UnixWordRubout,       "UnixWordRubout") },
                { Keys.CtrlY,           MakeKeyHandler(Yank,                 "Yank") },
                { Keys.CtrlAt,          MakeKeyHandler(SetMark,              "SetMark") },
                { Keys.CtrlBackspace,   MakeKeyHandler(BackwardDeleteChar,   "BackwardDeleteChar") },
                { Keys.CtrlUnderbar,    MakeKeyHandler(Undo,                 "Undo") },
                { Keys.CtrlRBracket,    MakeKeyHandler(CharacterSearch,      "CharacterSearch") },
                { Keys.CtrlAltRBracket, MakeKeyHandler(CharacterSearchBackward,"CharacterSearchBackward") },
                { Keys.Alt0,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Alt1,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Alt2,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Alt3,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Alt4,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Alt5,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Alt6,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Alt7,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Alt8,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Alt9,            MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.AltMinus,        MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.AltB,            MakeKeyHandler(BackwardWord,         "BackwardWord") },
                { Keys.AltShiftB,       MakeKeyHandler(SelectBackwardWord,   "SelectBackwardWord") },
                { Keys.AltD,            MakeKeyHandler(KillWord,             "KillWord") },
                { Keys.AltF,            MakeKeyHandler(ForwardWord,          "ForwardWord") },
                { Keys.AltShiftF,       MakeKeyHandler(SelectForwardWord,    "SelectForwardWord") },
                { Keys.AltR,            MakeKeyHandler(RevertLine,           "RevertLine") },
                { Keys.AltY,            MakeKeyHandler(YankPop,              "YankPop") },
                { Keys.AltBackspace,    MakeKeyHandler(BackwardKillWord,     "BackwardKillWord") },
                { Keys.AltEquals,       MakeKeyHandler(PossibleCompletions,  "PossibleCompletions") },
                { Keys.CtrlAltQuestion, MakeKeyHandler(ShowKeyBindings,      "ShowKeyBindings") },
                { Keys.AltQuestion,     MakeKeyHandler(WhatIsKey,            "WhatIsKey") },
                { Keys.AltPeriod,       MakeKeyHandler(YankLastArg,          "YankLastArg") },
                { Keys.AltUnderbar,     MakeKeyHandler(YankLastArg,          "YankLastArg") },
                { Keys.CtrlAltY,        MakeKeyHandler(YankNthArg,           "YankNthArg") },
            };

            // Some bindings are not available on certain platforms
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _dispatchTable.Add(Keys.CtrlH,        MakeKeyHandler(BackwardDeleteChar,    "BackwardDeleteChar"));
                _dispatchTable.Add(Keys.CtrlSpace,    MakeKeyHandler(MenuComplete,          "MenuComplete"));
                _dispatchTable.Add(Keys.CtrlEnd,      MakeKeyHandler(ScrollDisplayToCursor, "ScrollDisplayToCursor"));
                _dispatchTable.Add(Keys.CtrlHome,     MakeKeyHandler(ScrollDisplayTop,      "ScrollDisplayTop"));

                // PageUp/PageDown and CtrlPageUp/CtrlPageDown bindings are supported on Windows only because they depend on the
                // API 'Console.SetWindowPosition', which throws 'PlatformNotSupportedException' on unix platforms.
                _dispatchTable.Add(Keys.PageUp,       MakeKeyHandler(ScrollDisplayUp,       "ScrollDisplayUp"));
                _dispatchTable.Add(Keys.PageDown,     MakeKeyHandler(ScrollDisplayDown,     "ScrollDisplayDown"));
                _dispatchTable.Add(Keys.CtrlPageUp,   MakeKeyHandler(ScrollDisplayUpLine,   "ScrollDisplayUpLine"));
                _dispatchTable.Add(Keys.CtrlPageDown, MakeKeyHandler(ScrollDisplayDownLine, "ScrollDisplayDownLine"));
            }
            else
            {
                _dispatchTable.Add(Keys.AltSpace,     MakeKeyHandler(SetMark,               "SetMark"));
            }

            _chordDispatchTable = new Dictionary<PSKeyInfo, Dictionary<PSKeyInfo, KeyHandler>>
            {
                // Escape,<key> table (meta key)
                [Keys.Escape] = new Dictionary<PSKeyInfo, KeyHandler>
                {
                    { Keys.B,         MakeKeyHandler(BackwardWord,     "BackwardWord") },
                    { Keys.D,         MakeKeyHandler(KillWord,         "KillWord")},
                    { Keys.F,         MakeKeyHandler(ForwardWord,      "ForwardWord")},
                    { Keys.R,         MakeKeyHandler(RevertLine,       "RevertLine")},
                    { Keys.Y,         MakeKeyHandler(YankPop,          "YankPop")},
                    { Keys.CtrlY,     MakeKeyHandler(YankNthArg,       "YankNthArg")},
                    { Keys.Backspace, MakeKeyHandler(BackwardKillWord, "BackwardKillWord")},
                    { Keys.Period,    MakeKeyHandler(YankLastArg,      "YankLastArg")},
                    { Keys.Underbar,  MakeKeyHandler(YankLastArg,      "YankLastArg")},
                },

                // Ctrl+X,<key> table
                [Keys.CtrlX] = new Dictionary<PSKeyInfo, KeyHandler>
                {
                    { Keys.Backspace, MakeKeyHandler(BackwardKillLine,     "BackwardKillLine") },
                    { Keys.CtrlE,     MakeKeyHandler(ViEditVisually,       "ViEditVisually") },
                    { Keys.CtrlU,     MakeKeyHandler(Undo,                 "Undo") },
                    { Keys.CtrlX,     MakeKeyHandler(ExchangePointAndMark, "ExchangePointAndMark") },
                }
            };
        }

        /// <summary>
        /// Used to group the built in functions for help and Get-PSReadLineKeyHandler output.
        /// </summary>
        public static KeyHandlerGroup GetDisplayGrouping(string function)
        {
            switch (function)
            {
            case nameof(Abort):
            case nameof(AcceptAndGetNext):
            case nameof(AcceptLine):
            case nameof(AddLine):
            case nameof(BackwardDeleteChar):
            case nameof(BackwardDeleteLine):
            case nameof(BackwardDeleteWord):
            case nameof(BackwardKillLine):
            case nameof(BackwardKillWord):
            case nameof(CancelLine):
            case nameof(Copy):
            case nameof(CopyOrCancelLine):
            case nameof(Cut):
            case nameof(DeleteChar):
            case nameof(DeleteCharOrExit):
            case nameof(DeleteEndOfWord):
            case nameof(DeleteLine):
            case nameof(DeleteLineToFirstChar):
            case nameof(DeleteToEnd):
            case nameof(DeleteWord):
            case nameof(ForwardDeleteLine):
            case nameof(InsertLineAbove):
            case nameof(InsertLineBelow):
            case nameof(InvertCase):
            case nameof(KillLine):
            case nameof(KillRegion):
            case nameof(KillWord):
            case nameof(Paste):
            case nameof(PasteAfter):
            case nameof(PasteBefore):
            case nameof(PrependAndAccept):
            case nameof(Redo):
            case nameof(RepeatLastCommand):
            case nameof(RevertLine):
            case nameof(ShellBackwardKillWord):
            case nameof(ShellKillWord):
            case nameof(SwapCharacters):
            case nameof(Undo):
            case nameof(UndoAll):
            case nameof(UnixWordRubout):
            case nameof(ValidateAndAcceptLine):
            case nameof(ViAcceptLine):
            case nameof(ViAcceptLineOrExit):
            case nameof(ViAppendLine):
            case nameof(ViBackwardDeleteGlob):
            case nameof(ViBackwardGlob):
            case nameof(ViDeleteBrace):
            case nameof(ViDeleteEndOfGlob):
            case nameof(ViDeleteGlob):
            case nameof(ViDeleteToBeforeChar):
            case nameof(ViDeleteToBeforeCharBackward):
            case nameof(ViDeleteToChar):
            case nameof(ViDeleteToCharBackward):
            case nameof(ViInsertAtBegining):
            case nameof(ViInsertAtEnd):
            case nameof(ViInsertLine):
            case nameof(ViInsertWithAppend):
            case nameof(ViInsertWithDelete):
            case nameof(ViJoinLines):
            case nameof(ViReplaceToBeforeChar):
            case nameof(ViReplaceToBeforeCharBackward):
            case nameof(ViReplaceToChar):
            case nameof(ViReplaceToCharBackward):
            case nameof(ViYankBeginningOfLine):
            case nameof(ViYankEndOfGlob):
            case nameof(ViYankEndOfWord):
            case nameof(ViYankLeft):
            case nameof(ViYankLine):
            case nameof(ViYankNextGlob):
            case nameof(ViYankNextWord):
            case nameof(ViYankPercent):
            case nameof(ViYankPreviousGlob):
            case nameof(ViYankPreviousWord):
            case nameof(ViYankRight):
            case nameof(ViYankToEndOfLine):
            case nameof(ViYankToFirstChar):
            case nameof(Yank):
            case nameof(YankLastArg):
            case nameof(YankNthArg):
            case nameof(YankPop):
                return KeyHandlerGroup.Basic;

            // The following are private so cannot be used with a custom binding.
            case nameof(BackwardReplaceChar):
            case nameof(ReplaceChar):
            case nameof(ReplaceCharInPlace):
            case nameof(ViBackwardReplaceGlob):
            case nameof(ViBackwardReplaceLine):
            case nameof(ViBackwardReplaceLineToFirstChar):
            case nameof(ViBackwardReplaceWord):
            case nameof(ViReplaceBrace):
            case nameof(ViReplaceEndOfGlob):
            case nameof(ViReplaceEndOfWord):
            case nameof(ViReplaceGlob):
            case nameof(ViReplaceLine):
            case nameof(ViReplaceToEnd):
            case nameof(ViReplaceUntilEsc):
            case nameof(ViReplaceWord):
                return KeyHandlerGroup.Basic;

            case nameof(BackwardChar):
            case nameof(BackwardWord):
            case nameof(BeginningOfLine):
            case nameof(EndOfLine):
            case nameof(ForwardChar):
            case nameof(ForwardWord):
            case nameof(GotoBrace):
            case nameof(GotoColumn):
            case nameof(GotoFirstNonBlankOfLine):
            case nameof(MoveToEndOfLine):
            case nameof(MoveToFirstLine):
            case nameof(MoveToLastLine):
            case nameof(NextLine):
            case nameof(NextWord):
            case nameof(NextWordEnd):
            case nameof(PreviousLine):
            case nameof(ShellBackwardWord):
            case nameof(ShellForwardWord):
            case nameof(ShellNextWord):
            case nameof(ViBackwardChar):
            case nameof(ViBackwardWord):
            case nameof(ViForwardChar):
            case nameof(ViEndOfGlob):
            case nameof(ViEndOfPreviousGlob):
            case nameof(ViGotoBrace):
            case nameof(ViNextGlob):
            case nameof(ViNextWord):
                return KeyHandlerGroup.CursorMovement;

            case nameof(BeginningOfHistory):
            case nameof(ClearHistory):
            case nameof(EndOfHistory):
            case nameof(ForwardSearchHistory):
            case nameof(HistorySearchBackward):
            case nameof(HistorySearchForward):
            case nameof(NextHistory):
            case nameof(PreviousHistory):
            case nameof(ReverseSearchHistory):
            case nameof(ViSearchHistoryBackward):
                return KeyHandlerGroup.History;

            case nameof(Complete):
            case nameof(MenuComplete):
            case nameof(PossibleCompletions):
            case nameof(TabCompleteNext):
            case nameof(TabCompletePrevious):
            case nameof(ViTabCompleteNext):
            case nameof(ViTabCompletePrevious):
                return KeyHandlerGroup.Completion;

            case nameof(CaptureScreen):
            case nameof(ClearScreen):
            case nameof(DigitArgument):
            case nameof(InvokePrompt):
            case nameof(ScrollDisplayDown):
            case nameof(ScrollDisplayDownLine):
            case nameof(ScrollDisplayToCursor):
            case nameof(ScrollDisplayTop):
            case nameof(ScrollDisplayUp):
            case nameof(ScrollDisplayUpLine):
            case nameof(SelfInsert):
            case nameof(ShowKeyBindings):
            case nameof(ViCommandMode):
            case nameof(ViDigitArgumentInChord):
            case nameof(ViEditVisually):
            case nameof(ViExit):
            case nameof(ViInsertMode):
            case nameof(WhatIsKey):
            case nameof(AcceptSuggestion):
            case nameof(AcceptNextSuggestionWord):
                return KeyHandlerGroup.Miscellaneous;

            case nameof(CharacterSearch):
            case nameof(CharacterSearchBackward):
            case nameof(RepeatLastCharSearch):
            case nameof(RepeatLastCharSearchBackwards):
            case nameof(RepeatSearch):
            case nameof(RepeatSearchBackward):
            case nameof(SearchChar):
            case nameof(SearchCharBackward):
            case nameof(SearchCharBackwardWithBackoff):
            case nameof(SearchCharWithBackoff):
            case nameof(SearchForward):
                return KeyHandlerGroup.Search;

            case nameof(ExchangePointAndMark):
            case nameof(SetMark):
            case nameof(SelectAll):
            case nameof(SelectBackwardChar):
            case nameof(SelectBackwardsLine):
            case nameof(SelectBackwardWord):
            case nameof(SelectForwardChar):
            case nameof(SelectForwardWord):
            case nameof(SelectLine):
            case nameof(SelectNextWord):
            case nameof(SelectShellBackwardWord):
            case nameof(SelectShellForwardWord):
            case nameof(SelectShellNextWord):
                return KeyHandlerGroup.Selection;

            default:
                return KeyHandlerGroup.Custom;
            }
        }

        /// <summary>
        /// Show all bound keys.
        /// </summary>
        public static void ShowKeyBindings(ConsoleKeyInfo? key = null, object arg = null)
        {
            var buffer = new StringBuilder();
            var boundKeys = GetKeyHandlers(includeBound: true, includeUnbound: false);
            var console = _singleton._console;
            foreach (var group in boundKeys.GroupBy(k => k.Group).OrderBy(k => k.Key))
            {
                var groupDescription = PowerShell.KeyHandler.GetGroupingDescription(group.Key);
                buffer.AppendFormat("\n{0}\n", groupDescription);
                buffer.Append('=', groupDescription.Length);
                buffer.AppendLine();

                // Compute column widths
                var groupBindings = group.OrderBy(k => k.Function).ToArray();
                var keyWidth = -1;
                var funcWidth = -1;
                foreach (var binding in groupBindings)
                {
                    keyWidth = Math.Max(keyWidth, binding.Key.Length);
                    funcWidth = Math.Max(funcWidth, binding.Function.Length);
                }
                var maxDescriptionLength = console.WindowWidth - keyWidth - funcWidth - 2;
                var fmtString = "{0,-" + keyWidth + "} {1,-" + funcWidth + "} {2}\n";

                foreach (var boundKey in groupBindings)
                {
                    var description = boundKey.Description;
                    if (description.Length >= maxDescriptionLength)
                    {
                        description = description.Substring(0, maxDescriptionLength - 4) + "...";
                    }
                    buffer.AppendFormat(CultureInfo.InvariantCulture, fmtString, boundKey.Key, boundKey.Function, description);
                }
            }

            // Don't overwrite any of the line - so move to first line after the end of our buffer.
            var point = _singleton.ConvertOffsetToPoint(_singleton._buffer.Length);
            console.SetCursorPosition(point.X, point.Y);
            console.Write("\n");

            console.WriteLine(buffer.ToString());
            InvokePrompt(key: null, arg: _singleton._console.CursorTop);
        }

        /// <summary>
        /// Read a key and tell me what the key is bound to.
        /// </summary>
        public static void WhatIsKey(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._statusLinePrompt = "what-is-key: ";
            _singleton.Render();
            var toLookup = ReadKey();
            var buffer = new StringBuilder();
            _singleton._dispatchTable.TryGetValue(toLookup, out var keyHandler);
            buffer.Append(toLookup.KeyStr);
            if (keyHandler != null)
            {
                if (keyHandler.BriefDescription == "ChordFirstKey")
                {
                    if (_singleton._chordDispatchTable.TryGetValue(toLookup, out var secondKeyDispatchTable))
                    {
                        toLookup = ReadKey();
                        secondKeyDispatchTable.TryGetValue(toLookup, out keyHandler);
                        buffer.Append(",");
                        buffer.Append(toLookup.KeyStr);
                    }
                }
            }
            buffer.Append(": ");
            if (keyHandler != null)
            {
                buffer.Append(keyHandler.BriefDescription);
                if (!string.IsNullOrWhiteSpace(keyHandler.LongDescription))
                {
                    buffer.Append(" - ");
                    buffer.Append(keyHandler.LongDescription);
                }
            }
            else if (toLookup.KeyChar != 0)
            {
                buffer.Append("SelfInsert");
                buffer.Append(" - ");
                buffer.Append(PSReadLineResources.SelfInsertDescription);
            }
            else
            {
                buffer.Append(PSReadLineResources.KeyIsUnbound);
            }

            _singleton.ClearStatusMessage(render: false);

            var console = _singleton._console;
            // Don't overwrite any of the line - so move to first line after the end of our buffer.
            var point = _singleton.ConvertOffsetToPoint(_singleton._buffer.Length);
            console.SetCursorPosition(point.X, point.Y);
            console.Write("\n");

            console.WriteLine(buffer.ToString());
            InvokePrompt(key: null, arg: console.CursorTop);
        }
    }
}
