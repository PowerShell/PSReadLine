/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Language;
using System.Management.Automation.Subsystem.Prediction;
using System.Diagnostics.CodeAnalysis;
using Microsoft.PowerShell.Internal;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private const string DefaultName = "PSReadLine";
        private static readonly PredictionClient s_predictionClient = new(DefaultName, PredictionClientKind.Terminal);
        private static PropertyInfo s_pCurrentLocation = null;

        /// <summary>
        /// Initialize the <see cref="PropertyInfo"/> objects for those public settable properties newly added to
        /// <see cref="PredictionClient"/>.
        /// </summary>
        private static void InitializePropertyInfo()
        {
            Version ver = typeof(PSObject).Assembly.GetName().Version;
            if (ver.Major < 7 || ver.Minor < 4)
            {
                return;
            }

            Type pcType = typeof(PredictionClient);
            // Property added in 7.4
            s_pCurrentLocation = pcType.GetProperty("CurrentLocation");
        }

        /// <summary>
        /// New public settable properties may be added to the <see cref="PredictionClient"/> type as it evolves to
        /// offer more helpful context information. We dynamically set those properties here to avoid any backward
        /// compatibility issues.
        /// </summary>
        private static void UpdatePredictionClient(Runspace runspace, EngineIntrinsics engineIntrinsics)
        {
            // Set the current location if the 'CurrentLocation' property exists.
            if (s_pCurrentLocation is not null)
            {
                // Set the current location if it's a local Runspace. Otherwise, set it to null.
                object path = runspace is null || runspace.RunspaceIsRemote
                    ? null
                    : engineIntrinsics?.SessionState.Path.CurrentLocation;
                s_pCurrentLocation.SetValue(s_predictionClient, path);
            }
        }

        // Stub helper methods so prediction can be mocked
        [ExcludeFromCodeCoverage]
        Task<List<PredictionResult>> IPSConsoleReadLineMockableMethods.PredictInputAsync(Ast ast, Token[] tokens)
        {
            return CommandPrediction.PredictInputAsync(s_predictionClient, ast, tokens);
        }

        [ExcludeFromCodeCoverage]
        void IPSConsoleReadLineMockableMethods.OnSuggestionDisplayed(Guid predictorId, uint session, int countOrIndex)
        {
            CommandPrediction.OnSuggestionDisplayed(s_predictionClient, predictorId, session, countOrIndex);
        }

        [ExcludeFromCodeCoverage]
        void IPSConsoleReadLineMockableMethods.OnSuggestionAccepted(Guid predictorId, uint session, string suggestionText)
        {
            CommandPrediction.OnSuggestionAccepted(s_predictionClient, predictorId, session, suggestionText);
        }

        [ExcludeFromCodeCoverage]
        void IPSConsoleReadLineMockableMethods.OnCommandLineAccepted(IReadOnlyList<string> history)
        {
            CommandPrediction.OnCommandLineAccepted(s_predictionClient, history);
        }

        [ExcludeFromCodeCoverage]
        void IPSConsoleReadLineMockableMethods.OnCommandLineExecuted(string commandLine, bool success)
        {
            CommandPrediction.OnCommandLineExecuted(s_predictionClient, commandLine, success);
        }

        private readonly Prediction _prediction;

        /// <summary>
        /// Report the execution result (success or failure) of the last accepted command line.
        /// </summary>
        /// <param name="success">Whether the execution was successful.</param>
        private void ReportExecutionStatus(bool success)
        {
            _prediction.OnCommandLineExecuted(_acceptedCommandLine, success);
        }

        /// <summary>
        /// Accept the suggestion text if there is one.
        /// </summary>
        public static void AcceptSuggestion(ConsoleKeyInfo? key = null, object arg = null)
        {
            Prediction prediction = _singleton._prediction;
            if (prediction.ActiveView is PredictionInlineView inlineView && inlineView.HasActiveSuggestion)
            {
                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                inlineView.OnSuggestionAccepted();

                using var _ = prediction.DisableScoped();

                _singleton._current = _singleton._buffer.Length;
                Insert(inlineView.SuggestionText.Substring(_singleton._current));
            }
        }

        /// <summary>
        /// Accept the current or the next suggestion text word.
        /// </summary>
        public static void AcceptNextSuggestionWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!TryGetArgAsInt(arg, out var numericArg, 1))
            {
                return;
            }

            if (numericArg > 0)
            {
                AcceptNextSuggestionWord(numericArg);
            }
        }

        /// <summary>
        /// Implementation for accepting the current or the next suggestion text word.
        /// </summary>
        private static void AcceptNextSuggestionWord(int numericArg)
        {
            if (_singleton._prediction.ActiveView is PredictionInlineView inlineView && inlineView.HasActiveSuggestion)
            {
                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                int start = _singleton._buffer.Length;
                int index = start;
                while (numericArg-- > 0 && index < inlineView.SuggestionText.Length)
                {
                    index = inlineView.FindForwardSuggestionWordPoint(index, _singleton.Options.WordDelimiters);
                }

                inlineView.OnSuggestionAccepted();

                _singleton._current = start;
                Insert(inlineView.SuggestionText.Substring(start, index - start));
            }
        }

        /// <summary>
        /// Select the next suggestion item in the list view.
        /// </summary>
        public static void NextSuggestion(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, +1);
            UpdateListSelection(numericArg);
        }

        /// <summary>
        /// Select the previous suggestion item in the list view.
        /// </summary>
        public static void PreviousSuggestion(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, -1);
            if (numericArg > 0)
            {
                numericArg = -numericArg;
            }

            UpdateListSelection(numericArg);
        }

        /// <summary>
        /// Show the tooltip of the currently selected list item in the full view.
        /// </summary>
        public static void ShowFullPredictionTooltip(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._prediction.ActiveView is PredictionListView listView && listView.HasActiveSuggestion)
            {
                string tooltip = listView.SelectedItemTooltip;
                if (!string.IsNullOrWhiteSpace(tooltip))
                {
                    _singleton._mockableMethods.RenderFullHelp(tooltip, regexPatternToScrollTo: null);
                }
            }
        }

        /// <summary>
        /// Implementation for updating the selected item in list view.
        /// </summary>
        private static bool UpdateListSelection(int numericArg)
        {
            if (_singleton._prediction.ActiveView is PredictionListView listView && listView.HasActiveSuggestion)
            {
                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                listView.UpdateListSelection(move: numericArg);
                ReplaceSelection(listView.SelectedItemText);

                return true;
            }

            return false;
        }

        private static bool UpdateListByPaging(bool pageUp, int numericArg)
        {
            if (_singleton._prediction.ActiveView is PredictionListView listView && listView.HasActiveSuggestion)
            {
                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                if (listView.UpdateListByPaging(pageUp, numericArg))
                {
                    ReplaceSelection(listView.SelectedItemText);
                }

                return true;
            }

            return false;
        }

        private static bool UpdateListByLoopingSources(bool jumpUp, int numericArg)
        {
            if (_singleton._prediction.ActiveView is PredictionListView listView && listView.HasActiveSuggestion)
            {
                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                if (listView.UpdateListByLoopingSources(jumpUp, numericArg))
                {
                    ReplaceSelection(listView.SelectedItemText);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Replace current buffer with the selected list item text.
        /// The replacement is done in a way that allows further selection updates for the same list view
        /// to override the previous update in the undo/redo stack, so that 'undo' always get back to the
        /// original user input that triggers the current list view.
        /// </summary>
        private static void ReplaceSelection(string selectedItemText)
        {
            var insertStringItem = EditItemInsertString.Create(selectedItemText, position: 0);
            insertStringItem.Replaceable = true;

            if (_singleton.IsLastEditItemReplaceable)
            {
                _singleton.SaveEditItem(insertStringItem);
                _singleton._buffer.Clear();
                _singleton._buffer.Append(selectedItemText);
                _singleton._current = selectedItemText.Length;

                _singleton.Render();
                return;
            }

            bool useEditGroup = _singleton._editGroupStart == -1;
            if (useEditGroup)
            {
                _singleton.StartEditGroup();
            }

            var str = _singleton._buffer.ToString();
            _singleton.SaveEditItem(EditItemDelete.Create(str, position: 0));
            _singleton._buffer.Clear();

            _singleton.SaveEditItem(insertStringItem);
            _singleton._buffer.Append(selectedItemText);
            _singleton._current = selectedItemText.Length;

            if (useEditGroup)
            {
                _singleton.EndEditGroup(); // Instigator is needed for VI undo
                _singleton.Render();
            }
        }

        /// <summary>
        /// Switch to the other prediction view.
        /// </summary>
        public static void SwitchPredictionView(ConsoleKeyInfo? key = null, object arg = null)
        {
            int count = Enum.GetNames(typeof(PredictionViewStyle)).Length;
            int value = (int)_singleton._options.PredictionViewStyle;
            var style = (PredictionViewStyle)((value + 1) % count);

            _singleton._options.PredictionViewStyle = style;
            _singleton._prediction.SetViewStyle(style);
            _singleton.Render();
        }

        /// <summary>
        /// Write a warning message if the current window size is too small for the specified prediction view.
        /// </summary>
        internal static void WarnWhenWindowSizeTooSmallForView(PredictionViewStyle viewStyle, PSCmdlet cmdlet)
        {
            if (viewStyle != PredictionViewStyle.ListView)
            {
                return;
            }

            var console = _singleton._console;
            var minWidth = PredictionListView.MinWindowWidth;
            var minHeight = PredictionListView.MinWindowHeight;

            if (console.WindowWidth < minWidth || console.WindowHeight < minHeight)
            {
                cmdlet.WriteWarning(string.Format(PSReadLineResources.WindowSizeTooSmallForListView, minWidth, minHeight));
            }
        }

        /// <summary>
        /// The type that controls the predictive suggestion feature and exposes the active view.
        /// </summary>
        private class Prediction
        {
            private readonly PSConsoleReadLine _singleton;

            private bool _showPrediction = true;
            private bool _pauseQuery = false;
            private PredictionListView _listView;
            private PredictionInlineView _inlineView;

            /// <summary>
            /// Gets indication on whether the prediction feature is on.
            /// </summary>
            private bool IsPredictionOn => _singleton._options.PredictionSource != PredictionSource.None && _showPrediction;

            /// <summary>
            /// Gets the active prediction view.
            /// </summary>
            internal PredictionViewBase ActiveView { get; private set; }

            internal Prediction(PSConsoleReadLine singleton)
            {
                _singleton = singleton;
                SetViewStyle(PSConsoleReadLineOptions.DefaultPredictionViewStyle);
            }

            /// <summary>
            /// Set the active prediction view style.
            /// </summary>
            internal void SetViewStyle(PredictionViewStyle style)
            {
                ActiveView?.Reset();

                switch (style)
                {
                    case PredictionViewStyle.InlineView:
                        _inlineView ??= new PredictionInlineView(_singleton);
                        ActiveView = _inlineView;
                        break;

                    case PredictionViewStyle.ListView:
                        _listView ??= new PredictionListView(_singleton);
                        ActiveView = _listView;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(style));
                }
            }

            /// <summary>
            /// Reset the prediction component on 'ReadLine' initialization.
            /// </summary>
            internal void Reset()
            {
                // This field may be set to 'false' globally and left that way,
                // so we reset it on a new 'ReadLine' call just in case.
                _showPrediction = true;
                ActiveView.Reset();
            }

            /// <summary>
            /// Pause the query for predictive suggestions within the calling scope. 
            /// Note: the calling method need to use this method with the 'using' statement/variable,
            /// so that the suggestion feature can be properly restored.
            /// </summary>
            internal IDisposable PauseQuery()
            {
                if (!IsPredictionOn)
                {
                    // If the prediction is off, there is no need to pause the prediction query in the
                    // caller's lexical scope. So we return a non-op disposable object to avoid unneeded
                    // allocation.
                    return Disposable.NonOp;
                }

                var saved = _pauseQuery;
                _pauseQuery = true;

                return new Disposable(() => _pauseQuery = saved);
            }

            /// <summary>
            /// Turn off the prediction feature within the calling scope.
            /// Note: the calling method need to use this method with the 'using' statement/variable,
            /// so that the suggestion feature can be properly restored.
            /// </summary>
            internal IDisposable DisableScoped()
            {
                if (_singleton._options.PredictionSource == PredictionSource.None)
                {
                    // If the prediction source is set to 'None', then the prediction feature is already
                    // disabled. So we return a non-op disposable object to avoid unneeded allocation.
                    return Disposable.NonOp;
                }

                var saved = _showPrediction;
                _showPrediction = false;

                return new Disposable(() => _showPrediction = saved);
            }

            /// <summary>
            /// Turn off the prediction feature globally and also clear the current prediction view.
            /// </summary>
            internal void DisableGlobal(bool cursorAtEol)
            {
                _showPrediction = false;
                ActiveView.Clear(cursorAtEol);
            }

            /// <summary>
            /// Turn on the prediction feature globally.
            /// </summary>
            internal void EnableGlobal()
            {
                _showPrediction = true;
            }

            /// <summary>
            /// Query for predictive suggestions.
            /// </summary>
            internal void QueryForSuggestion(string userInput)
            {
                if (IsPredictionOn && (_pauseQuery || ActiveView.HasPendingUpdate))
                {
                    return;
                }

                if (!IsPredictionOn || string.IsNullOrWhiteSpace(userInput) || userInput.IndexOf('\n') != -1)
                {
                    ActiveView.Reset();
                    return;
                }

                ActiveView.GetSuggestion(userInput);
            }

            /// <summary>
            /// Revert the list view suggestion.
            /// Namely, clear the list view and revert the buffer to the original user input.
            /// </summary>
            internal bool RevertSuggestion()
            {
                bool retValue = false;
                if (ActiveView is PredictionListView listView && listView.HasActiveSuggestion)
                {
                    if (listView.SelectedItemIndex > -1 && _singleton._undoEditIndex > 0)
                    {
                        _singleton._edits[_singleton._undoEditIndex - 1].Undo();
                        _singleton._undoEditIndex--;
                    }

                    retValue = true;
                    using var _ = DisableScoped();
                    _singleton.Render();
                }

                return retValue;
            }

            /// <summary>
            /// Get called when a command line is accepted.
            /// </summary>
            internal void OnCommandLineAccepted(string commandLine)
            {
                if (ActiveView.UsePlugin && !string.IsNullOrWhiteSpace(commandLine))
                {
                    _singleton._mockableMethods.OnCommandLineAccepted(_singleton._recentHistory.ToArray());
                }
            }

            /// <summary>
            /// Get called when the last accepted command line finished execution.
            /// </summary>
            internal void OnCommandLineExecuted(string commandLine, bool success)
            {
                if (ActiveView.UsePlugin && !string.IsNullOrWhiteSpace(commandLine))
                {
                    _singleton._mockableMethods.OnCommandLineExecuted(commandLine, success);
                }
            }
        }
    }
}
