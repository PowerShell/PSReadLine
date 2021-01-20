/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private void RemoveEditsAfterUndo()
        {
            // If there is some sort of edit after an undo, forget
            // any edit items that were undone.
            int removeCount = _edits.Count - _undoEditIndex;
            if (removeCount > 0)
            {
                _edits.RemoveRange(_undoEditIndex, removeCount);
                if (_edits.Count < _editGroupStart)
                {
                    // Reset the group start index if any edits before setting the start mark were undone.
                    _editGroupStart = -1;
                }
            }
        }

        private void SaveEditItem(EditItem editItem)
        {
            if (_statusIsErrorMessage)
            {
                // After an edit, clear the error message
                ClearStatusMessage(render: true);
            }

            if (IsLastEditItemReplaceable)
            {
                int lastEditIndex = _edits.Count - 1;
                if (editItem.Replaceable)
                {
                    if (_edits[lastEditIndex] is GroupedEdit groupedEdit)
                    {
                        var groupedEdits = groupedEdit._groupedEditItems;
                        groupedEdits[groupedEdits.Count - 1] = editItem;
                    }
                    else
                    {
                        _edits[lastEditIndex] = editItem;
                    }

                    return;
                }

                _edits[lastEditIndex].Replaceable = false;
            }

            _edits.Add(editItem);
            _undoEditIndex = _edits.Count;
        }

        private void StartEditGroup()
        {
            if (_editGroupStart != -1)
            {
                // Nesting not supported.
                throw new InvalidOperationException();
            }

            RemoveEditsAfterUndo();
            _editGroupStart = _edits.Count;
        }

        private void EndEditGroup(Action<ConsoleKeyInfo?, object> instigator = null, object instigatorArg = null)
        {
            // Remove the undone edits when closing an edit group, so the generated group
            // doesn't contain those edits that were already undone.
            RemoveEditsAfterUndo();

            // If any edits before the start mark were done, the start mark will be reset
            // and no need to generate the edit group.
            if (_editGroupStart < 0)
            {
                return;
            }

            var groupEditCount = _edits.Count - _editGroupStart;

            // It's possible that just enough edits were undone and now 'groupEditCount' is 0.
            // We don't generate the edit group in that case.
            if (groupEditCount > 0)
            {
                var groupedEditItems = _edits.GetRange(_editGroupStart, groupEditCount);
                _edits.RemoveRange(_editGroupStart, groupEditCount);
                SaveEditItem(GroupedEdit.Create(groupedEditItems, instigator, instigatorArg));
            }
            _editGroupStart = -1;
        }

        private bool IsLastEditItemReplaceable
        {
            get
            {
                RemoveEditsAfterUndo();

                int lastEditIndex = _edits.Count - 1;
                return lastEditIndex >= 0 && _edits[lastEditIndex].Replaceable;
            }
        }

        /// <summary>
        /// Undo a previous edit.
        /// </summary>
        public static void Undo(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._undoEditIndex > 0)
            {
                if (_singleton._statusIsErrorMessage)
                {
                    // After an edit, clear the error message
                    _singleton.ClearStatusMessage(render: false);
                }
                _singleton._edits[_singleton._undoEditIndex - 1].Undo();
                _singleton._undoEditIndex--;

                if (_singleton._options.EditMode == EditMode.Vi && _singleton._current >= _singleton._buffer.Length)
                {
                    _singleton._current = Math.Max(0, _singleton._buffer.Length + ViEndOfLineFactor);
                }
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }

        /// <summary>
        /// Undo an undo.
        /// </summary>
        public static void Redo(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._undoEditIndex < _singleton._edits.Count)
            {
                _singleton._edits[_singleton._undoEditIndex].Redo();
                _singleton._undoEditIndex++;
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }

        internal abstract class EditItem
        {
            public Action<ConsoleKeyInfo?, object> _instigator;
            public object _instigatorArg;

            public abstract void Undo();
            public abstract void Redo();
            public virtual bool Replaceable { get; set; }
        }

        [DebuggerDisplay("Insert '{_insertedCharacter}' ({_insertStartPosition})")]
        class EditItemInsertChar : EditItem
        {
            // The character inserted is not needed for undo, only for redo
            private char _insertedCharacter;
            private int _insertStartPosition;

            public static EditItem Create(char character, int position)
            {
                return new EditItemInsertChar
                {
                    _insertedCharacter = character,
                    _insertStartPosition = position
                };
            }

            public override void Undo()
            {
                Debug.Assert(_singleton._buffer[_insertStartPosition] == _insertedCharacter, "Character to undo is not what it should be");
                _singleton._buffer.Remove(_insertStartPosition, 1);
                _singleton._current = _insertStartPosition;
            }

            public override void Redo()
            {
                _singleton._buffer.Insert(_insertStartPosition, _insertedCharacter);
                _singleton._current++;
            }
        }

        [DebuggerDisplay("Insert '{_insertedString}' ({_insertStartPosition})")]
        class EditItemInsertString : EditItem
        {
            // The string inserted tells us the length to delete on undo.
            // The contents of the string are only needed for redo.
            private readonly string _insertedString;
            private readonly int _insertStartPosition;

            protected EditItemInsertString(string str, int position)
            {
                _insertedString = str;
                _insertStartPosition = position;
            }

            public static EditItem Create(string str, int position)
            {
                return new EditItemInsertString(str, position);
            }

            public override void Undo()
            {
                Debug.Assert(_singleton._buffer.ToString(_insertStartPosition, _insertedString.Length).Equals(_insertedString),
                    "Character to undo is not what it should be");
                _singleton._buffer.Remove(_insertStartPosition, _insertedString.Length);
                _singleton._current = _insertStartPosition;
            }

            public override void Redo()
            {
                _singleton._buffer.Insert(_insertStartPosition, _insertedString);
                _singleton._current += _insertedString.Length;
            }
        }

        [DebuggerDisplay("Insert '{_insertedString}' ({_insertStartPosition}, Anchor: {_insertAnchor})")]
        class EditItemInsertLines : EditItemInsertString
        {
            // in linewise pastes, the _insertAnchor represents the position
            // of the cursor at the time paste was invoked. This is recorded
            // so as to be restored when undoing the paste.
            private readonly int _insertAnchor;

            private EditItemInsertLines(string str, int position, int anchor)
                :base(str, position)
            {
                _insertAnchor = anchor;
            }

            public static EditItem Create(string str, int position, int anchor)
            {
                return new EditItemInsertLines(str, position, anchor);
            }

            public override void Undo()
            {
                base.Undo();
                _singleton._current = _insertAnchor;
            }
        }

        [DebuggerDisplay("Delete '{_deletedString}' ({_deleteStartPosition})")]
        class EditItemDelete : EditItem
        {
            private readonly string _deletedString;
            private readonly int _deleteStartPosition;

            // undoing a delete operation will insert some text starting from the _deleteStartPosition.
            // the _adjustCursorOnUndo flag specifies whether the cursor must be adjusted.
            // by default the cursor will move to end of the inserted text.
            private readonly bool _adjustCursorOnUndo;

            protected EditItemDelete(string str, int position, Action<ConsoleKeyInfo?, object> instigator, object instigatorArg)
                : this(str, position, instigator, instigatorArg, true)
            {
            }

            protected EditItemDelete(string str, int position, Action<ConsoleKeyInfo?, object> instigator, object instigatorArg, bool adjustCursor)
            {
                _deletedString = str;
                _deleteStartPosition = position;
                _instigator = instigator;
                _instigatorArg = instigatorArg;
                _adjustCursorOnUndo = adjustCursor;
            }

            public static EditItem Create(string str, int position, Action<ConsoleKeyInfo?, object> instigator = null, object instigatorArg = null, bool adjustCursor = true)
            {
                return new EditItemDelete(
                    str,
                    position,
                    instigator,
                    instigatorArg,
                    adjustCursor);
            }

            public override void Undo()
            {
                var newCurrent = _deleteStartPosition;
                newCurrent += _adjustCursorOnUndo
                    ? _deletedString.Length
                    : 0
                    ;

                _singleton._buffer.Insert(_deleteStartPosition, _deletedString);
                _singleton._current = newCurrent;
            }

            public override void Redo()
            {
                _singleton._buffer.Remove(_deleteStartPosition, _deletedString.Length);
                _singleton._current = _deleteStartPosition;
            }
        }

        [DebuggerDisplay("DeleteLines '{_deletedString}' ({_deleteStartPosition}, Anchor: {_deleteAnchor})")]
        class EditItemDeleteLines : EditItemDelete
        {
            // in linewise deletes, the _deleteAnchor represents the position
            // of the cursor at the time delete was invoked. This is recorded
            // so as to be restored when undoing the delete.
            private readonly int _deleteAnchor;

            private EditItemDeleteLines(string str, int position, int anchor, Action<ConsoleKeyInfo?, object> instigator, object instigatorArg)
                : base(str, position, instigator, instigatorArg)
            {
                _deleteAnchor = anchor;
            }

            public static EditItem Create(string str, int position, int anchor, Action<ConsoleKeyInfo?, object> instigator = null, object instigatorArg = null)
            {
                return new EditItemDeleteLines(str, position, anchor, instigator, instigatorArg);
            }

            public override void Undo()
            {
                base.Undo();
                _singleton._current = _deleteAnchor;
            }
        }

        [DebuggerDisplay("SwapCharacters")]
        class EditItemSwapCharacters : EditItem
        {
            private readonly int _swapPosition;

            private EditItemSwapCharacters(int swapPosition)
            {
                _swapPosition = swapPosition;
            }

            public static EditItem Create(int swapPosition)
            { 
                return new EditItemSwapCharacters(swapPosition);
            }

            public override void Redo()
            {
                Undo();
            }

            public override void Undo()
            {
                _singleton.SwapCharactersImpl(_swapPosition);
            }
        }

        class GroupedEdit : EditItem
        {
            internal List<EditItem> _groupedEditItems;

            public static EditItem Create(List<EditItem> groupedEditItems, Action<ConsoleKeyInfo?, object> instigator = null, object instigatorArg = null)
            {
                return new GroupedEdit
                {
                    _groupedEditItems = groupedEditItems,
                    _instigator = instigator,
                    _instigatorArg = instigatorArg
                };
            }

            public override void Undo()
            {
                for (int i = _groupedEditItems.Count - 1; i >= 0; i--)
                {
                    _groupedEditItems[i].Undo();
                }
            }

            public override void Redo()
            {
                foreach (var editItem in _groupedEditItems)
                {
                    editItem.Redo();
                }
            }

            public override bool Replaceable
            {
                get => _groupedEditItems[_groupedEditItems.Count - 1].Replaceable;
                set => _groupedEditItems[_groupedEditItems.Count - 1].Replaceable = value;
            }
        }
    }
}
