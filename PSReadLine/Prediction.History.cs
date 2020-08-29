/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private HashSet<string> _cacheSet;
        private List<SuggestionEntry> _cacheList;

        /// <summary>
        /// Currently we only select single-line history that is prefixed with the user input,
        /// but it can be improved to not strictly use the user input as a prefix, but a hint
        /// to extract a partial pipeline or statement from a single-line or multiple-line
        /// history entry.
        /// </summary>
        private string GetHistorySuggestion(string text)
        {
            for (int index = _history.Count - 1; index >= 0; index --)
            {
                var line = _history[index].CommandLine.TrimEnd();
                if (line.Length > text.Length)
                {
                    bool isMultiLine = line.Contains('\n');
                    if (!isMultiLine && line.StartsWith(text, Options.HistoryStringComparison))
                    {
                        return line;
                    }
                }
            }

            return null;
        }

        private List<SuggestionEntry> GetHistorySuggestions(string input, int count)
        {
            List<SuggestionEntry> results = null;
            int remainingCount = count;
            const string source = "History";

            _cacheSet ??= new HashSet<string>(Options.HistoryStringComparer);
            _cacheList ??= new List<SuggestionEntry>();

            for (int historyIndex = _history.Count - 1; historyIndex >= 0; historyIndex --)
            {
                var line = _history[historyIndex].CommandLine.TrimEnd();

                // Skip the history command lines that are smaller in length than the user input,
                // or contain multiple logical lines.
                if (line.Length <= input.Length || _cacheSet.Contains(line) || line.Contains('\n'))
                {
                    continue;
                }

                int matchIndex = line.IndexOf(input, Options.HistoryStringComparison);
                if (matchIndex == -1)
                {
                    continue;
                }

                if (results == null)
                {
                    results = new List<SuggestionEntry>(capacity: count);
                }

                _cacheSet.Add(line);
                if (matchIndex == 0)
                {
                    results.Add(new SuggestionEntry(source, line, matchIndex));
                    if (--remainingCount == 0)
                    {
                        break;
                    }
                }
                else if (_cacheList.Count < remainingCount)
                {
                    _cacheList.Add(new SuggestionEntry(source, line, matchIndex));
                }
            }

            if (remainingCount > 0 && _cacheList.Count > 0)
            {
                for (int i = 0; i < remainingCount && i < _cacheList.Count; i++)
                {
                    results.Add(_cacheList[i]);
                }
            }

            _cacheSet.Clear();
            _cacheList.Clear();
            return results;
        }
    }
}
