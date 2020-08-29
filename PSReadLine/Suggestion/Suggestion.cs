/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private readonly Prediction _prediction;

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

                using var _ = prediction.DisableScoped();
                Replace(0, _singleton._buffer.Length, inlineView.SuggestionText);
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

        private static void AcceptNextSuggestionWord(int numericArg)
        {
            if (_singleton._prediction.ActiveView is PredictionInlineView inlineView && inlineView.HasActiveSuggestion)
            {
                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                int index = _singleton._buffer.Length;
                while (numericArg-- > 0 && index < inlineView.SuggestionText.Length)
                {
                    index = inlineView.FindForwardSuggestionWordPoint(index, _singleton.Options.WordDelimiters);
                }

                Replace(0, _singleton._buffer.Length, inlineView.SuggestionText.Substring(0, index));
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

        private static bool UpdateListSelection(int numericArg, bool calledFromPreviousHistory = false)
        {
            if (_singleton._prediction.ActiveView is PredictionListView listView && listView.HasActiveSuggestion)
            {
                if (calledFromPreviousHistory && listView.SelectedItemIndex == -1)
                {
                    return false;
                }

                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                listView.UpdateListSelection(move: numericArg);
                Replace(0, _singleton._buffer.Length, listView.SelectedItemText);

                return true;
            }

            return false;
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

        private class Prediction
        {
            private readonly PSConsoleReadLine _singleton;

            private bool _showPrediction = true;
            private bool _pauseQuery = false;
            private PredictionListView _listView;
            private PredictionInlineView _inlineView;

            private bool IsPredictionOn => _singleton._options.PredictionSource != PredictionSource.None && _showPrediction;

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
            /// Pause the query for predictive suggestions within the calling scope. 
            /// Note: the calling method need to use this method with the 'using' statement/variable,
            /// so that the suggestion feature can be properly restored.
            /// </summary>
            internal IDisposable PauseQuery()
            {
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
        }
    }
}
