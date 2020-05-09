using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private string _suggestionText;
        private string _lastUserInput;
        private bool _showSuggestion = true;

        private bool IsPredictionOn => _options.PredictionSource != PredictionSource.None && _showSuggestion;
        private bool IsPredictionOff => _options.PredictionSource == PredictionSource.None || !_showSuggestion;

        /// <summary>
        /// Turn off the prediction feature for the scope of the calling method.
        /// Note: the calling method need to use this method with the 'using' statement/variable,
        /// so that the suggestion feature can be properly restored.
        /// </summary>
        private IDisposable PredictionOff()
        {
            var oldSuggestionMode = _singleton._showSuggestion;
            _singleton._showSuggestion = false;

            return new Disposable(() => _singleton._showSuggestion = oldSuggestionMode);
        }

        private void ResetSuggestion()
        {
            _suggestionText = null;
            _lastUserInput = null;
        }

        private string GetSuggestion(string text)
        {
            if (IsPredictionOff || LineIsMultiLine() || string.IsNullOrWhiteSpace(text))
            {
                ResetSuggestion();
                return null;
            }

            try
            {
                if (_suggestionText == null || _suggestionText.Length <= text.Length ||
                    text.Length < _lastUserInput.Length || !_suggestionText.StartsWith(text, _options.HistoryStringComparison))
                {
                    _lastUserInput = text;
                    _suggestionText = GetHistorySuggestion(text);
                }
            }
            catch
            {
                _suggestionText = null;
            }

            return _suggestionText?.Substring(text.Length);
        }

        /// <summary>
        /// Accept the suggestion text if there is one.
        /// </summary>
        public static void AcceptSuggestion(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._suggestionText != null)
            {
                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                using var _ = _singleton.PredictionOff();
                Replace(0, _singleton._buffer.Length, _singleton._suggestionText);
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
            if (_singleton._suggestionText != null)
            {
                // Ignore the visual selection.
                _singleton._visualSelectionCommandCount = 0;

                int index = _singleton._buffer.Length;
                while (numericArg-- > 0 && index < _singleton._suggestionText.Length)
                {
                    index = _singleton.FindForwardSuggestionWordPoint(index, _singleton.Options.WordDelimiters);
                }

                Replace(0, _singleton._buffer.Length, _singleton._suggestionText.Substring(0, index));
            }
        }
    }
}
