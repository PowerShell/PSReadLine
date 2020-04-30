using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private class SuggestionModeRestore : IDisposable
        {
            private bool oldSuggestionMode;

            internal SuggestionModeRestore(bool showSuggestion)
            {
                oldSuggestionMode = _singleton._showSuggestion;
                _singleton._showSuggestion = showSuggestion;
            }

            public void Dispose()
            {
                _singleton._showSuggestion = oldSuggestionMode;
            }
        }

        private string _suggestionText;
        private uint _lastRenderedTextHash;
        private bool _showSuggestion = true;

        /// <summary>
        /// Turn off the prediction feature for the scope of the calling method.
        /// Note: the calling method need to use this method with the 'using' statement/variable,
        /// so that the suggestion feature can be properly restored.
        /// </summary>
        private SuggestionModeRestore PredictionOff() => new SuggestionModeRestore(false);

        private string GetSuggestion(string text)
        {
            if (_options.PredictionStyle == PredictionStyle.None || !_showSuggestion ||
                LineIsMultiLine() || string.IsNullOrWhiteSpace(text))
            {
                _suggestionText = null;
                _lastRenderedTextHash = 0;
                return null;
            }

            try
            {
                uint textHash = _lastRenderedTextHash;
                _lastRenderedTextHash = FNV1a32Hash.ComputeHash(text);
                if (textHash != _lastRenderedTextHash)
                {
                    _suggestionText = GetHistorySuggestion(text);
                }
            }
            catch
            {
                _suggestionText = null;
            }

            return _suggestionText?.Substring(text.Length);
        }
    }
}
