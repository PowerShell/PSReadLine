/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Reflection;
using System.Threading;
using Microsoft.PowerShell.Internal;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        public PSConsoleReadLineOptions Options { get; }

        private void SetOptionsInternal(SetPSReadLineOption options)
        {
            if (options.ContinuationPrompt != null) Options.ContinuationPrompt = options.ContinuationPrompt;
            if (options._historyNoDuplicates.HasValue) Options.HistoryNoDuplicates = options.HistoryNoDuplicates;
            if (options._historySearchCursorMovesToEnd.HasValue)
                Options.HistorySearchCursorMovesToEnd = options.HistorySearchCursorMovesToEnd;
            if (options._addToHistoryHandlerSpecified) Options.AddToHistoryHandler = options.AddToHistoryHandler;
            if (options._commandValidationHandlerSpecified)
                Options.CommandValidationHandler = options.CommandValidationHandler;
            if (options._maximumHistoryCount.HasValue)
            {
                Options.MaximumHistoryCount = options.MaximumHistoryCount;
                if (_hs.Historys != null)
                {
                    var newHistory = new HistoryQueue<HistoryItem>(Options.MaximumHistoryCount);
                    while (_hs.Historys.Count > Options.MaximumHistoryCount) _hs.Historys.Dequeue();
                    while (_hs.Historys.Count > 0) newHistory.Enqueue(_hs.Historys.Dequeue());
                    _hs.Historys = newHistory;
                    SearcherReadLine.ResetCurrentHistoryIndex(false);
                }
            }

            if (options._maximumKillRingCount.HasValue)
                Options.MaximumKillRingCount = options.MaximumKillRingCount;
            // TODO - make _killRing smaller
            if (options._editMode.HasValue)
            {
                Options.EditMode = options.EditMode;

                // Switching/resetting modes - clear out chord dispatch table
                _chordDispatchTable.Clear();

                SetDefaultBindings(Options.EditMode);
            }

            if (options._showToolTips.HasValue) Options.ShowToolTips = options.ShowToolTips;
            if (options._extraPromptLineCount.HasValue) Options.ExtraPromptLineCount = options.ExtraPromptLineCount;
            if (options._dingTone.HasValue) Options.DingTone = options.DingTone;
            if (options._dingDuration.HasValue) Options.DingDuration = options.DingDuration;
            if (options._bellStyle.HasValue) Options.BellStyle = options.BellStyle;
            if (options._completionQueryItems.HasValue) Options.CompletionQueryItems = options.CompletionQueryItems;
            if (options.WordDelimiters != null) Options.WordDelimiters = options.WordDelimiters;
            if (options._historySearchCaseSensitive.HasValue)
                Options.HistorySearchCaseSensitive = options.HistorySearchCaseSensitive;
            if (options._historySaveStyle.HasValue) Options.HistorySaveStyle = options.HistorySaveStyle;
            if (options._viModeIndicator.HasValue) Options.ViModeIndicator = options.ViModeIndicator;
            if (options.ViModeChangeHandler != null)
            {
                if (Options.ViModeIndicator != ViModeStyle.Script)
                    throw new ParameterBindingException("ViModeChangeHandler option requires ViModeStyle.Script");
                Options.ViModeChangeHandler = options.ViModeChangeHandler;
            }

            if (options.HistorySavePath != null)
            {
                Options.HistorySavePath = options.HistorySavePath;
                _hs.HistoryFileMutex?.Dispose();
                _hs.HistoryFileMutex = new Mutex(false, _hs.GetHistorySaveFileMutexName());
                _hs.HistoryFileLastSavedSize = 0;
            }

            if (options._ansiEscapeTimeout.HasValue) Options.AnsiEscapeTimeout = options.AnsiEscapeTimeout;
            if (options.PromptText != null) Options.PromptText = options.PromptText;
            if (options._predictionSource.HasValue)
            {
                if (Renderer.Console is PlatformWindows.LegacyWin32Console &&
                    options.PredictionSource != PredictionSource.None)
                    throw new ArgumentException(PSReadLineResources.PredictiveSuggestionNotSupported);

                var notTest = ReferenceEquals(_mockableMethods, this);
                if ((options.PredictionSource & PredictionSource.Plugin) != 0 && Environment.Version.Major < 6 &&
                    notTest) throw new ArgumentException(PSReadLineResources.PredictionPluginNotSupported);

                Options.PredictionSource = options.PredictionSource;
            }

            if (options._predictionViewStyle.HasValue)
            {
                WarnWhenWindowSizeTooSmallForView(options.PredictionViewStyle, options);
                Options.PredictionViewStyle = options.PredictionViewStyle;
                _Prediction.SetViewStyle(options.PredictionViewStyle);
            }

            if (options.Colors != null)
            {
                var e = options.Colors.GetEnumerator();
                while (e.MoveNext())
                    if (e.Key is string property)
                        Options.SetColor(property, e.Value);
            }
        }

        private void SetKeyHandlerInternal(string[] keys, Action<ConsoleKeyInfo?, object> handler,
            string briefDescription, string longDescription, ScriptBlock scriptBlock)
        {
            foreach (var key in keys)
            {
                var chord = ConsoleKeyChordConverter.Convert(key);
                var firstKey = PSKeyInfo.FromConsoleKeyInfo(chord[0]);
                if (chord.Length == 1)
                {
                    _dispatchTable[firstKey] = MakeKeyHandler(handler, briefDescription, longDescription, scriptBlock);
                }
                else
                {
                    _dispatchTable[firstKey] = MakeKeyHandler(Chord, "ChordFirstKey");
                    if (!_chordDispatchTable.TryGetValue(firstKey, out var secondDispatchTable))
                    {
                        secondDispatchTable = new Dictionary<PSKeyInfo, KeyHandler>();
                        _chordDispatchTable[firstKey] = secondDispatchTable;
                    }

                    secondDispatchTable[PSKeyInfo.FromConsoleKeyInfo(chord[1])] =
                        MakeKeyHandler(handler, briefDescription, longDescription, scriptBlock);
                }
            }
        }

        private void RemoveKeyHandlerInternal(string[] keys)
        {
            foreach (var key in keys)
            {
                var chord = ConsoleKeyChordConverter.Convert(key);
                var firstKey = PSKeyInfo.FromConsoleKeyInfo(chord[0]);
                if (chord.Length == 1)
                {
                    _dispatchTable.Remove(firstKey);
                }
                else
                {
                    if (_chordDispatchTable.TryGetValue(firstKey, out var secondDispatchTable))
                    {
                        secondDispatchTable.Remove(PSKeyInfo.FromConsoleKeyInfo(chord[1]));
                        if (secondDispatchTable.Count == 0) _dispatchTable.Remove(firstKey);
                    }
                }
            }
        }

        /// <summary>
        ///     Helper function for the Set-PSReadLineOption cmdlet.
        /// </summary>
        public static void SetOptions(SetPSReadLineOption options)
        {
            Singleton.SetOptionsInternal(options);
        }

        /// <summary>
        ///     Helper function for the Get-PSReadLineOption cmdlet.
        /// </summary>
        public static PSConsoleReadLineOptions GetOptions()
        {
            // Should we copy?  It doesn't matter much, everything can be tweaked from
            // the cmdlet anyway.
            return Singleton.Options;
        }

        /// <summary>
        ///     Helper function for the Set-PSReadLineKeyHandler cmdlet.
        /// </summary>
        public static void SetKeyHandler(string[] key, ScriptBlock scriptBlock, string briefDescription,
            string longDescription)
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

            if (string.IsNullOrWhiteSpace(briefDescription)) briefDescription = "CustomAction";
            if (string.IsNullOrWhiteSpace(longDescription))
                longDescription = PSReadLineResources.CustomActionDescription;
            Singleton.SetKeyHandlerInternal(key, HandlerWrapper, briefDescription, longDescription, scriptBlock);
        }

        /// <summary>
        ///     Helper function for the Set-PSReadLineKeyHandler cmdlet.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public static void SetKeyHandler(string[] key, Action<ConsoleKeyInfo?, object> handler, string briefDescription,
            string longDescription)
        {
            Singleton.SetKeyHandlerInternal(key, handler, briefDescription, longDescription, null);
        }

        /// <summary>
        ///     Helper function for the Remove-PSReadLineKeyHandler cmdlet.
        /// </summary>
        public static void RemoveKeyHandler(string[] key)
        {
            Singleton.RemoveKeyHandlerInternal(key);
        }

        /// <summary>
        ///     Return all bound key handlers.
        /// </summary>
        public static IEnumerable<PowerShell.KeyHandler> GetKeyHandlers()
        {
            return GetKeyHandlers(true, false);
        }

        /// <summary>
        ///     Helper function for the Get-PSReadLineKeyHandler cmdlet.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<PowerShell.KeyHandler> GetKeyHandlers(bool includeBound, bool includeUnbound)
        {
            var boundFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in Singleton._dispatchTable)
            {
                if (entry.Value.BriefDescription == "Ignore"
                    || entry.Value.BriefDescription == "ChordFirstKey")
                    continue;
                boundFunctions.Add(entry.Value.BriefDescription);
                if (includeBound)
                    yield return new PowerShell.KeyHandler
                    {
                        Key = entry.Key.KeyStr,
                        Function = entry.Value.BriefDescription,
                        Description = entry.Value.LongDescription,
                        Group = GetDisplayGrouping(entry.Value.BriefDescription)
                    };
            }

            // Added to support vi command mode mappings
            if (Singleton.Options.EditMode == EditMode.Vi)
                foreach (var entry in _viCmdKeyMap)
                {
                    if (entry.Value.BriefDescription == "Ignore"
                        || entry.Value.BriefDescription == "ChordFirstKey")
                        continue;
                    boundFunctions.Add(entry.Value.BriefDescription);
                    if (includeBound)
                        yield return new PowerShell.KeyHandler
                        {
                            Key = "<" + entry.Key.KeyStr + ">",
                            Function = entry.Value.BriefDescription,
                            Description = entry.Value.LongDescription,
                            Group = GetDisplayGrouping(entry.Value.BriefDescription)
                        };
                }

            foreach (var entry in Singleton._chordDispatchTable)
                foreach (var secondEntry in entry.Value)
                {
                    boundFunctions.Add(secondEntry.Value.BriefDescription);
                    if (includeBound)
                        yield return new PowerShell.KeyHandler
                        {
                            Key = entry.Key.KeyStr + "," + secondEntry.Key.KeyStr,
                            Function = secondEntry.Value.BriefDescription,
                            Description = secondEntry.Value.LongDescription,
                            Group = GetDisplayGrouping(secondEntry.Value.BriefDescription)
                        };
                }

            // Added to support vi command mode chorded mappings
            if (Singleton.Options.EditMode == EditMode.Vi)
                foreach (var entry in _viCmdChordTable)
                    foreach (var secondEntry in entry.Value)
                    {
                        if (secondEntry.Value.BriefDescription == "Ignore") continue;
                        boundFunctions.Add(secondEntry.Value.BriefDescription);
                        if (includeBound)
                            yield return new PowerShell.KeyHandler
                            {
                                Key = "<" + entry.Key.KeyStr + "," + secondEntry.Key.KeyStr + ">",
                                Function = secondEntry.Value.BriefDescription,
                                Description = secondEntry.Value.LongDescription,
                                Group = GetDisplayGrouping(secondEntry.Value.BriefDescription)
                            };
                    }

            if (includeUnbound)
            {
                // SelfInsert isn't really unbound, but we don't want UI to show it that way
                boundFunctions.Add("SelfInsert");

                foreach (var method in BindableFunctions)
                {
                    if (!boundFunctions.Contains(method.Name))
                        yield return new PowerShell.KeyHandler
                        {
                            Key = "Unbound",
                            Function = method.Name,
                            Description = null,
                            Group = GetDisplayGrouping(method.Name)
                        };
                }
            }
        }

        /// <summary>
        ///     Return key handlers bound to specified chords.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<PowerShell.KeyHandler> GetKeyHandlers(string[] Chord)
        {
            var boundFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (Chord == null || Chord.Length == 0) yield break;

            foreach (var Key in Chord)
            {
                var consoleKeyChord = ConsoleKeyChordConverter.Convert(Key);
                var firstKey = PSKeyInfo.FromConsoleKeyInfo(consoleKeyChord[0]);

                if (Singleton._dispatchTable.TryGetValue(firstKey, out var entry))
                {
                    if (consoleKeyChord.Length == 1)
                    {
                        yield return new PowerShell.KeyHandler
                        {
                            Key = firstKey.KeyStr,
                            Function = entry.BriefDescription,
                            Description = entry.LongDescription,
                            Group = GetDisplayGrouping(entry.BriefDescription)
                        };
                    }
                    else
                    {
                        var secondKey = PSKeyInfo.FromConsoleKeyInfo(consoleKeyChord[1]);
                        if (Singleton._chordDispatchTable.TryGetValue(firstKey, out var secondDispatchTable) &&
                            secondDispatchTable.TryGetValue(secondKey, out entry))
                            yield return new PowerShell.KeyHandler
                            {
                                Key = firstKey.KeyStr + "," + secondKey.KeyStr,
                                Function = entry.BriefDescription,
                                Description = entry.LongDescription,
                                Group = GetDisplayGrouping(entry.BriefDescription)
                            };
                    }
                }

                // If in Vi mode, also check Vi's command mode list.
                if (Singleton.Options.EditMode == EditMode.Vi)
                    if (_viCmdKeyMap.TryGetValue(firstKey, out entry))
                    {
                        if (consoleKeyChord.Length == 1)
                        {
                            if (entry.BriefDescription == "Ignore") continue;

                            yield return new PowerShell.KeyHandler
                            {
                                Key = "<" + firstKey.KeyStr + ">",
                                Function = entry.BriefDescription,
                                Description = entry.LongDescription,
                                Group = GetDisplayGrouping(entry.BriefDescription)
                            };
                        }
                        else
                        {
                            var secondKey = PSKeyInfo.FromConsoleKeyInfo(consoleKeyChord[1]);
                            if (_viCmdChordTable.TryGetValue(firstKey, out var secondDispatchTable) &&
                                secondDispatchTable.TryGetValue(secondKey, out entry))
                            {
                                if (entry.BriefDescription == "Ignore") continue;

                                yield return new PowerShell.KeyHandler
                                {
                                    Key = "<" + firstKey.KeyStr + "," + secondKey.KeyStr + ">",
                                    Function = entry.BriefDescription,
                                    Description = entry.LongDescription,
                                    Group = GetDisplayGrouping(entry.BriefDescription)
                                };
                            }
                        }
                    }
            }
        }

        private class CustomHandlerException : Exception
        {
            internal CustomHandlerException(Exception innerException)
                : base("", innerException)
            {
            }
        }
    }
}