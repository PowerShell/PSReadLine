using System;
using System.Collections.Generic;

namespace Pseudo
{
    internal class DummySuggestion
    {
        private static readonly Dictionary<string, string> s_suggestions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal static string GetCommandLineSuggestion(string text)
        {
            if (s_suggestions.TryGetValue(text, out string suggestion))
            {
                return suggestion;
            }

            return null;
        }
    }
}
