/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private readonly PSConsoleReadLineOptions _options;
        private PSConsoleReadLineOptions Options => _options;

        private void SetOptionsInternal(SetPSReadLineOption options)
        {
            if (options.ContinuationPrompt != null)
            {
                Options.ContinuationPrompt = options.ContinuationPrompt;
            }
            if (options._historyNoDuplicates.HasValue)
            {
                Options.HistoryNoDuplicates = options.HistoryNoDuplicates;
            }
            if (options._historySearchCursorMovesToEnd.HasValue)
            {
                Options.HistorySearchCursorMovesToEnd = options.HistorySearchCursorMovesToEnd;
            }
            if (options._addToHistoryHandlerSpecified)
            {
                Options.AddToHistoryHandler = options.AddToHistoryHandler;
            }
            if (options._commandValidationHandlerSpecified)
            {
                Options.CommandValidationHandler = options.CommandValidationHandler;
            }
            if (options._maximumHistoryCount.HasValue)
            {
                Options.MaximumHistoryCount = options.MaximumHistoryCount;
                if (_history != null)
                {
                    var newHistory = new HistoryQueue<HistoryItem>(Options.MaximumHistoryCount);
                    while (_history.Count > Options.MaximumHistoryCount)
                    {
                        _history.Dequeue();
                    }
                    while (_history.Count > 0)
                    {
                        newHistory.Enqueue(_history.Dequeue());
                    }
                    _history = newHistory;
                    _currentHistoryIndex = _history.Count;
                }
            }
            if (options._maximumKillRingCount.HasValue)
            {
                Options.MaximumKillRingCount = options.MaximumKillRingCount;
                // TODO - make _killRing smaller
            }
            if (options._editMode.HasValue)
            {
                Options.EditMode = options.EditMode;
                SetDefaultBindings(Options.EditMode);
            }
            if (options._showToolTips.HasValue)
            {
                Options.ShowToolTips = options.ShowToolTips;
            }
            if (options._extraPromptLineCount.HasValue)
            {
                Options.ExtraPromptLineCount = options.ExtraPromptLineCount;
            }
            if (options._dingTone.HasValue)
            {
                Options.DingTone = options.DingTone;
            }
            if (options._dingDuration.HasValue)
            {
                Options.DingDuration = options.DingDuration;
            }
            if (options._bellStyle.HasValue)
            {
                Options.BellStyle = options.BellStyle;
            }
            if (options._completionQueryItems.HasValue)
            {
                Options.CompletionQueryItems = options.CompletionQueryItems;
            }
            if (options.WordDelimiters != null)
            {
                Options.WordDelimiters = options.WordDelimiters;
            }
            if (options._historySearchCaseSensitive.HasValue)
            {
                Options.HistorySearchCaseSensitive = options.HistorySearchCaseSensitive;
            }
            if (options._historySaveStyle.HasValue)
            {
                Options.HistorySaveStyle = options.HistorySaveStyle;
            }
            if (options._viModeIndicator.HasValue)
            {
                Options.ViModeIndicator = options.ViModeIndicator;
            }
            if (options.ViModeChangeHandler != null)
            {
                if (Options.ViModeIndicator != ViModeStyle.Script)
                {
                    throw new ParameterBindingException("ViModeChangeHandler option requires ViModeStyle.Script");
                }
                Options.ViModeChangeHandler = options.ViModeChangeHandler;
            }
            if (options.HistorySavePath != null)
            {
                Options.HistorySavePath = options.HistorySavePath;
                _historyFileMutex?.Dispose();
                _historyFileMutex = new Mutex(false, GetHistorySaveFileMutexName());
                _historyFileLastSavedSize = 0;
            }
            if (options._ansiEscapeTimeout.HasValue)
            {
                Options.AnsiEscapeTimeout = options.AnsiEscapeTimeout;
            }
            if (options.PromptText != null)
            {
                Options.PromptText = options.PromptText;
            }
            if (options._predictionSource.HasValue)
            {
                if (_console is PlatformWindows.LegacyWin32Console && options.PredictionSource != PredictionSource.None)
                {
                    throw new ArgumentException(PSReadLineResources.PredictiveSuggestionNotSupported);
                }

                bool notTest = ReferenceEquals(_mockableMethods, this);
                if ((options.PredictionSource & PredictionSource.Plugin) != 0 && Environment.Version.Major < 6 && notTest)
                {
                    throw new ArgumentException(PSReadLineResources.PredictionPluginNotSupported);
                }

                Options.PredictionSource = options.PredictionSource;
            }
            if (options._predictionViewStyle.HasValue)
            {
                WarnWhenWindowSizeTooSmallForView(options.PredictionViewStyle, options);
                Options.PredictionViewStyle = options.PredictionViewStyle;
                _prediction.SetViewStyle(options.PredictionViewStyle);
            }
            if (options.Colors != null)
            {
                IDictionaryEnumerator e = options.Colors.GetEnumerator();
                while (e.MoveNext())
                {
                    if (e.Key is string property)
                    {
                        Options.SetColor(property, e.Value);
                    }
                }
            }
            if (options._terminateOrphanedConsoleApps.HasValue)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Options.TerminateOrphanedConsoleApps = options.TerminateOrphanedConsoleApps;
                    PlatformWindows.SetTerminateOrphanedConsoleApps(Options.TerminateOrphanedConsoleApps);
                }
                else
                {
                    throw new PlatformNotSupportedException(
                        string.Format(
                            CultureInfo.CurrentUICulture,
                            PSReadLineResources.OptionNotSupportedOnNonWindows,
                            nameof(Options.TerminateOrphanedConsoleApps)));
                }
            }
        }

        private void SetKeyHandlerInternal(string[] keys, Action<ConsoleKeyInfo?, object> handler, string briefDescription, string longDescription, ScriptBlock scriptBlock)
        {
            foreach (var key in keys)
            {
                var chord = ConsoleKeyChordConverter.Convert(key);
                var chordHandler = MakeKeyHandler(handler, briefDescription, longDescription, scriptBlock);

                var chordDispatchTable = _dispatchTable;

                for (var index = 0; index < chord.Length; index++)
                {
                    var anchorKey = PSKeyInfo.FromConsoleKeyInfo(chord[index]);

                    var createDispatchTable = chord.Length - index > 1;
                    if (createDispatchTable)
                    {
                        var dispatchTableExists =
                            chordDispatchTable.ContainsKey(anchorKey) &&
                            !chordDispatchTable[anchorKey].TryGetKeyHandler(out var _)
                            ;

                        if (!dispatchTableExists)
                        {
                            chordDispatchTable[anchorKey] = new ChordDispatchTable(
                                Enumerable.Empty<KeyValuePair<PSKeyInfo, KeyHandlerOrChordDispatchTable>>());
                        }

                        chordDispatchTable = (ChordDispatchTable)chordDispatchTable[anchorKey];
                    }
                    else
                    {
                        chordDispatchTable[anchorKey] = chordHandler;
                    }
                }
            }
        }

        private void RemoveKeyHandlerInternal(string[] keys)
        {
            foreach (var key in keys)
            {
                var consoleKeyChord = ConsoleKeyChordConverter.Convert(key);
                var dispatchTable = _singleton._dispatchTable;

                for (var index = 0; index < consoleKeyChord.Length; index++)
                {
                    var chordKey = PSKeyInfo.FromConsoleKeyInfo(consoleKeyChord[index]);
                    if (!dispatchTable.TryGetValue(chordKey, out var handlerOrChordDispatchTable))
                        continue;

                    if (handlerOrChordDispatchTable.TryGetKeyHandler(out var keyHandler))
                    {
                        dispatchTable.Remove(chordKey);
                    }

                    else
                    {
                        dispatchTable = (ChordDispatchTable)handlerOrChordDispatchTable;
                    }
                }
            }
        }

        /// <summary>
        /// Helper function for the Set-PSReadLineOption cmdlet.
        /// </summary>
        public static void SetOptions(SetPSReadLineOption options)
        {
            _singleton.SetOptionsInternal(options);
        }

        /// <summary>
        /// Helper function for the Get-PSReadLineOption cmdlet.
        /// </summary>
        public static PSConsoleReadLineOptions GetOptions()
        {
            // Should we copy?  It doesn't matter much, everything can be tweaked from
            // the cmdlet anyway.
            return _singleton._options;
        }

        class CustomHandlerException : Exception
        {
            internal CustomHandlerException(Exception innerException)
                : base("", innerException)
            {
            }
        }

        /// <summary>
        /// Helper function for the Set-PSReadLineKeyHandler cmdlet.
        /// </summary>
        public static void SetKeyHandler(string[] key, ScriptBlock scriptBlock, string briefDescription, string longDescription)
        {
            void HandlerWrapper(ConsoleKeyInfo? k, object arg)
            {
                try
                {
                    scriptBlock.Invoke(k, arg);
                }
                catch (Exception e)
                {
                    throw new CustomHandlerException(e);
                }
            }

            if (string.IsNullOrWhiteSpace(briefDescription))
            {
                briefDescription = "CustomAction";
            }
            if (string.IsNullOrWhiteSpace(longDescription))
            {
                longDescription = PSReadLineResources.CustomActionDescription;
            }
            _singleton.SetKeyHandlerInternal(key, HandlerWrapper, briefDescription, longDescription, scriptBlock);
        }

        /// <summary>
        /// Helper function for the Set-PSReadLineKeyHandler cmdlet.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public static void SetKeyHandler(string[] key, Action<ConsoleKeyInfo?, object> handler, string briefDescription, string longDescription)
        {
            _singleton.SetKeyHandlerInternal(key, handler, briefDescription, longDescription, null);
        }

        /// <summary>
        /// Helper function for the Remove-PSReadLineKeyHandler cmdlet.
        /// </summary>
        public static void RemoveKeyHandler(string[] key)
        {
            _singleton.RemoveKeyHandlerInternal(key);
        }

        /// <summary>
        /// Return all bound key handlers.
        /// </summary>
        public static IEnumerable<PowerShell.KeyHandler> GetKeyHandlers()
        {
            return GetKeyHandlers(includeBound: true, includeUnbound: false);
        }

        /// <summary>
        /// Helper function for the Get-PSReadLineKeyHandler cmdlet.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<PowerShell.KeyHandler> GetKeyHandlers(bool includeBound, bool includeUnbound)
        {
            var boundFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var templates = new List<(ChordDispatchTable DispatchTable, string[] Surrounds)> {
                (_singleton._dispatchTable, new[]{ "", "", }),
            };

            if (_singleton._options.EditMode == EditMode.Vi)
            {
                templates.Add((_viCmdKeyMap, new[] { "<", ">" }));
            }

            foreach (var template in templates)
            {
                var handlers = GetKeyHandlers("", template.Surrounds, template.DispatchTable, includeBound, ref boundFunctions);
                foreach (var handler in handlers)
                    yield return handler;
            }

            if (includeUnbound)
            {
                // SelfInsert isn't really unbound, but we don't want UI to show it that way
                boundFunctions.Add("SelfInsert");

                var methods = typeof(PSConsoleReadLine).GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length != 2 ||
                        parameters[0].ParameterType != typeof(ConsoleKeyInfo?) ||
                        parameters[1].ParameterType != typeof(object))
                    {
                        continue;
                    }

                    if (!boundFunctions.Contains(method.Name))
                    {
                        yield return new PowerShell.KeyHandler
                        {
                            Key = "Unbound",
                            Function = method.Name,
                            Description = null,
                            Group = GetDisplayGrouping(method.Name),
                        };
                    }
                }
            }
        }

        private static PowerShell.KeyHandler[] GetKeyHandlers(
            string chordPrefix,
            string[] surrounds,
            ChordDispatchTable dispatchTable,
            bool includeBound,
            ref HashSet<string> boundFunctions
        )
        {
            var keyHandlers = new List<PowerShell.KeyHandler>();

            foreach (var entry in dispatchTable)
            {
                var handlerOrChordDispatchTable = entry.Value;

                if (handlerOrChordDispatchTable.TryGetKeyHandler(out var keyHandler))
                {
                    boundFunctions.Add(keyHandler.BriefDescription);
                    if (includeBound)
                    {
                        var handlerKey = chordPrefix.Length == 0
                            ? entry.Key.KeyStr
                            : chordPrefix.Substring(1) + "," + entry.Key.KeyStr
                            ;

                        keyHandlers.Add(new PowerShell.KeyHandler
                        {
                            Key = surrounds[0] + handlerKey + surrounds[1],
                            Function = keyHandler.BriefDescription,
                            Description = keyHandler.LongDescription,
                            Group = GetDisplayGrouping(keyHandler.BriefDescription),
                        });
                    }
                }

                else
                {
                    keyHandlers.AddRange(
                        GetKeyHandlers(
                            chordPrefix + "," + entry.Key.KeyStr,
                            surrounds,
                            (ChordDispatchTable)handlerOrChordDispatchTable,
                            includeBound,
                            ref boundFunctions
                        ));
                }
            }

            return keyHandlers.ToArray();
        }

        /// <summary>
        /// Return key handlers bound to specified chords.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<PowerShell.KeyHandler> GetKeyHandlers(string[] Chord)
        {
            if (Chord == null || Chord.Length == 0)
            {
                yield break;
            }

            foreach (string key in Chord)
            {
                ConsoleKeyInfo[] consoleKeyChord = ConsoleKeyChordConverter.Convert(key);

                foreach (var handler in GetKeyHandlers(_singleton._dispatchTable, consoleKeyChord))
                    yield return handler;

                // If in Vi mode, also check Vi's command mode list.
                if (_singleton._options.EditMode == EditMode.Vi)
                {
                    foreach (var handler in GetKeyHandlers(_viCmdKeyMap, consoleKeyChord, new[] { "<", ">" }))
                        yield return handler;
                }
            }
        }

        private static IEnumerable<PowerShell.KeyHandler> GetKeyHandlers(
            ChordDispatchTable dispatchTable,
            ConsoleKeyInfo[] consoleKeyChord,
            string[] surrounds = null
        )
        {
            surrounds ??= new[] { "", "" };
            var handlerKey = "";

            for (var index = 0; index < consoleKeyChord.Length; index++)
            {
                var chordKey = PSKeyInfo.FromConsoleKeyInfo(consoleKeyChord[index]);
                if (!dispatchTable.TryGetValue(chordKey, out var handlerOrChordDispatchTable))
                    continue;

                handlerKey += "," + chordKey.KeyStr;

                if (handlerOrChordDispatchTable.TryGetKeyHandler(out var keyHandler))
                {
                    yield return new PowerShell.KeyHandler
                    {
                        Key = surrounds[0] + handlerKey.Substring(1) + surrounds[1],
                        Function = keyHandler.BriefDescription,
                        Description = keyHandler.LongDescription,
                        Group = GetDisplayGrouping(keyHandler.BriefDescription),
                    };
                }

                else
                {
                    dispatchTable = (ChordDispatchTable)handlerOrChordDispatchTable;
                }
            }
        }
    }
}
