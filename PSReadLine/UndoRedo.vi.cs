/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Encapsulates state and behaviour for an edit group.
        /// 
        /// An edit group is a sequence of distinct internal changes
        /// to the buffer that are considered to be a single user-facing
        /// change for the purpose of the undo/redo actions.
        ///
        /// </summary>
        internal struct GroupUndoHelper
        {
            public Action<ConsoleKeyInfo?, object> _instigator;
            public object _instigatorArg;

            public void StartGroup(Action<ConsoleKeyInfo?, object> instigator, object instigatorArg)
            {
                if (_singleton._editGroupStart != -1)
                {
                    // a nested "start" of a group is being made
                    // we need to record the state of the preceding start

                    _singleton._groupUndoStates.Push(
                        new GroupUndoState(_singleton._groupUndoHelper, _singleton._editGroupStart));
                }

                _instigator = instigator;
                _instigatorArg = instigatorArg;
                _singleton.StartEditGroup();
            }

            public void Clear()
            {
                _instigator = null;
                _instigatorArg = null;
            }

            public void EndGroup()
            {
                if (_singleton._editGroupStart >= 0)
                {
                    _singleton.EndEditGroup(_instigator, _instigatorArg);

                    if (_singleton._groupUndoStates.Count > 0)
                    {
                        var groupUndoState = _singleton._groupUndoStates.Pop();
                        _singleton._editGroupStart = groupUndoState.EditGroupStart;
                        groupUndoState.GroupUndoHelper.EndGroup();
                    }
                }
                Clear();
            }
        }
        private GroupUndoHelper _groupUndoHelper = new GroupUndoHelper();

        /// <summary>
        /// Records states of changes made as part of an edit group.
        /// 
        /// In an ideal situation, edit groups would be started and ended
        /// in balanced pairs of calls. However, we expose public methods
        /// that may start and edit group and rely on future actions to
        /// properly end the group.
        /// 
        /// To improve robustness of the code, we allow starting "nested"
        /// edit groups and end the whole sequence of groups once. That is,
        /// a single call to _singleton.EndEditGroup() will properly record the
        /// changes made up to that point from calls to _singleton.StartEditGroup()
        /// that have been made at different points in the overall sequence of changes.
        ///
        /// </summary>
        internal class GroupUndoState
        {
            public GroupUndoState(GroupUndoHelper undoHelper, int editGroupStart)
            {
                GroupUndoHelper = undoHelper;
                EditGroupStart = editGroupStart;
            }
            public GroupUndoHelper GroupUndoHelper { get; private set; }
            public int EditGroupStart { get; private set; }
        }

        /// <summary>
        /// Records the sequence of "nested" starts of a edit group.
        /// </summary>
        private readonly Stack<GroupUndoState> _groupUndoStates = new Stack<GroupUndoState>();

        /// <summary>
        /// Undo all previous edits for line.
        /// </summary>
        public static void UndoAll(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._undoEditIndex > 0)
            {
                while (_singleton._undoEditIndex > 0)
                {
                    _singleton._edits[_singleton._undoEditIndex - 1].Undo();
                    _singleton._undoEditIndex--;
                }
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }
    }
    internal static class StackExtensions
    {
        /// <summary>
        /// This helper method copies the contents of the target stack
        /// with items from the supplied stack.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="origin"></param>
        /// <param name="target"></param>
        public static void CopyTo<T>(this Stack<T> origin, Stack<T> target)
        {
            target.Clear();
            foreach (var item in origin)
            {
                target.Push(item);
            }
        }
    }
}
