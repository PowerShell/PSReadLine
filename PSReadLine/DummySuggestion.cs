using System;
using System.Collections.Generic;

namespace Pseudo
{
    internal class DummySuggestion
    {
        private static readonly List<string> s_suggestions;

        static DummySuggestion()
        {
            s_suggestions = new List<string>()
            {
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
}
