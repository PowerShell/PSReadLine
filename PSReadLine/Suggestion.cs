using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell
{
    internal class DummySuggestion
    {
        private static readonly List<string> s_suggestions;

        static DummySuggestion()
        {
            s_suggestions = new List<string>()
            {
                "ildasm",
                "git",
                "git branch -r",
                "git checkout -b",
                "git checkout master",
                "git fetch --all -p",
                "git status",
                "git diff",
                "git diff --cached",
                "git add -u",
                "git add -A"
            };
        }

        internal static string GetCommandLineSuggestion(string text)
        {
            foreach (string candidate in s_suggestions)
            {
                if (candidate.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }
    }

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
