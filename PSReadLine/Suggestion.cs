using System;
using System.Collections.Generic;

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

        private SuggestionModeRestore ChangeSuggestionMode(bool showSuggestion)
        {
            return new SuggestionModeRestore(showSuggestion);
        }
    }
}
