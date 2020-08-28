/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private bool _showSuggestion = true;
        private bool _pausePrediction = false;
        private PredictionViewBase _activePredictionView;

        private bool IsPredictionOn => _options.PredictionSource != PredictionSource.None && _showSuggestion;
        private bool IsPredictionOff => _options.PredictionSource == PredictionSource.None || !_showSuggestion;

        /// <summary>
        /// Turn off the prediction feature for the scope of the calling method.
        /// Note: the calling method need to use this method with the 'using' statement/variable,
        /// so that the suggestion feature can be properly restored.
        /// </summary>
        private IDisposable PredictionOff()
        {
            var saved = _showSuggestion;
            _showSuggestion = false;

            return new Disposable(() => _showSuggestion = saved);
        }

        private IDisposable PausePrediction()
        {
            var saved = _pausePrediction;
            _pausePrediction = true;

            return new Disposable(() => _pausePrediction = saved);
        }

        private void SetPredictionView(PredictionViewStyle style)
        {
            _activePredictionView?.Reset();

            switch (style)
            {
                case PredictionViewStyle.InlineView:
                    _activePredictionView = new PredictionInlineView(this);
                    break;

                case PredictionViewStyle.ListView:
                    _activePredictionView = new PredictionListView(this);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(style));
            }
        }

        private void PerformPrediction(string userInput)
        {
            if (IsPredictionOn && (_pausePrediction || _activePredictionView.HasPendingUpdate))
            {
                return;
            }

            if (IsPredictionOff || LineIsMultiLine() || string.IsNullOrWhiteSpace(userInput))
            {
                _activePredictionView.Reset();
                return;
            }

            _activePredictionView.GetSuggestion(userInput);
        }

        /// <summary>
        /// Accept the suggestion text if there is one.
        /// </summary>
        public static void AcceptSuggestion(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._activePredictionView is PredictionInlineView inlineView && inlineView.HasActiveSuggestion)
            {
                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                using var _ = _singleton.PredictionOff();
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
            if (_singleton._activePredictionView is PredictionInlineView inlineView && inlineView.HasActiveSuggestion)
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

        public static void NextSuggestion(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, +1);
            UpdateListSelection(numericArg);
        }

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
            if (_singleton._activePredictionView is PredictionListView listView && listView.HasActiveSuggestion)
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

        public static void SwitchPredictionView(ConsoleKeyInfo? key = null, object arg = null)
        {
            int count = Enum.GetNames(typeof(PredictionViewStyle)).Length;
            int value = (int)_singleton._options.PredictionViewStyle;
            var style = (PredictionViewStyle)((value + 1) % count);

            _singleton._options.PredictionViewStyle = style;
            _singleton.SetPredictionView(style);
            _singleton.Render();
        }
    }
}
