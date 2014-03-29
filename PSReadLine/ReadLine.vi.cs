using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Remembers last history search direction.
        /// </summary>
        private bool _searchHistoryBackward = true;

        private class ViCharacterSearcher
        {
            private char c = '\0';
            private bool wasBackward = false;
            private bool wasBackoff = false;

            private static ViCharacterSearcher instance = new ViCharacterSearcher();

            public static bool IsRepeatable
            {
                get { return instance.c != '\0'; }
            }

            private void Set( char theChar, bool isBackward = false, bool isBackoff = false )
            {
                this.c = theChar;
                this.wasBackward = isBackward;
                this.wasBackoff = isBackoff;
            }

            /// <summary>
            /// Repeat the last recorded string search.
            /// </summary>
            public static void RepeatLastSearch( ConsoleKeyInfo? key, object arg )
            {
                if( !IsRepeatable )
                {
                    Ding();
                    return;
                }

                if( instance.wasBackward )
                {
                    SearchBackward( instance.c, null, instance.wasBackoff );
                }
                else
                {
                    Search( instance.c, null, instance.wasBackoff );
                }
            }

            /// <summary>
            /// Repeat the last recorded string search, but in the opposite direction.
            /// </summary>
            public static void RepeatLastSearchBackwards( ConsoleKeyInfo? key, object arg )
            {
                if( !IsRepeatable )
                {
                    Ding();
                    return;
                }

                if( instance.wasBackward )
                {
                    Search( instance.c, null, instance.wasBackoff );
                }
                else
                {
                    SearchBackward( instance.c, null, instance.wasBackoff );
                }
            }

            /// <summary>
            /// Read the next character and then find it, going forward, and then back off a character.
            /// This is for 't' functionality.
            /// </summary>
            public static void Search( ConsoleKeyInfo? key = null, object arg = null )
            {
                char keyChar = ReadKey().KeyChar;
                instance.Set( keyChar, isBackward: false, isBackoff: false );
                Search( keyChar, arg, backoff: false );
            }

            /// <summary>
            /// Read the next character and then find it, going backard, and then back off a character.
            /// This is for 'T' functionality.
            /// </summary>
            public static void SearchBackward( ConsoleKeyInfo? key = null, object arg = null )
            {
                char keyChar = ReadKey().KeyChar;
                instance.Set( keyChar, isBackward: true, isBackoff: false );
                SearchBackward( keyChar, arg, backoff: false );
            }

            /// <summary>
            /// Read the next character and then find it, going forward, and then back off a character.
            /// This is for 't' functionality.
            /// </summary>
            public static void SearchWithBackoff( ConsoleKeyInfo? key = null, object arg = null )
            {
                char keyChar = ReadKey().KeyChar;
                instance.Set( keyChar, isBackward: false, isBackoff: true );
                Search( keyChar, arg, backoff: true );
            }

            /// <summary>
            /// Read the next character and then find it, going backard, and then back off a character.
            /// This is for 'T' functionality.
            /// </summary>
            public static void SearchBackwardWithBackoff( ConsoleKeyInfo? key = null, object arg = null )
            {
                char keyChar = ReadKey().KeyChar;
                instance.Set( keyChar, isBackward: true, isBackoff: true );
                SearchBackward( keyChar, arg, backoff: true );
            }

            private static void Search( char keyChar, object arg, bool backoff )
            {
                int qty = ( arg is int ) ? (int) arg : 1;

                for( int i = _singleton._current + 1; i < _singleton._buffer.Length; i++ )
                {
                    if( _singleton._buffer[i] == keyChar )
                    {
                        qty -= 1;
                        if( qty == 0 )
                        {
                            _singleton._current = backoff ? i - 1 : i;
                            _singleton.PlaceCursor();
                            return;
                        }
                    }
                }
                Ding();
            }

            private static void SearchBackward( char keyChar, object arg, bool backoff )
            {
                instance.Set( keyChar, isBackward: true, isBackoff: backoff );
                int qty = ( arg is int ) ? (int) arg : 1;

                for( int i = _singleton._current - 1; i >= 0; i-- )
                {
                    if( _singleton._buffer[i] == keyChar )
                    {
                        qty -= 1;
                        if( qty == 0 )
                        {
                            _singleton._current = backoff ? i + 1 : i;
                            _singleton.PlaceCursor();
                            return;
                        }
                    }
                }
                Ding();
            }
        }

        #region KeyBindings
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viInsKeyMap = new Dictionary<ConsoleKeyInfo, KeyHandler>( new ConsoleKeyInfoComparer() )
            {
                { Keys.Enter,           MakeKeyHandler(AcceptLine,             "AcceptLine") },
                { Keys.ShiftEnter,      MakeKeyHandler(AddLine,                "AddLine") },
                { Keys.Escape,          MakeKeyHandler(ViCmdMode,              "ToViCmdMode") },
                { Keys.LeftArrow,       MakeKeyHandler(BackwardChar,           "BackwardChar") },
                { Keys.RightArrow,      MakeKeyHandler(ViForwardChar,          "ForwardChar") },
                { Keys.CtrlLeftArrow,   MakeKeyHandler(BackwardWord,           "BackwardWord") },
                { Keys.CtrlRightArrow,  MakeKeyHandler(NextWord,               "NextWord") },
                //{ Keys.UpArrow,         MakeKeyHandler(PreviousHistory,      "PreviousHistory") },
                //{ Keys.DownArrow,       MakeKeyHandler(NextHistory,          "NextHistory") },
                { Keys.Home,            MakeKeyHandler(BeginningOfLine,        "BeginningOfLine") },
                { Keys.End,             MakeKeyHandler(EndOfLine,              "EndOfLine") },
                { Keys.Delete,          MakeKeyHandler(ViDeleteChar,           "DeleteChar") },
                { Keys.Backspace,       MakeKeyHandler(ViBackwardDeleteChar,   "BackwardDeleteChar") },
                { Keys.CtrlSpace,       MakeKeyHandler(PossibleCompletions,    "PossibleCompletions") },
                { Keys.Tab,             MakeKeyHandler(TabCompleteNext,        "TabCompleteNext") },
                { Keys.ShiftTab,        MakeKeyHandler(TabCompletePrevious,    "TabCompletePrevious") },
                { Keys.CtrlV,           MakeKeyHandler(Paste,                  "Paste") },
                { Keys.VolumeDown,      MakeKeyHandler(Ignore,                 "Ignore") },
                { Keys.VolumeUp,        MakeKeyHandler(Ignore,                 "Ignore") },
                { Keys.VolumeMute,      MakeKeyHandler(Ignore,                 "Ignore") },
                { Keys.CtrlC,           MakeKeyHandler(CancelLine,             "CancelLine") },
                { Keys.CtrlL,           MakeKeyHandler(ClearScreen,            "ClearScreen") },
                { Keys.CtrlY,           MakeKeyHandler(Redo,                   "Redo") },
                { Keys.CtrlZ,           MakeKeyHandler(Undo,                   "Undo") },
                { Keys.CtrlBackspace,   MakeKeyHandler(BackwardKillWord,       "BackwardKillWord") },
                { Keys.CtrlDelete,      MakeKeyHandler(KillWord,               "KillWord") },
                { Keys.CtrlEnd,         MakeKeyHandler(ForwardDeleteLine,      "ForwardDeleteLine") },
                { Keys.CtrlHome,        MakeKeyHandler(BackwardDeleteLine,     "BackwardDeleteLine") },
                { Keys.CtrlRBracket,    MakeKeyHandler(GotoBrace,              "GotoBrace") },
                { Keys.F3,              MakeKeyHandler(CharacterSearch,        "CharacterSearch") },
                { Keys.ShiftF3,         MakeKeyHandler(CharacterSearchBackward,"CharacterSearchBackward") },
            };
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viCmdKeyMap = new Dictionary<ConsoleKeyInfo, KeyHandler>( new ConsoleKeyInfoComparer() )
            {
                { Keys.Enter,           MakeKeyHandler(ViAcceptLine,           "AcceptLine") },
                { Keys.ShiftEnter,      MakeKeyHandler(AddLine,              "AddLine") },
                { Keys.Escape,          MakeKeyHandler(Ding,                 "Ignore") },
                //{ Keys.Escape,          MakeKeyHandler(RevertLine,           "RevertLine") },
                { Keys.LeftArrow,       MakeKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.RightArrow,      MakeKeyHandler(ViForwardChar,        "ForwardChar") },
                { Keys.Space,           MakeKeyHandler(ViForwardChar,        "ForwardChar") },
                { Keys.CtrlLeftArrow,   MakeKeyHandler(BackwardWord,         "BackwardWord") },
                { Keys.CtrlRightArrow,  MakeKeyHandler(NextWord,             "NextWord") },
                { Keys.UpArrow,         MakeKeyHandler(PreviousHistory,      "PreviousHistory") },
                { Keys.DownArrow,       MakeKeyHandler(NextHistory,          "NextHistory") },
                { Keys.Home,            MakeKeyHandler(BeginningOfLine,      "BeginningOfLine") },
                { Keys.End,             MakeKeyHandler(ViEndOfLine,          "EndOfLine") },
                { Keys.Delete,          MakeKeyHandler(ViDeleteChar,         "DeleteChar") },
                { Keys.Backspace,       MakeKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.CtrlSpace,       MakeKeyHandler(PossibleCompletions,  "PossibleCompletions") },
                { Keys.Tab,             MakeKeyHandler(TabCompleteNext,      "TabCompleteNext") },
                { Keys.ShiftTab,        MakeKeyHandler(TabCompletePrevious,  "TabCompletePrevious") },
                { Keys.CtrlV,           MakeKeyHandler(Paste,                "Paste") },
                { Keys.VolumeDown,      MakeKeyHandler(Ignore,               "Ignore") },
                { Keys.VolumeUp,        MakeKeyHandler(Ignore,               "Ignore") },
                { Keys.VolumeMute,      MakeKeyHandler(Ignore,               "Ignore") },
                { Keys.CtrlC,           MakeKeyHandler(CancelLine,           "CancelLine") },
                { Keys.CtrlL,           MakeKeyHandler(ClearScreen,          "ClearScreen") },
                { Keys.CtrlT,           MakeKeyHandler(ViTransposeChars,     "ViTransposeChars" ) },
                { Keys.CtrlU,           MakeKeyHandler(BackwardDeleteLine,   "BackwardDeleteLine") },      
                { Keys.CtrlW,           MakeKeyHandler(ViBackwardDeleteWord, "ViBackwardDeleteWord") },
                { Keys.CtrlY,           MakeKeyHandler(Redo,                 "Redo") },
                { Keys.CtrlZ,           MakeKeyHandler(Undo,                 "Undo") },
                { Keys.CtrlBackspace,   MakeKeyHandler(BackwardKillWord,     "BackwardKillWord") },
                { Keys.CtrlDelete,      MakeKeyHandler(KillWord,             "KillWord") },
                { Keys.CtrlEnd,         MakeKeyHandler(ForwardDeleteLine,    "ForwardDeleteLine") },
                { Keys.CtrlHome,        MakeKeyHandler(BackwardDeleteLine,   "BackwardDeleteLine") },
                { Keys.CtrlRBracket,    MakeKeyHandler(GotoBrace,            "GotoBrace") },
                { Keys.F3,              MakeKeyHandler(CharacterSearch,      "CharacterSearch") },
                { Keys.ShiftF3,         MakeKeyHandler(CharacterSearchBackward, "CharacterSearchBackward") },
                { Keys.A,               MakeKeyHandler(ViInsModeWithAppend,  "Ignore") },
                { Keys.B,               MakeKeyHandler(BackwardWord,         "BackwardWord") },
                { Keys.C,               MakeKeyHandler(Chord,                "ChordFirstKey") },
                { Keys.D,               MakeKeyHandler(Chord,                "ChordFirstKey") },
                { Keys.E,               MakeKeyHandler(ViForwardWord,        "ForwardWord") },
                { Keys.F,               MakeKeyHandler(ViCharacterSearcher.Search, "Search") },
                { Keys.G,               MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.H,               MakeKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.I,               MakeKeyHandler(ViInsMode,            "Insert") },
                { Keys.J,               MakeKeyHandler(ViNextHistory,        "NextHistory") },
                { Keys.K,               MakeKeyHandler(ViPreviousHistory,    "PreviousHistory") },
                { Keys.L,               MakeKeyHandler(ViForwardChar,        "ForwardChar") },
                { Keys.M,               MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.N,               MakeKeyHandler(ViRepeatSearch,       "ViRepeatSearch") },
                { Keys.O,               MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.P,               MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.Q,               MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.R,               MakeKeyHandler(ViReplaceCharInPlace, "ChordFirstKey") },
                { Keys.S,               MakeKeyHandler(ViInsModeWithDelete,  "Ignore") },
                { Keys.T,               MakeKeyHandler(ViCharacterSearcher.SearchWithBackoff, "Search") },
                { Keys.U,               MakeKeyHandler(Undo,                 "Undo") },
                { Keys.V,               MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.W,               MakeKeyHandler(NextWord,             "NextWord") },
                { Keys.X,               MakeKeyHandler(ViDeleteChar,         "DeleteChar") },
                { Keys.Y,               MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.Z,               MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucA,             MakeKeyHandler(ViInsModeAtEnd,       "AppendAtEnd") },
                { Keys.ucB,             MakeKeyHandler(BackwardWord,         "BackwardWord") },
                { Keys.ucC,             MakeKeyHandler(ViReplaceToEnd,       "ViReplaceToEnd") },
                { Keys.ucD,             MakeKeyHandler(ViDeleteToEnd,        "ViDeleteToEnd") },
                { Keys.ucE,             MakeKeyHandler(ViForwardWord,        "ForwardWord") },
                { Keys.ucF,             MakeKeyHandler(ViCharacterSearcher.SearchBackward, "SearchBackward") },
                { Keys.ucG,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucH,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucI,             MakeKeyHandler(ViInsModeAtBegining,  "InsertAtBeginning") },
                { Keys.ucJ,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucK,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucL,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucM,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucN,             MakeKeyHandler(ViRepeatSearchBackward, "ViRepeatSearchBackward") },
                { Keys.ucO,             MakeKeyHandler(BeginningOfLine,      "BeginningOfLine") },
                { Keys.ucP,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucQ,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucR,             MakeKeyHandler(ViReplaceUntilEsc,    "ViReplaceUntilEsc") },
                { Keys.ucS,             MakeKeyHandler(ViReplaceLine,        "ViReplaceLine") },
                { Keys.ucT,             MakeKeyHandler(ViCharacterSearcher.SearchBackwardWithBackoff, "SearchBackward") },
                { Keys.ucU,             MakeKeyHandler(ViUndoAll,            "UndoAll") },
                { Keys.ucV,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucW,             MakeKeyHandler(NextWord,             "NextWord") },
                { Keys.ucX,             MakeKeyHandler(ViBackwardDeleteChar, "BackwardDeleteChar") },
                { Keys.ucY,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys.ucZ,             MakeKeyHandler(Ding,                 "Ignore") },
                { Keys._0,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._1,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._2,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._3,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._4,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._5,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._6,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._7,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._8,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys._9,              MakeKeyHandler(DigitArgument,        "DigitArgument") },
                { Keys.Dollar,          MakeKeyHandler(ViEndOfLine,          "EndOfLine" ) },
                { Keys.Percent,         MakeKeyHandler(ViGotoMatchingBrace,  "GotoBrace" ) },
                { Keys.Pound,           MakeKeyHandler(ViCommentLine,        "CommentLine" ) },
                { Keys.Pipe,            MakeKeyHandler(ViGotoColumn,         "GotoColumn" ) },
                { Keys.Uphat,           MakeKeyHandler(ViFirstNonBlankOfLine, "ViFirstNonBlankOfLine" ) },
                { Keys.Tilde,           MakeKeyHandler(ViInvertCase,          "ViInvertCase" ) },
                { Keys.Slash,           MakeKeyHandler(ViStartSearchBackward, "ViStartSearchBackward") },
                { Keys.CtrlR,           MakeKeyHandler(ViStartSearchBackward, "ViStartSearchBackward") },
                { Keys.Question,        MakeKeyHandler(ViStartSearchForward,  "ViStartSearchForward") },
                { Keys.CtrlS,           MakeKeyHandler(ViStartSearchForward,  "ViStartSearchForward") },
                { Keys.Period,          MakeKeyHandler(ViRepeatLastMod,       "ViRepeatLastMod" ) },
                { Keys.Semicolon,       MakeKeyHandler(ViCharacterSearcher.RepeatLastSearch,          "RepeatLastSearch" ) },
                { Keys.Comma,           MakeKeyHandler(ViCharacterSearcher.RepeatLastSearchBackwards, "RepeastLastSearch" ) }
            };

        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viChordDTable = new Dictionary<ConsoleKeyInfo, KeyHandler>( new ConsoleKeyInfoComparer() )
            {
                { Keys.D,               MakeKeyHandler( ViDeleteLine,         "DeleteLine" ) },
                { Keys.Dollar,          MakeKeyHandler( ViDeleteToEnd,        "ViDeleteToEnd" ) },
                { Keys.B,               MakeKeyHandler( ViBackwardDeleteWord, "ViBackwardDeleteWord" ) },
                { Keys.ucB,             MakeKeyHandler( ViBackwardDeleteWord, "ViBackwardDeleteWord" ) },
                { Keys.W,               MakeKeyHandler( ViDeleteWord,         "ViDeleteWord" ) },
                { Keys.ucW,             MakeKeyHandler( ViDeleteWord,         "ViDeleteWord" ) },
                { Keys.E,               MakeKeyHandler( ViDeleteToEndOfWord,  "ViDeleteWord" ) },
                { Keys.ucE,             MakeKeyHandler( ViDeleteToEndOfWord,  "ViDeleteWord" ) },
                { Keys.H,               MakeKeyHandler( ViBackwardDeleteChar, "BackwardDeleteChar" ) },
                { Keys.L,               MakeKeyHandler( ViDeleteChar,         "DeleteChar" ) },
                { Keys.Space,           MakeKeyHandler( ViDeleteChar,         "DeleteChar" ) },
                { Keys._0,              MakeKeyHandler( BackwardDeleteLine,   "BackwardDeleteLine" ) },
                { Keys.Uphat,           MakeKeyHandler( ViBackwardDeleteLineToFirstChar, "ViBackwardDeleteLineToFirstChar" ) },
                { Keys.Percent,         MakeKeyHandler( ViDeleteBrace,       "ViDeleteBrace" ) }
            };
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viChordCTable = new Dictionary<ConsoleKeyInfo, KeyHandler>( new ConsoleKeyInfoComparer() )
            {
                { Keys.C,               MakeKeyHandler( ViReplaceLine,         "ViReplaceLine" ) },
                { Keys.Dollar,          MakeKeyHandler( ViReplaceToEnd,        "ViReplaceToEnd" ) },
                { Keys.B,               MakeKeyHandler( ViBackwardReplaceWord, "ViBackwardDeleteWord" ) },
                { Keys.ucB,             MakeKeyHandler( ViBackwardReplaceWord, "ViBackwardDeleteWord" ) },
                { Keys.W,               MakeKeyHandler( ViReplaceWord,         "ViReplaceWord" ) },
                { Keys.ucW,             MakeKeyHandler( ViReplaceWord,         "ViReplaceWord" ) },
                { Keys.E,               MakeKeyHandler( ViReplaceWord,         "ViReplaceWord" ) },
                { Keys.ucE,             MakeKeyHandler( ViReplaceWord,         "ViReplaceWord" ) },
                { Keys.H,               MakeKeyHandler( ViBackwardReplaceChar, "ViBackwardReplaceChar" ) },
                { Keys.L,               MakeKeyHandler( ViReplaceChar,         "ViReplaceChar" ) },
                { Keys.Space,           MakeKeyHandler( ViReplaceChar,         "ViReplaceChar" ) },
                { Keys._0,              MakeKeyHandler( ViBackwardReplaceLine, "ViBackwardReplaceLine" ) },
                { Keys.Uphat,           MakeKeyHandler( ViBackwardReplaceLineToFirstChar, "ViBackwardReplaceLineToFirstChar" ) },
                { Keys.Percent,         MakeKeyHandler( ViReplaceBrace,        "ViReplaceBrace" ) }
            };
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _viChordYTable = new Dictionary<ConsoleKeyInfo, KeyHandler>( new ConsoleKeyInfoComparer() )
            {
                { Keys.D,               MakeKeyHandler( ViReplaceChar,        "ViReplaceChar" ) },
                { Keys.Dollar,          MakeKeyHandler( ViDeleteToEnd,        "ViDeleteToEnd" ) },
                { Keys.B,               MakeKeyHandler( ViBackwardDeleteWord, "ViBackwardDeleteWord" ) },
                { Keys.ucB,             MakeKeyHandler( ViBackwardDeleteWord, "ViBackwardDeleteWord" ) },
                { Keys.W,               MakeKeyHandler( ViDeleteWord,         "ViDeleteWord" ) },
                { Keys.ucW,             MakeKeyHandler( ViDeleteWord,         "ViDeleteWord" ) },
                { Keys.E,               MakeKeyHandler( ViDeleteWord,         "ViDeleteWord" ) },
                { Keys.ucE,             MakeKeyHandler( ViDeleteWord,         "ViDeleteWord" ) },
                { Keys.H,               MakeKeyHandler( BackwardDeleteChar,   "BackwardDeleteChar" ) },
                { Keys.L,               MakeKeyHandler( DeleteChar,           "DeleteChar" ) },
                { Keys.Space,           MakeKeyHandler( DeleteChar,           "DeleteChar" ) },
                { Keys._0,              MakeKeyHandler( BackwardDeleteLine,   "BackwardDeleteLine" ) },
                { Keys.Uphat,           MakeKeyHandler( ViBackwardDeleteLineToFirstChar, "ViBackwardDeleteLineToFirstChar" ) },
                { Keys.Percent,         MakeKeyHandler( ViDeleteBrace,        "ViDeleteBrace" ) }
            };
        #endregion KeyBindings

        /// <summary>
        /// Sets up the key bindings for vi operations.
        /// </summary>
        private void SetDefaultViBindings()
        {
            _dispatchTable = _viInsKeyMap;
            _chordDispatchTable = new Dictionary<ConsoleKeyInfo, Dictionary<ConsoleKeyInfo, KeyHandler>>();
            _chordDispatchTable[Keys.D] = _viChordDTable;
            _chordDispatchTable[Keys.C] = _viChordCTable;
            _chordDispatchTable[Keys.Y] = _viChordYTable;
        }

        /// <summary>
        /// Delete the character under the cursor.
        /// </summary>
        public static void ViDeleteChar( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( _singleton._visualSelectionCommandCount > 0 )
            {
                int start, length;
                _singleton.GetRegion( out start, out length );
                Delete( start, length );
                return;
            }

            if( _singleton._buffer.Length > 0 && _singleton._current < _singleton._buffer.Length )
            {
                int qty = ( arg is int ) ? (int) arg : 1;
                qty = Math.Min( qty, _singleton._buffer.Length - _singleton._current );

                _singleton.SaveEditItem(
                    EditItemDelete.Create( _singleton._buffer.ToString( _singleton._current, qty ),
                    _singleton._current,
                    ViDeleteChar,
                    arg ) );
                _singleton._buffer.Remove( _singleton._current, qty );
                if( _singleton._current >= _singleton._buffer.Length )
                {
                    _singleton._current = _singleton._buffer.Length - 1;
                }
                _singleton.Render();
            }
        }

        /// <summary>
        /// Delete the character before the cursor.
        /// </summary>
        public static void ViBackwardDeleteChar( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( _singleton._visualSelectionCommandCount > 0 )
            {
                int start, length;
                _singleton.GetRegion( out start, out length );
                Delete( start, length );
                return;
            }

            if( _singleton._buffer.Length > 0 && _singleton._current > 0 )
            {
                int qty = ( arg is int ) ? (int) arg : 1;
                qty = Math.Min( qty, _singleton._current );

                int startDeleteIndex = _singleton._current - qty;
                _singleton.SaveEditItem(
                    EditItemDelete.Create(
                        _singleton._buffer.ToString( startDeleteIndex, qty ),
                        startDeleteIndex,
                        ViBackwardDeleteChar,
                        arg )
                        );
                _singleton._buffer.Remove( startDeleteIndex, qty );
                _singleton._current = startDeleteIndex;
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }

        /// <summary>
        /// Move the cursor to the end of the input.
        /// </summary>
        public static void ViEndOfLine( ConsoleKeyInfo? key = null, object arg = null )
        {
            _singleton._current = _singleton._buffer.Length - 1;
            _singleton.PlaceCursor();
        }

        /// <summary>
        /// Move the cursor one character to the right.  This may move the cursor to the next
        /// line of multi-line input.
        /// </summary>
        public static void ViForwardChar( ConsoleKeyInfo? key = null, object arg = null )
        {
            int qty = ( arg is int ) ? (int) arg : 1;   // For VI movement
            int distance = Math.Min( qty, _singleton._buffer.Length - _singleton._current - 1 );
            if( distance > 0 )
            {
                _singleton._current += distance;
                _singleton.PlaceCursor();
            }
        }

        /// <summary>
        /// Move the cursor forward to the end of the current word, or if between words,
        /// to the end of the next word.  Word boundaries are defined by a configurable
        /// set of characters.
        /// </summary>
        public static void ViForwardWord( ConsoleKeyInfo? key = null, object arg = null )
        {
            int i = _singleton.ViFindNextWordEnd( _singleton.Options.WordDelimiters ) - 1;
            _singleton._current = i;
            _singleton.PlaceCursor();

            // For VI movement
            int qty = ( arg is int ) ? (int) arg : 1;
            if( qty > 1 )
            {
                ViForwardWord( key, qty - 1 );
            }
        }

        /// <summary>
        /// Find the end of the current/next word as defined by wordDelimiters and whitespace.
        /// </summary>
        private int ViFindNextWordEnd( string wordDelimiters )
        {
            int i = _current;

            if( InWord( i, wordDelimiters ) )
            {
                if( i < _buffer.Length - 1 && !InWord( i + 1, wordDelimiters ) )
                {
                    i++;
                }
            }

            if( i == _buffer.Length )
            {
                return i;
            }

            if( !InWord( i, wordDelimiters ) )
            {
                // Scan to end of current non-word region
                while( i < _buffer.Length )
                {
                    if( InWord( i, wordDelimiters ) )
                    {
                        break;
                    }
                    i += 1;
                }
            }
            while( i < _buffer.Length )
            {
                if( !InWord( i, wordDelimiters ) )
                {
                    break;
                }
                i += 1;
            }
            return i;
        }

        /// <summary>
        /// Find the start of the next word from the supplied location.
        /// Needed by VI.
        /// </summary>
        private int ViFindNextWordPointFrom( int cursor, string wordDelimiters )
        {
            int i = cursor;
            if( i == _singleton._buffer.Length )
            {
                return i;
            }

            if( InWord( i, wordDelimiters ) )
            {
                // Scan to end of current word region
                while( i < _singleton._buffer.Length )
                {
                    if( !InWord( i, wordDelimiters ) )
                    {
                        break;
                    }
                    i += 1;
                }
            }

            while( i < _singleton._buffer.Length )
            {
                if( InWord( i, wordDelimiters ) )
                {
                    break;
                }
                i += 1;
            }
            return i;
        }

        /// <summary>
        /// Find the beginning of the previous word from the supplied spot.
        /// </summary>
        private int ViFindPreviousWordPointFrom( int cursor, string wordDelimiters )
        {
            int i = cursor - 1;
            if( i < 0 )
            {
                return 0;
            }

            if( !InWord( i, wordDelimiters ) )
            {
                // Scan backwards until we are at the end of the previous word.
                while( i > 0 )
                {
                    if( InWord( i, wordDelimiters ) )
                    {
                        break;
                    }
                    i -= 1;
                }
            }
            while( i > 0 )
            {
                if( !InWord( i, wordDelimiters ) )
                {
                    i += 1;
                    break;
                }
                i -= 1;
            }
            return i;
        }

        private static void ViPreviousHistory( ConsoleKeyInfo? key, object arg )
        {
            PreviousHistory( key, arg );
            _singleton._current = 0;
            _singleton.PlaceCursor();
        }

        private static void ViNextHistory( ConsoleKeyInfo? key, object arg )
        {
            NextHistory( key, arg );
            _singleton._current = 0;
            _singleton.PlaceCursor();
        }

        private static void ViReplaceUntilEsc( ConsoleKeyInfo? key, object arg )
        {
            if( _singleton._current >= _singleton._buffer.Length )
            {
                Ding();
                return;
            }

            int startingCursor = _singleton._current;
            int maxDeleteLength = _singleton._buffer.Length - _singleton._current;
            StringBuilder deletedStr = new StringBuilder();

            ConsoleKeyInfo nextKey = ReadKey();
            while( nextKey.Key != ConsoleKey.Escape && nextKey.Key != ConsoleKey.Enter )
            {
                if( nextKey.Key != ConsoleKey.Backspace && nextKey.KeyChar != '\u0000' )
                {
                    if( _singleton._current >= _singleton._buffer.Length )
                    {
                        _singleton._buffer.Append( nextKey.KeyChar );
                    }
                    else
                    {
                        deletedStr.Append( _singleton._buffer[_singleton._current] );
                        _singleton._buffer[_singleton._current] = nextKey.KeyChar;
                    }
                    _singleton._current++;
                    _singleton.Render();
                }
                if( nextKey.Key == ConsoleKey.Backspace )
                {
                    if( _singleton._current == startingCursor )
                    {
                        Ding();
                    }
                    else
                    {
                        if( deletedStr.Length == _singleton._current - startingCursor )
                        {
                           _singleton._buffer[_singleton._current - 1] = deletedStr[deletedStr.Length - 1];
                            deletedStr.Remove( deletedStr.Length - 1, 1 );
                        } else {
                            _singleton._buffer.Remove( _singleton._current - 1, 1 );
                        }
                        _singleton._current--;
                        _singleton.Render();
                    }
                }
                nextKey = ReadKey();
            }

            if( _singleton._current > startingCursor )
            {
                _singleton.StartEditGroup();
                string insStr = _singleton._buffer.ToString( startingCursor, _singleton._current - startingCursor );
                _singleton.SaveEditItem( EditItemDelete.Create( deletedStr.ToString(), startingCursor ) );
                _singleton.SaveEditItem( EditItemInsertString.Create( insStr, startingCursor ) );
                _singleton.EndEditGroup();
            }

            if( nextKey.Key == ConsoleKey.Enter )
            {
                ViAcceptLine( nextKey );
            }
        }

        private static void ViReplaceBrace( ConsoleKeyInfo? key, object arg )
        {
            ViDeleteBrace( key, arg );
            ViInsMode( key, arg );
        }

        private static void ViBackwardReplaceLineToFirstChar( ConsoleKeyInfo? key, object arg )
        {
            ViBackwardDeleteLineToFirstChar( key, arg );
            ViInsMode( key, arg );
        }

        private static void ViBackwardReplaceLine( ConsoleKeyInfo? key, object arg )
        {
            BackwardDeleteLine( key, arg );
            ViInsMode( key, arg );
        }

        private static void ViBackwardReplaceChar( ConsoleKeyInfo? key, object arg )
        {
            BackwardDeleteChar( key, arg );
            ViSingleCharInsMode( key, arg );
        }

        private static void ViBackwardReplaceWord( ConsoleKeyInfo? key, object arg )
        {
            ViBackwardDeleteWord( key, arg );
            ViInsMode( key, arg );
        }

        private static void ViReplaceToEnd( ConsoleKeyInfo? key, object arg )
        {
            ViDeleteToEnd( key, arg );
            _singleton._current++;
            _singleton.PlaceCursor();
            ViInsMode( key, arg );
        }

        private static void ViReplaceLine( ConsoleKeyInfo? key, object arg )
        {
            ViDeleteLine( key, arg );
            ViInsMode( key, arg );
        }

        private static void ViReplaceWord( ConsoleKeyInfo? key, object arg )
        {
            ViDeleteWord( key, arg );
            if( _singleton._current < _singleton._buffer.Length - 1 )
            {
                Insert( ' ' );
                _singleton._current--;
                _singleton.PlaceCursor();
            }
            ViInsMode( key, arg );
        }

        private static void ViReplaceChar( ConsoleKeyInfo? key, object arg )
        {
            DeleteChar( key, arg );
            ViSingleCharInsMode( key, arg );
        }

        private static void ViReplaceCharInPlace( ConsoleKeyInfo? key, object arg )
        {
            ConsoleKeyInfo nextKey = ReadKey();
            if( nextKey.KeyChar > 0 && nextKey.Key != ConsoleKey.Escape && nextKey.Key != ConsoleKey.Enter )
            {
                _singleton.StartEditGroup();
                _singleton.SaveEditItem( EditItemDelete.Create( _singleton._buffer[_singleton._current].ToString(), _singleton._current ) );
                _singleton.SaveEditItem( EditItemInsertString.Create( nextKey.KeyChar.ToString(), _singleton._current ) );
                _singleton.EndEditGroup();

                _singleton._buffer[_singleton._current] = nextKey.KeyChar;
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }

        private static void ViSingleCharInsMode( ConsoleKeyInfo? key = null, object arg = null )
        {
            ConsoleKeyInfo secondKey = ReadKey();
            _singleton.ProcessOneKey( secondKey, _viInsKeyMap, ignoreIfNoAction: false, arg: arg );
        }

        /// <summary>
        /// Delete to the end of the line.
        /// </summary>
        private static void ViDeleteToEnd( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( _singleton._current >= _singleton._buffer.Length )
            {
                Ding();
                return;
            }

            _singleton.SaveEditItem( EditItemDelete.Create(
                _singleton._buffer.ToString( _singleton._current, _singleton._buffer.Length - _singleton._current ),
                _singleton._current,
                ViDeleteToEnd,
                arg
                ) );
            _singleton._buffer.Remove( _singleton._current, _singleton._buffer.Length - _singleton._current );
            _singleton._current = _singleton._buffer.Length - 1;
            _singleton.Render();
        }

        /// <summary>
        /// Delete the next word.
        /// </summary>
        private static void ViDeleteWord( ConsoleKeyInfo? key = null, object arg = null )
        {
            int qty = ( arg is int ) ? (int) arg : 1;
            int endPoint = _singleton._current;
            for( int i = 0; i < qty; i++ )
            {
                endPoint = _singleton.ViFindNextWordPointFrom( endPoint, _singleton.Options.WordDelimiters );
            }

            if( endPoint <= _singleton._current )
            {
                Ding();
                return;
            }
            _singleton.SaveEditItem( EditItemDelete.Create(
                _singleton._buffer.ToString( _singleton._current, endPoint - _singleton._current ),
                _singleton._current,
                ViDeleteWord,
                arg
                ) );
            _singleton._buffer.Remove( _singleton._current, endPoint - _singleton._current );
            _singleton.Render();
        }

        /// <summary>
        /// Delete to the end of the word.
        /// </summary>
        private static void ViDeleteToEndOfWord( ConsoleKeyInfo? key = null, object arg = null )
        {
            int qty = ( arg is int ) ? (int) arg : 1;
            int endPoint = _singleton._current;
            for( int i = 0; i < qty; i++ )
            {
                endPoint = _singleton.ViFindNextWordEnd( _singleton.Options.WordDelimiters );
            }

            if( endPoint <= _singleton._current )
            {
                Ding();
                return;
            }
            _singleton.SaveEditItem( EditItemDelete.Create(
                _singleton._buffer.ToString( _singleton._current, endPoint - _singleton._current ),
                _singleton._current,
                ViDeleteWord,
                arg
                ) );
            _singleton._buffer.Remove( _singleton._current, endPoint - _singleton._current );
            if( _singleton._current >= _singleton._buffer.Length )
            {
                _singleton._current = _singleton._buffer.Length - 1;
            }
            _singleton.Render();
        }

        private static void Ding( ConsoleKeyInfo? key = null, object arg = null )
        {
            Ding();
        }

        /// <summary>
        /// Switch the current operating mode from Vi-Insert to Vi-Command.
        /// </summary>
        public static void ViCmdMode( ConsoleKeyInfo? key = null, object arg = null )
        {
            _singleton._dispatchTable = _viCmdKeyMap;
            BackwardChar();
            _singleton.PlaceCursor();
        }

        public static void ViInsMode( ConsoleKeyInfo? key = null, object arg = null )
        {
            _singleton._dispatchTable = _viInsKeyMap;
        }

        public static void ViInsModeAtBegining( ConsoleKeyInfo? key = null, object arg = null )
        {
            ViInsMode( key, arg );
            BeginningOfLine( key, arg );
        }

        public static void ViInsModeAtEnd( ConsoleKeyInfo? key = null, object arg = null )
        {
            ViInsMode( key, arg );
            EndOfLine( key, arg );
        }

        public static void ViInsModeWithAppend( ConsoleKeyInfo? key = null, object arg = null )
        {
            ViInsMode( key, arg );
            ForwardChar( key, arg );
        }

        public static void ViInsModeWithDelete( ConsoleKeyInfo? key = null, object arg = null )
        {
            ViInsMode( key, arg );
            DeleteChar( key, arg );
        }

        public static void ViAcceptLine( ConsoleKeyInfo? key = null, object arg = null )
        {
            ViInsMode( key, arg );
            AcceptLine( key, arg );
        }

        public static void ViCommentLine( ConsoleKeyInfo? key = null, object arg = null )
        {
            BeginningOfLine( key, arg );
            SelfInsert( key, arg );
            ViAcceptLine( key, arg );
        }

        /// <summary>
        /// Invert the case of the current character and move to the next one.
        /// </summary>
        private static void ViInvertCase( ConsoleKeyInfo? key, object arg )
        {
            if( _singleton._current >= _singleton._buffer.Length )
            {
                Ding();
                return;
            }

            int qty = ( arg is int ) ? (int) arg : 1;

            for( int i = 0; i < qty && _singleton._current < _singleton._buffer.Length; i++ )
            {
                char c = _singleton._buffer[_singleton._current];
                if( Char.IsLetter( c ) )
                {
                    char newChar = Char.IsUpper( c ) ? Char.ToLower( c ) : char.ToUpper( c );
                    EditItem delEditItem = EditItemDelete.Create( c.ToString(), _singleton._current );
                    EditItem insEditItem = EditItemInsertChar.Create( newChar, _singleton._current );
                    _singleton.SaveEditItem( GroupedEdit.Create( new List<EditItem> 
                        {
                            delEditItem,
                            insEditItem
                        },
                        ViInvertCase,
                        arg
                    ) );

                    _singleton._buffer[_singleton._current] = newChar;
                }
                _singleton._current = Math.Min( _singleton._current + 1, _singleton._buffer.Length );
                _singleton.PlaceCursor();
            }
            _singleton.Render();
        }

        /// <summary>
        /// Swap the current character and the one before it.
        /// </summary>
        private static void ViTransposeChars( ConsoleKeyInfo? arg1, object arg2 )
        {
            if( _singleton._current <= 0 || _singleton._current >= _singleton._buffer.Length )
            {
                Ding();
                return;
            }

            char current = _singleton._buffer[_singleton._current];
            char previous = _singleton._buffer[_singleton._current - 1];

            _singleton.StartEditGroup();
            _singleton.SaveEditItem( EditItemDelete.Create( _singleton._buffer.ToString( _singleton._current - 1, 2 ), _singleton._current - 1 ) );
            _singleton.SaveEditItem( EditItemInsertChar.Create( current, _singleton._current - 1 ) );
            _singleton.SaveEditItem( EditItemInsertChar.Create( previous, _singleton._current ) );
            _singleton.EndEditGroup();

            _singleton._buffer[_singleton._current] = previous;
            _singleton._buffer[_singleton._current - 1] = current;
            _singleton._current = Math.Min( _singleton._current + 1, _singleton._buffer.Length - 1 );
            _singleton.PlaceCursor();
            _singleton.Render();
        }

        /// <summary>
        /// Move to the column indicated by arg.
        /// </summary>
        public static void ViGotoColumn( ConsoleKeyInfo? key = null, object arg = null )
        {
            int col = ( arg is int ) ? (int) arg : 1;
            if( col > 0 && col <= _singleton._buffer.Length )
            {
                _singleton._current = Math.Min( col, _singleton._buffer.Length ) - 1;
            }
            else
            {
                _singleton._current = _singleton._buffer.Length - 1;
                Ding();
            }
            _singleton.PlaceCursor();
        }

        /// <summary>
        /// Move the cursor to the end of the input.
        /// </summary>
        public static void ViFirstNonBlankOfLine( ConsoleKeyInfo? key = null, object arg = null )
        {
            for( int i = 0; i < _singleton._buffer.Length; i++ )
            {
                if( !Char.IsWhiteSpace( _singleton._buffer[i] ) )
                {
                    _singleton._current = i;
                    _singleton.PlaceCursor();
                    return;
                }
            }
        }

        /// <summary>
        /// Deletes text from the cursor to the first non-blank character of the line,
        /// </summary>
        public static void ViBackwardDeleteLineToFirstChar( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( _singleton._current > 0 )
            {
                int i = 0;
                for( ; i < _singleton._current; i++ )
                {
                    if( !Char.IsWhiteSpace( _singleton._buffer[i] ) )
                    {
                        break;
                    }
                }

                var str = _singleton._buffer.ToString( i, _singleton._current - i );
                _singleton.SaveEditItem( EditItemDelete.Create( str, i, ViBackwardDeleteLineToFirstChar ) );

                _singleton._buffer.Remove( i, _singleton._current - i );
                _singleton._current = i;
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }

        /// <summary>
        /// Deletes the current line, enabling undo.
        /// </summary>
        public static void ViDeleteLine( ConsoleKeyInfo? key = null, object arg = null )
        {
            _singleton.SaveEditItem( EditItemDelete.Create( _singleton._buffer.ToString(), 0 ) );
            _singleton._current = 0;
            _singleton._buffer.Remove( 0, _singleton._buffer.Length );
            _singleton.Render();
        }

        public static void ViBackwardDeleteWord( ConsoleKeyInfo? key = null, object arg = null )
        {
            int qty = ( arg is int ) ? (int) arg : 1;
            int deletePoint = _singleton._current;
            for( int i = 0; i < qty; i++ )
            {
                deletePoint = _singleton.ViFindPreviousWordPointFrom( deletePoint, _singleton.Options.WordDelimiters );
            }
            if( deletePoint == _singleton._current )
            {
                Ding();
                return;
            }
            _singleton.SaveEditItem( EditItemDelete.Create(
                _singleton._buffer.ToString( deletePoint, _singleton._current - deletePoint ),
                deletePoint,
                ViBackwardDeleteWord,
                arg
                ) );
            _singleton._buffer.Remove( deletePoint, _singleton._current - deletePoint );
            _singleton._current = deletePoint;
            _singleton.Render();
        }

        public static void ViGotoMatchingBrace( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( _singleton._current >= _singleton._buffer.Length )
            {
                Ding();
                return;
            }

            char current = _singleton._buffer[_singleton._current];
            if( current == '[' || current == '(' || current == '{' )
            {
                int other = ViFindNext( ViInverseOf( current ) );
                if( other != -1 )
                {
                    _singleton._current = other;
                    _singleton.PlaceCursor();
                    return;
                }
            }
            if( current == ']' || current == ')' || current == '}' )
            {
                int other = ViFindPrevious( ViInverseOf( current ) );
                if( other != -1 )
                {
                    _singleton._current = other;
                    _singleton.PlaceCursor();
                    return;
                }
            }

            Ding();
        }

        /// <summary>
        /// Find the matching brace, paren, or square bracket and delete all contents within, including the brace.
        /// </summary>
        public static void ViDeleteBrace( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( _singleton._current >= _singleton._buffer.Length )
            {
                Ding();
                return;
            }

            char current = _singleton._buffer[_singleton._current];
            if( current == '[' || current == '(' || current == '{' )
            {
                int other = ViFindNext( ViInverseOf( current ) );
                if( other != -1 )
                {
                    ViDeleteRange( _singleton._current, other, ViDeleteBrace );
                    return;
                }
            }
            if( current == ']' || current == ')' || current == '}' )
            {
                int other = ViFindPrevious( ViInverseOf( current ) );
                if( other != -1 )
                {
                    ViDeleteRange( other, _singleton._current, ViDeleteBrace );
                    return;
                }
            }

            Ding();
        }


        /// <summary>
        /// Undo all previous edits.
        /// </summary>
        public static void ViUndoAll( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( _singleton._undoEditIndex > 0 )
            {
                while( _singleton._undoEditIndex > 0 )
                {
                    _singleton._edits[_singleton._undoEditIndex - 1].Undo( _singleton );
                    _singleton._undoEditIndex--;
                }
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }

        /// <summary>
        /// Delete all characters included in the supplied range.
        /// </summary>
        /// <param name="first">Index of where to begin the delete.</param>
        /// <param name="last">Index of where to end the delete.</param>
        /// <param name="action">Action that generated this request, used for repeat command ('.').</param>
        private static void ViDeleteRange( int first, int last, Action<ConsoleKeyInfo?, object> action )
        {
            int length = last - first + 1;

            _singleton.SaveEditItem( EditItemDelete.Create( _singleton._buffer.ToString( first, length ), first, action ) );
            _singleton._current = first;
            _singleton._buffer.Remove( first, length );
            _singleton.Render();
        }

        /// <summary>
        /// Find the next instance of the indicated character, typically a brace.
        /// </summary>
        /// <param name="brace">The character to find.</param>
        /// <returns>-1 if no instance is found.</returns>
        private static int ViFindNext( char brace )
        {
            if( brace == char.MinValue )
            {
                return -1;
            }

            int i = _singleton._current + 1;
            for( ; i < _singleton._buffer.Length; i++ )
            {
                if( brace.Equals( _singleton._buffer[i] ) )
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Find the previous instance of the indicated character, typically a brace.
        /// </summary>
        /// <param name="brace">The character to find.</param>
        /// <returns>-1 if no instance is found.</returns>
        private static int ViFindPrevious( char brace )
        {
            if( brace == char.MinValue )
            {
                return -1;
            }
            int i = _singleton._current - 1;
            for( ; i >= 0 && i < _singleton._buffer.Length; i-- )
            {
                if( brace.Equals( _singleton._buffer[i] ) )
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Returns the inverse of the supplied brace.
        /// </summary>
        /// <param name="brace">The supplied brace.</param>
        /// <returns>char.MinValue if there is no inverse.</returns>
        private static char ViInverseOf( char brace )
        {
            switch( brace )
            {
                case '[':
                    return ']';
                case ']':
                    return '[';
                case '(':
                    return ')';
                case ')':
                    return '(';
                case '{':
                    return '}';
                case '}':
                    return '{';
            }
            return char.MinValue;
        }

        /// <summary>
        /// Prompts for a search string and initiates search upon AcceptLine.
        /// </summary>
        public static void ViStartSearchBackward( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( !key.HasValue || char.IsControl( key.Value.KeyChar ) )
            {
                Ding();
                return;
            }

            _singleton.ViStartSearch( backward: true );
        }

        /// <summary>
        /// Prompts for a search string and initiates search upon AcceptLine.
        /// </summary>
        public static void ViStartSearchForward( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( !key.HasValue || char.IsControl( key.Value.KeyChar ) )
            {
                Ding();
                return;
            }

            _singleton.ViStartSearch( backward: false );
        }

        /// <summary>
        /// Repeat the last search in the same direction as before.
        /// </summary>
        public static void ViRepeatSearch( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( string.IsNullOrEmpty( _singleton._searchHistoryPrefix ) )
            {
                Ding();
                return;
            }

            _singleton.ViHistorySearch();
        }

        /// <summary>
        /// Repeat the last search in the same direction as before.
        /// </summary>
        public static void ViRepeatSearchBackward( ConsoleKeyInfo? key = null, object arg = null )
        {
            _singleton._searchHistoryBackward = !_singleton._searchHistoryBackward;
            ViRepeatSearch();
            _singleton._searchHistoryBackward = !_singleton._searchHistoryBackward;
        }

        private void ViStartSearch( bool backward )
        {
            _statusLinePrompt = "find: ";
            var argBuffer = _statusBuffer;
            Render(); // Render prompt

            while( true )
            {
                var nextKey = ReadKey();
                if( nextKey.Key == Keys.Enter.Key )
                {
                    _searchHistoryPrefix = argBuffer.ToString();
                    _searchHistoryBackward = backward;
                    ViHistorySearch();
                    break;
                }
                if( nextKey.Key == Keys.Escape.Key )
                {
                    break;
                }
                if( nextKey.Key == Keys.Backspace.Key )
                {
                    if( argBuffer.Length > 0 )
                    {
                        argBuffer.Remove( argBuffer.Length - 1, 1 );
                        Render(); // Render prompt
                        continue;
                    }
                    break;
                }
                argBuffer.Append( nextKey.KeyChar );
                Render(); // Render prompt
            }

            // Remove our status line
            argBuffer.Clear();
            _statusLinePrompt = null;
            Render(); // Render prompt
        }

        private void ViHistorySearch()
        {
            _searchHistoryCommandCount++;

            int incr = _searchHistoryBackward ? -1 : +1;
            for( int i = _currentHistoryIndex + incr; i >= 0 && i < _history.Count; i += incr )
            {
                if( Options.HistoryStringComparison.HasFlag( StringComparison.OrdinalIgnoreCase ) )
                {
                    if( _history[i]._line.ToLower().Contains( _searchHistoryPrefix.ToLower() ) )
                    {
                        _currentHistoryIndex = i;
                        UpdateFromHistory( moveCursor: Options.HistorySearchCursorMovesToEnd );
                        return;
                    }
                }
                else
                {
                    if( _history[i]._line.Contains( _searchHistoryPrefix ) )
                    {
                        _currentHistoryIndex = i;
                        UpdateFromHistory( moveCursor: Options.HistorySearchCursorMovesToEnd );
                        return;
                    }
                }
            }

            Ding();
        }

        /// <summary>
        /// Repeat the last text modification.
        /// </summary>
        public static void ViRepeatLastMod( ConsoleKeyInfo? key = null, object arg = null )
        {
            if( _singleton._undoEditIndex > 0 )
            {
                EditItem editItem = _singleton._edits[_singleton._undoEditIndex - 1];
                if( editItem._instigator != null )
                {
                    editItem._instigator( key, editItem._instigatorArg );
                    return;
                }
            }
            Ding();
        }
    }
}
