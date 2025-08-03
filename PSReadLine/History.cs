/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Management.Automation.Language;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{
    /// <summary>
    /// FNV-1a hashing algorithm: http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a
    /// </summary>
    internal class FNV1a32Hash
    {
        // FNV-1a algorithm parameters: http://www.isthe.com/chongo/tech/comp/fnv/#FNV-param
        private const uint FNV32_PRIME = 16777619;
        private const uint FNV32_OFFSETBASIS = 2166136261;

        internal static uint ComputeHash(string input)
        {
            char ch;
            uint hash = FNV32_OFFSETBASIS, lowByte, highByte;

            for (int i = 0; i < input.Length; i++)
            {
                ch = input[i];
                lowByte = (uint)(ch & 0x00FF);
                hash = unchecked((hash ^ lowByte) * FNV32_PRIME);

                highByte = (uint)(ch >> 8);
                hash = unchecked((hash ^ highByte) * FNV32_PRIME);
            }

            return hash;
        }
    }

    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// History details including the command line, source, and start and approximate execution time.
        /// </summary>
        [DebuggerDisplay("{" + nameof(CommandLine) + "}")]
        public class HistoryItem
        {
            /// <summary>
            /// The command line, or if multiple lines, the lines joined
            /// with a newline.
            /// </summary>
            public string CommandLine { get; internal set; }

            /// <summary>
            /// The time at which the command was added to history in UTC.
            /// </summary>
            public DateTime StartTime { get; internal set; }

            /// <summary>
            /// The approximate elapsed time (includes time to invoke Prompt).
            /// The value can be 0 ticks if if accessed before PSReadLine
            /// gets a chance to set it.
            /// </summary>
            public TimeSpan ApproximateElapsedTime { get; internal set; }

            /// <summary>
            /// True if the command was from another running session
            /// (as opposed to read from the history file at startup.)
            /// </summary>
            public bool FromOtherSession { get; internal set; }

            /// <summary>
            /// True if the command was read in from the history file at startup.
            /// </summary>
            public bool FromHistoryFile { get; internal set; }

            internal bool _saved;
            internal bool _sensitive;
            internal List<EditItem> _edits;
            internal int _undoEditIndex;
            internal int _editGroupStart;
        }

        // History state
        private HistoryQueue<HistoryItem> _history;
        private HistoryQueue<string> _recentHistory;
        private HistoryItem _previousHistoryItem;
        private Dictionary<string, int> _hashedHistory;
        private int _currentHistoryIndex;
        private int _getNextHistoryIndex;
        private int _searchHistoryCommandCount;
        private int _recallHistoryCommandCount;
        private int _anyHistoryCommandCount;
        private string _searchHistoryPrefix;
        // When cycling through history, the current line (not yet added to history)
        // is saved here so it can be restored.
        private readonly HistoryItem _savedCurrentLine;

        private Mutex _historyFileMutex;
        private long _historyFileLastSavedSize;

        private const string _forwardISearchPrompt = "fwd-i-search: ";
        private const string _backwardISearchPrompt = "bck-i-search: ";
        private const string _failedForwardISearchPrompt = "failed-fwd-i-search: ";
        private const string _failedBackwardISearchPrompt = "failed-bck-i-search: ";

        // Pattern used to check for sensitive inputs.
        private static readonly Regex s_sensitivePattern = new Regex(
            "password|asplaintext|token|apikey|secret",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> s_SecretMgmtCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "Get-Secret",
            "Get-SecretInfo",
            "Get-SecretVault",
            "Register-SecretVault",
            "Remove-Secret",
            "Set-SecretInfo",
            "Set-SecretVaultDefault",
            "Test-SecretVault",
            "Unlock-SecretVault",
            "Unregister-SecretVault",
            "Get-AzAccessToken",
        };

        private void ClearSavedCurrentLine()
        {
            _savedCurrentLine.CommandLine = null;
            _savedCurrentLine._edits = null;
            _savedCurrentLine._undoEditIndex = 0;
            _savedCurrentLine._editGroupStart = -1;
        }

        private AddToHistoryOption GetAddToHistoryOption(string line, bool fromHistoryFile)
        {
            // Whitespace only is useless, never add.
            if (string.IsNullOrWhiteSpace(line))
            {
                return AddToHistoryOption.SkipAdding;
            }

            // Under "no dupes" (which is on by default), immediately drop dupes of the previous line.
            if (Options.HistoryNoDuplicates && _history.Count > 0 &&
                string.Equals(_history[_history.Count - 1].CommandLine, line, StringComparison.Ordinal))
            {
                return AddToHistoryOption.SkipAdding;
            }

            if (!fromHistoryFile && Options.AddToHistoryHandler != null)
            {
                if (Options.AddToHistoryHandler == PSConsoleReadLineOptions.DefaultAddToHistoryHandler)
                {
                    // Avoid boxing if it's the default handler.
                    return GetDefaultAddToHistoryOption(line);
                }

                object value = Options.AddToHistoryHandler(line);
                if (value is PSObject psObj)
                {
                    value = psObj.BaseObject;
                }

                if (value is bool boolValue)
                {
                    return boolValue ? AddToHistoryOption.MemoryAndFile : AddToHistoryOption.SkipAdding;
                }

                if (value is AddToHistoryOption enumValue)
                {
                    return enumValue;
                }

                if (value is string strValue && Enum.TryParse(strValue, out enumValue))
                {
                    return enumValue;
                }

                // 'TryConvertTo' incurs exception handling when the value cannot be converted to the target type.
                // It's expensive, especially when we need to process lots of history items from file during the
                // initialization. So do the conversion as the last resort.
                if (LanguagePrimitives.TryConvertTo(value, out enumValue))
                {
                    return enumValue;
                }
            }

            // Add to both history queue and file by default.
            return AddToHistoryOption.MemoryAndFile;
        }

        private string MaybeAddToHistory(
            string result,
            List<EditItem> edits,
            int undoEditIndex,
            bool fromDifferentSession = false,
            bool fromInitialRead = false)
        {
            bool fromHistoryFile = fromDifferentSession || fromInitialRead;
            var addToHistoryOption = GetAddToHistoryOption(result, fromHistoryFile);

            if (addToHistoryOption != AddToHistoryOption.SkipAdding)
            {
                _previousHistoryItem = new HistoryItem
                {
                    CommandLine = result,
                    _edits = edits,
                    _undoEditIndex = undoEditIndex,
                    _editGroupStart = -1,
                    _saved = fromHistoryFile,
                    FromOtherSession = fromDifferentSession,
                    FromHistoryFile = fromInitialRead,
                };

                if (!fromHistoryFile)
                {
                    // Add to the recent history queue, which is used when querying for prediction.
                    _recentHistory.Enqueue(result);
                    // 'MemoryOnly' indicates sensitive content in the command line
                    _previousHistoryItem._sensitive = addToHistoryOption == AddToHistoryOption.MemoryOnly;
                    _previousHistoryItem.StartTime = DateTime.UtcNow;
                }

                _history.Enqueue(_previousHistoryItem);

                _currentHistoryIndex = _history.Count;

                if (_options.HistorySaveStyle == HistorySaveStyle.SaveIncrementally && !fromHistoryFile)
                {
                    IncrementalHistoryWrite();
                }
            }
            else
            {
                _previousHistoryItem = null;
            }

            // Clear the saved line unless we used AcceptAndGetNext in which
            // case we're really still in middle of history and might want
            // to recall the saved line.
            if (_getNextHistoryIndex == 0)
            {
                ClearSavedCurrentLine();
            }
            return result;
        }

        private string GetHistorySaveFileMutexName()
        {
            // Return a reasonably unique name - it's not too important as there will rarely
            // be any contention.
            uint hashFromPath = FNV1a32Hash.ComputeHash(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? _options.HistorySavePath.ToLower()
                    : _options.HistorySavePath);

            return "PSReadLineHistoryFile_" + hashFromPath.ToString();
        }

        private void IncrementalHistoryWrite()
        {
            var i = _currentHistoryIndex - 1;
            while (i >= 0)
            {
                if (_history[i]._saved)
                {
                    break;
                }
                i -= 1;
            }

            WriteHistoryRange(i + 1, _history.Count - 1, overwritten: false);
        }

        private void SaveHistoryAtExit()
        {
            WriteHistoryRange(0, _history.Count - 1, overwritten: true);
        }

        private int historyErrorReportedCount;
        private void ReportHistoryFileError(Exception e)
        {
            if (historyErrorReportedCount == 2)
                return;

            historyErrorReportedCount += 1;
            Console.Write(_options._errorColor);
            Console.WriteLine(PSReadLineResources.HistoryFileErrorMessage, Options.HistorySavePath, e.Message);
            if (historyErrorReportedCount == 2)
            {
                Console.WriteLine(PSReadLineResources.HistoryFileErrorFinalMessage);
            }
            Console.Write("\x1b0m");
        }

        private bool WithHistoryFileMutexDo(int timeout, Action action)
        {
            int retryCount = 0;
            do
            {
                try
                {
                    if (_historyFileMutex.WaitOne(timeout))
                    {
                        try
                        {
                            action();
                            return true;
                        }
                        catch (UnauthorizedAccessException uae)
                        {
                            ReportHistoryFileError(uae);
                            return false;
                        }
                        catch (IOException ioe)
                        {
                            ReportHistoryFileError(ioe);
                            return false;
                        }
                        finally
                        {
                            _historyFileMutex.ReleaseMutex();
                        }
                    }

                    // Consider it a failure if we timed out on the mutex.
                    return false;
                }
                catch (AbandonedMutexException)
                {
                    retryCount += 1;

                    // We acquired the mutex object that was abandoned by another powershell process.
                    // Now, since we own it, we must release it before retry, otherwise, we will miss
                    // a release and keep holding the mutex, in which case the 'WaitOne' calls from
                    // all other powershell processes will time out.
                    _historyFileMutex.ReleaseMutex();
                }
            } while (retryCount > 0 && retryCount < 3);

            // If we reach here, that means we've done the retries but always got the 'AbandonedMutexException'.
            return false;
        }

        private void WriteHistoryRange(int start, int end, bool overwritten)
        {
            WithHistoryFileMutexDo(100, () =>
            {
                bool retry = true;
                // Get the new content since the last sync.
                List<string> historyLines = overwritten ? null : ReadHistoryFileIncrementally();

                try
                {
                    retry_after_creating_directory:
                    try
                    {
                        using (var file = overwritten ? File.CreateText(Options.HistorySavePath) : File.AppendText(Options.HistorySavePath))
                        {
                            for (var i = start; i <= end; i++)
                            {
                                HistoryItem item = _history[i];
                                item._saved = true;

                                // Actually, skip writing sensitive items to file.
                                if (item._sensitive) { continue; }

                                var line = item.CommandLine.Replace("\n", "`\n");
                                file.WriteLine(line);
                            }
                        }
                        var fileInfo = new FileInfo(Options.HistorySavePath);
                        _historyFileLastSavedSize = fileInfo.Length;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Try making the directory, but just once
                        if (retry)
                        {
                            retry = false;
                            Directory.CreateDirectory(Path.GetDirectoryName(Options.HistorySavePath));
                            goto retry_after_creating_directory;
                        }
                    }
                }
                finally
                {
                    if (historyLines != null)
                    {
                        // Populate new history from other sessions to the history queue after we are done
                        // with writing the specified range to the file.
                        // We do it at this point to make sure the range of history items from 'start' to
                        // 'end' do not get changed before the writing to the file.
                        UpdateHistoryFromFile(historyLines, fromDifferentSession: true, fromInitialRead: false);
                    }
                }
            });
        }

        /// <summary>
        /// Helper method to read the incremental part of the history file.
        /// Note: the call to this method should be guarded by the mutex that protects the history file.
        /// </summary>
        private List<string> ReadHistoryFileIncrementally()
        {
            var fileInfo = new FileInfo(Options.HistorySavePath);
            if (fileInfo.Exists && fileInfo.Length != _historyFileLastSavedSize)
            {
                var historyLines = new List<string>();
                using (var fs = new FileStream(Options.HistorySavePath, FileMode.Open))
                using (var sr = new StreamReader(fs))
                {
                    fs.Seek(_historyFileLastSavedSize, SeekOrigin.Begin);

                    while (!sr.EndOfStream)
                    {
                        historyLines.Add(sr.ReadLine());
                    }
                }

                _historyFileLastSavedSize = fileInfo.Length;
                return historyLines.Count > 0 ? historyLines : null;
            }

            return null;
        }

        private bool MaybeReadHistoryFile()
        {
            if (Options.HistorySaveStyle == HistorySaveStyle.SaveIncrementally)
            {
                return WithHistoryFileMutexDo(1000, () =>
                {
                    List<string> historyLines = ReadHistoryFileIncrementally();
                    if (historyLines != null)
                    {
                        UpdateHistoryFromFile(historyLines, fromDifferentSession: true, fromInitialRead: false);
                    }
                });
            }

            // true means no errors, not that we actually read the file
            return true;
        }

        private void ReadHistoryFile()
        {
            if (File.Exists(Options.HistorySavePath))
            {
                WithHistoryFileMutexDo(1000, () =>
                {
                    var historyLines = ReadHistoryLinesImpl(Options.HistorySavePath, Options.MaximumHistoryCount);
                    UpdateHistoryFromFile(historyLines, fromDifferentSession: false, fromInitialRead: true);
                    var fileInfo = new FileInfo(Options.HistorySavePath);
                    _historyFileLastSavedSize = fileInfo.Length;
                });
            }

            static IEnumerable<string> ReadHistoryLinesImpl(string path, int historyCount)
            {
                const long offset_1mb = 1048576;
                const long offset_05mb = 524288;

                // 1mb content contains more than 34,000 history lines for a typical usage, which should be
                // more than enough to cover 20,000 history records (a history record could be a multi-line
                // command). Similarly, 0.5mb content should be enough to cover 10,000 history records.
                // We optimize the file reading when the history count falls in those ranges. If the history
                // count is even larger, which should be very rare, we just read all lines.
                long offset = historyCount switch
                {
                    <= 10000 => offset_05mb,
                    <= 20000 => offset_1mb,
                    _ => 0,
                };

                using var fs = new FileStream(path, FileMode.Open);
                using var sr = new StreamReader(fs);

                if (offset > 0 && fs.Length > offset)
                {
                    // When the file size is larger than the offset, we only read that amount of content from the end.
                    fs.Seek(-offset, SeekOrigin.End);

                    // After seeking, the current position may point at the middle of a history record, or even at a
                    // byte within a UTF-8 character (history file is saved with UTF-8 encoding). So, let's ignore the
                    // first line read from that position.
                    sr.ReadLine();

                    string line;
                    while ((line = sr.ReadLine()) is not null)
                    {
                        if (!line.EndsWith("`", StringComparison.Ordinal))
                        {
                            // A complete history record is guaranteed to start from the next line.
                            break;
                        }
                    }
                }

                // Read lines in the streaming way, so it won't consume to much memory even if we have to
                // read all lines from a large history file.
                while (!sr.EndOfStream)
                {
                    yield return sr.ReadLine();
                }
            }
        }

        void UpdateHistoryFromFile(IEnumerable<string> historyLines, bool fromDifferentSession, bool fromInitialRead)
        {
            var sb = new StringBuilder();
            foreach (var line in historyLines)
            {
                if (line.EndsWith("`", StringComparison.Ordinal))
                {
                    sb.Append(line, 0, line.Length - 1);
                    sb.Append('\n');
                }
                else if (sb.Length > 0)
                {
                    sb.Append(line);
                    var l = sb.ToString();
                    var editItems = new List<EditItem> {EditItemInsertString.Create(l, 0)};
                    MaybeAddToHistory(l, editItems, 1, fromDifferentSession, fromInitialRead);
                    sb.Clear();
                }
                else
                {
                    var editItems = new List<EditItem> {EditItemInsertString.Create(line, 0)};
                    MaybeAddToHistory(line, editItems, 1, fromDifferentSession, fromInitialRead);
                }
            }
        }

        private static bool IsOnLeftSideOfAnAssignment(Ast ast, out Ast rhs)
        {
            bool result = false;
            rhs = null;

            do
            {
                if (ast.Parent is AssignmentStatementAst assignment)
                {
                    rhs = assignment.Right;
                    result = ReferenceEquals(assignment.Left, ast);

                    break;
                }

                ast = ast.Parent;
            }
            while (ast.Parent is not null);

            return result;
        }

        private static bool IsRightSideOfAnAssignmentSafe(Ast rhs)
        {
            if (rhs is PipelineAst)
            {
                // Right hand side is a pipeline.
                return true;
            }

            if (rhs is CommandExpressionAst cmdExprAst && cmdExprAst.Expression is MemberExpressionAst or InvokeMemberExpressionAst)
            {
                // Right hand side is a member access, or method invocation.
                return true;
            }

            return false;
        }

        private static bool IsSecretMgmtCommand(StringConstantExpressionAst strConst, out CommandAst command)
        {
            command = null;
            bool result = false;

            if (strConst.Parent is CommandAst cmdAst && ReferenceEquals(cmdAst.CommandElements[0], strConst) && s_SecretMgmtCommands.Contains(strConst.Value))
            {
                result = true;
                command = cmdAst;
            }

            return result;
        }

        private static bool IsSafePropertyUsage(Ast member)
        {
            bool result = false;

            if (member.Parent is MemberExpressionAst memberExpr)
            {
                // - If the property is NOT on the left side of an assignment, then it's safe.
                // - Otherwise, if the right-hand side is a pipeline or a variable, then we consider it safe.
                result = !IsOnLeftSideOfAnAssignment(memberExpr, out Ast rhs)
                    || rhs is PipelineAst
                    || (rhs is CommandExpressionAst cmdExpr && cmdExpr.Expression is VariableExpressionAst);
            }

            return result;
        }

        private static ExpressionAst GetArgumentForParameter(CommandParameterAst param)
        {
            if (param.Argument is not null)
            {
                return param.Argument;
            }

            var command = (CommandAst)param.Parent;
            int index = 1;
            for (; index < command.CommandElements.Count; index++)
            {
                if (ReferenceEquals(command.CommandElements[index], param))
                {
                    break;
                }
            }

            int argIndex = index + 1;
            if (argIndex < command.CommandElements.Count
                && command.CommandElements[argIndex] is ExpressionAst arg)
            {
                return arg;
            }

            return null;
        }

        private static bool IsCloudTokenOrSecretAccess(StringConstantExpressionAst arg2Ast, out CommandAst command)
        {
            bool result = false;
            command = arg2Ast.Parent as CommandAst;

            if (command is not null && command.CommandElements.Count >= 3
                && command.CommandElements[0] is StringConstantExpressionAst nameAst
                && command.CommandElements[1] is StringConstantExpressionAst arg1Ast
                && command.CommandElements[2] == arg2Ast)
            {
                string name = nameAst.Value;
                string arg1 = arg1Ast.Value;
                string arg2 = arg2Ast.Value;

                if (string.Equals(name, "gcloud", StringComparison.OrdinalIgnoreCase))
                {
                    result = string.Equals(arg1, "auth", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(arg2, "print-access-token", StringComparison.OrdinalIgnoreCase);
                }
                else if (string.Equals(name, "az", StringComparison.OrdinalIgnoreCase))
                {
                    result = string.Equals(arg1, "account", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(arg2, "get-access-token", StringComparison.OrdinalIgnoreCase);
                }
                else if (string.Equals(name, "kubectl", StringComparison.OrdinalIgnoreCase))
                {
                    result = (string.Equals(arg1, "get", StringComparison.OrdinalIgnoreCase) || string.Equals(arg1, "describe", StringComparison.OrdinalIgnoreCase))
                        && (string.Equals(arg2, "secrets", StringComparison.OrdinalIgnoreCase) || string.Equals(arg2, "secret", StringComparison.OrdinalIgnoreCase));
                }
            }

            if (!result)
            {
                command = null;
            }

            return result;
        }

        public static AddToHistoryOption GetDefaultAddToHistoryOption(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return AddToHistoryOption.SkipAdding;
            }

            Match match = s_sensitivePattern.Match(line);
            if (ReferenceEquals(match, Match.Empty))
            {
                return AddToHistoryOption.MemoryAndFile;
            }

            // The input contains at least one match of some sensitive patterns, so now we need to further
            // analyze the input using the ASTs to see if it should actually be considered sensitive.
            bool isSensitive = false;
            ParseError[] parseErrors = _singleton._parseErrors;

            // We need to compare the text here, instead of simply checking whether or not '_ast' is null.
            // This is because we may need to update from history file in the middle of editing an input,
            // and in that case, the '_ast' may be not-null, but it was not parsed from 'line'.
            Ast ast = string.Equals(_singleton._ast?.Extent.Text, line)
                ? _singleton._ast
                : Parser.ParseInput(line, out _, out parseErrors);

            if (parseErrors != null && parseErrors.Length > 0)
            {
                // If the input has any parsing errors, we cannot reliably analyze the AST. We just consider
                // it sensitive in this case, given that it contains matches of our sensitive pattern.
                return AddToHistoryOption.MemoryOnly;
            }

            do
            {
                int start = match.Index;
                int end = start + match.Length;

                IEnumerable<Ast> asts = ast.FindAll(
                    ast => ast.Extent.StartOffset <= start && ast.Extent.EndOffset >= end,
                    searchNestedScriptBlocks: true);

                Ast innerAst = asts.Last();
                switch (innerAst)
                {
                    case VariableExpressionAst:
                        // It's a variable with sensitive name. Using the variable is fine, but assigning to
                        // the variable could potentially expose sensitive content.
                        // If it appears on the left-hand-side of an assignment, and the right-hand-side is
                        // not a command invocation, we consider it sensitive.
                        // e.g. `$token = Get-Secret` vs. `$token = 'token-text'` or `$token, $url = ...`
                        isSensitive = IsOnLeftSideOfAnAssignment(innerAst, out Ast rhs) && !IsRightSideOfAnAssignmentSafe(rhs);

                        if (!isSensitive)
                        {
                            match = match.NextMatch();
                        }
                        break;

                    case StringConstantExpressionAst strConst:
                        isSensitive = true;
                        if (IsSecretMgmtCommand(strConst, out CommandAst command)
                            || IsCloudTokenOrSecretAccess(strConst, out command))
                        {
                            // If it's one of the secret management commands that we can ignore, we consider it safe.
                            isSensitive = false;
                            // And we can safely skip the whole command text in this case.
                            match = s_sensitivePattern.Match(line, command.Extent.EndOffset);
                        }
                        else if (IsSafePropertyUsage(strConst))
                        {
                            isSensitive = false;
                            match = match.NextMatch();
                        }

                        break;

                    case CommandParameterAst param:
                        // Special-case the '-AsPlainText' parameter.
                        if (string.Equals(param.ParameterName, "AsPlainText"))
                        {
                            isSensitive = true;
                            break;
                        }

                        ExpressionAst arg = GetArgumentForParameter(param);
                        if (arg is null)
                        {
                            // If no argument is found following the parameter, then it could be a switching parameter
                            // such as '-UseDefaultPassword' or '-SaveToken', which we assume will not expose sensitive information.
                            match = match.NextMatch();
                        }
                        else if (arg is VariableExpressionAst)
                        {
                            // Argument is a variable. It's fine to use a variable for a senstive parameter.
                            // e.g. `Invoke-WebRequest -Token $token`
                            match = s_sensitivePattern.Match(line, arg.Extent.EndOffset);
                        }
                        else if (arg is ParenExpressionAst paren
                            && paren.Pipeline is PipelineAst pipeline
                            && pipeline.PipelineElements[0] is not CommandExpressionAst)
                        {
                            // Argument is a command invocation, such as `Invoke-WebRequest -Token (Get-Secret)`.
                            match = match.NextMatch();
                        }
                        else
                        {
                            // We consider all other arguments sensitive.
                            isSensitive = true;
                        }
                        break;

                    default:
                        isSensitive = true;
                        break;
                }
            }
            while (!isSensitive && !ReferenceEquals(match, Match.Empty));

            return isSensitive ? AddToHistoryOption.MemoryOnly : AddToHistoryOption.MemoryAndFile;
        }

        /// <summary>
        /// Add a command to the history - typically used to restore
        /// history from a previous session.
        /// </summary>
        public static void AddToHistory(string command)
        {
            command = command.Replace("\r\n", "\n");
            var editItems = new List<EditItem> {EditItemInsertString.Create(command, 0)};
            _singleton.MaybeAddToHistory(command, editItems, 1);
        }

        /// <summary>
        /// Clears history in PSReadLine.  This does not affect PowerShell history.
        /// </summary>
        public static void ClearHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._history?.Clear();
            _singleton._recentHistory?.Clear();
            _singleton._currentHistoryIndex = 0;
        }

        /// <summary>
        /// Return a collection of history items.
        /// </summary>
        public static HistoryItem[] GetHistoryItems()
        {
            return _singleton._history.ToArray();
        }

        enum HistoryMoveCursor { ToEnd, ToBeginning, DontMove }

        private void UpdateFromHistory(HistoryMoveCursor moveCursor)
        {
            string line;
            if (_currentHistoryIndex == _history.Count)
            {
                line = _savedCurrentLine.CommandLine;
                _edits = new List<EditItem>(_savedCurrentLine._edits);
                _undoEditIndex = _savedCurrentLine._undoEditIndex;
                _editGroupStart = _savedCurrentLine._editGroupStart;
            }
            else
            {
                line = _history[_currentHistoryIndex].CommandLine;
                _edits = new List<EditItem>(_history[_currentHistoryIndex]._edits);
                _undoEditIndex = _history[_currentHistoryIndex]._undoEditIndex;
                _editGroupStart = _history[_currentHistoryIndex]._editGroupStart;
            }
            _buffer.Clear();
            _buffer.Append(line);

            switch (moveCursor)
            {
                case HistoryMoveCursor.ToEnd:
                    _current = Math.Max(0, _buffer.Length + ViEndOfLineFactor);
                    break;
                case HistoryMoveCursor.ToBeginning:
                    _current = 0;
                    break;
                default:
                    if (_current > _buffer.Length)
                    {
                        _current = Math.Max(0, _buffer.Length + ViEndOfLineFactor);
                    }
                    break;
            }

            using var _ = _prediction.DisableScoped();
            Render();
        }

        private void SaveCurrentLine()
        {
            // We're called before any history operation - so it's convenient
            // to check if we need to load history from another sessions now.
            MaybeReadHistoryFile();

            _anyHistoryCommandCount += 1;
            if (_savedCurrentLine.CommandLine == null)
            {
                _savedCurrentLine.CommandLine = _buffer.ToString();
                _savedCurrentLine._edits = _edits;
                _savedCurrentLine._undoEditIndex = _undoEditIndex;
                _savedCurrentLine._editGroupStart = _editGroupStart;
            }
        }

        private void HistoryRecall(int direction)
        {
            if (_recallHistoryCommandCount == 0 && LineIsMultiLine())
            {
                MoveToLine(direction);
                return;
            }

            if (Options.HistoryNoDuplicates && _recallHistoryCommandCount == 0)
            {
                _hashedHistory = new Dictionary<string, int>();
            }

            int count = Math.Abs(direction);
            direction = direction < 0 ? -1 : +1;
            int newHistoryIndex = _currentHistoryIndex;
            while (count > 0)
            {
                newHistoryIndex += direction;
                if (newHistoryIndex < 0 || newHistoryIndex >= _history.Count)
                {
                    break;
                }

                if (_history[newHistoryIndex].FromOtherSession)
                {
                    continue;
                }

                if (Options.HistoryNoDuplicates)
                {
                    var line = _history[newHistoryIndex].CommandLine;
                    if (!_hashedHistory.TryGetValue(line, out var index))
                    {
                        _hashedHistory.Add(line, newHistoryIndex);
                        --count;
                    }
                    else if (newHistoryIndex == index)
                    {
                        --count;
                    }
                }
                else
                {
                    --count;
                }
            }
            _recallHistoryCommandCount += 1;
            if (newHistoryIndex >= 0 && newHistoryIndex <= _history.Count)
            {
                _currentHistoryIndex = newHistoryIndex;
                var moveCursor = InViCommandMode() && !_options.HistorySearchCursorMovesToEnd
                    ? HistoryMoveCursor.ToBeginning
                    : HistoryMoveCursor.ToEnd;
                UpdateFromHistory(moveCursor);
            }
        }

        /// <summary>
        /// Replace the current input with the 'previous' item from PSReadLine history.
        /// </summary>
        public static void PreviousHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, -1);
            if (numericArg > 0)
            {
                numericArg = -numericArg;
            }

            if (UpdateListSelection(numericArg))
            {
                return;
            }

            _singleton.SaveCurrentLine();
            _singleton.HistoryRecall(numericArg);
        }

        /// <summary>
        /// Replace the current input with the 'next' item from PSReadLine history.
        /// </summary>
        public static void NextHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, +1);
            if (UpdateListSelection(numericArg))
            {
                return;
            }

            _singleton.SaveCurrentLine();
            _singleton.HistoryRecall(numericArg);
        }

        private void HistorySearch(int direction)
        {
            if (_searchHistoryCommandCount == 0)
            {
                if (LineIsMultiLine())
                {
                    MoveToLine(direction);
                    return;
                }

                _searchHistoryPrefix = _buffer.ToString(0, _current);
                _emphasisStart = 0;
                _emphasisLength = _current;
                if (Options.HistoryNoDuplicates)
                {
                    _hashedHistory = new Dictionary<string, int>();
                }
            }
            _searchHistoryCommandCount += 1;

            int count = Math.Abs(direction);
            direction = direction < 0 ? -1 : +1;
            int newHistoryIndex = _currentHistoryIndex;
            while (count > 0)
            {
                newHistoryIndex += direction;
                if (newHistoryIndex < 0 || newHistoryIndex >= _history.Count)
                {
                    break;
                }

                if (_history[newHistoryIndex].FromOtherSession && _searchHistoryPrefix.Length == 0)
                {
                    continue;
                }

                var line = _history[newHistoryIndex].CommandLine;
                if (line.StartsWith(_searchHistoryPrefix, Options.HistoryStringComparison))
                {
                    if (Options.HistoryNoDuplicates)
                    {
                        if (!_hashedHistory.TryGetValue(line, out var index))
                        {
                            _hashedHistory.Add(line, newHistoryIndex);
                            --count;
                        }
                        else if (index == newHistoryIndex)
                        {
                            --count;
                        }
                    }
                    else
                    {
                        --count;
                    }
                }
            }

            if (newHistoryIndex >= 0 && newHistoryIndex <= _history.Count)
            {
                // Set '_current' back to where it was when starting the first search, because
                // it might be changed during the rendering of the last matching history command.
                _current = _emphasisLength;
                _currentHistoryIndex = newHistoryIndex;
                var moveCursor = InViCommandMode()
                    ? HistoryMoveCursor.ToBeginning
                    : Options.HistorySearchCursorMovesToEnd
                        ? HistoryMoveCursor.ToEnd
                        : HistoryMoveCursor.DontMove;
                UpdateFromHistory(moveCursor);
            }
        }

        /// <summary>
        /// Move to the first item in the history.
        /// </summary>
        public static void BeginningOfHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.SaveCurrentLine();
            _singleton._currentHistoryIndex = 0;
            _singleton.UpdateFromHistory(HistoryMoveCursor.ToEnd);
        }

        /// <summary>
        /// Move to the last item (the current input) in the history.
        /// </summary>
        public static void EndOfHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.SaveCurrentLine();
            GoToEndOfHistory();
        }

        private static void GoToEndOfHistory()
        {
            _singleton._currentHistoryIndex = _singleton._history.Count;
            _singleton.UpdateFromHistory(HistoryMoveCursor.ToEnd);
        }

        /// <summary>
        /// Replace the current input with the 'previous' item from PSReadLine history
        /// that matches the characters between the start and the input and the cursor.
        /// </summary>
        public static void HistorySearchBackward(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, -1);
            if (numericArg > 0)
            {
                numericArg = -numericArg;
            }

            if (UpdateListSelection(numericArg))
            {
                return;
            }

            _singleton.SaveCurrentLine();
            _singleton.HistorySearch(numericArg);
        }

        /// <summary>
        /// Replace the current input with the 'next' item from PSReadLine history
        /// that matches the characters between the start and the input and the cursor.
        /// </summary>
        public static void HistorySearchForward(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, +1);
            if (UpdateListSelection(numericArg))
            {
                return;
            }

            _singleton.SaveCurrentLine();
            _singleton.HistorySearch(numericArg);
        }

        private void UpdateHistoryDuringInteractiveSearch(string toMatch, int direction, ref int searchFromPoint)
        {
            searchFromPoint += direction;
            for (; searchFromPoint >= 0 && searchFromPoint < _history.Count; searchFromPoint += direction)
            {
                var line = _history[searchFromPoint].CommandLine;
                var startIndex = line.IndexOf(toMatch, Options.HistoryStringComparison);
                if (startIndex >= 0)
                {
                    if (Options.HistoryNoDuplicates)
                    {
                        if (!_hashedHistory.TryGetValue(line, out var index))
                        {
                            _hashedHistory.Add(line, searchFromPoint);
                        }
                        else if (index != searchFromPoint)
                        {
                            continue;
                        }
                    }
                    _statusLinePrompt = direction > 0 ? _forwardISearchPrompt : _backwardISearchPrompt;
                    _current = startIndex;
                    _emphasisStart = startIndex;
                    _emphasisLength = toMatch.Length;
                    _currentHistoryIndex = searchFromPoint;
                    var moveCursor = Options.HistorySearchCursorMovesToEnd
                        ? HistoryMoveCursor.ToEnd
                        : HistoryMoveCursor.DontMove;
                    UpdateFromHistory(moveCursor);
                    return;
                }
            }

            // Make sure we're never more than 1 away from being in range so if they
            // reverse direction, the first time they reverse they are back in range.
            if (searchFromPoint < 0)
                searchFromPoint = -1;
            else if (searchFromPoint >= _history.Count)
                searchFromPoint = _history.Count;

            _emphasisStart = -1;
            _emphasisLength = 0;
            _statusLinePrompt = direction > 0 ? _failedForwardISearchPrompt : _failedBackwardISearchPrompt;
            Render();
        }

        private void InteractiveHistorySearchLoop(int direction)
        {
            var searchFromPoint = _currentHistoryIndex;
            var searchPositions = new Stack<int>();
            searchPositions.Push(_currentHistoryIndex);

            if (Options.HistoryNoDuplicates)
            {
                _hashedHistory = new Dictionary<string, int>();
            }

            var toMatch = new StringBuilder(64);
            while (true)
            {
                Action<ConsoleKeyInfo?, object> function = null;

                var key = ReadKey();
                _dispatchTable.TryGetValue(key, out var handlerOrChordDispatchTable);
                if (handlerOrChordDispatchTable?.TryGetKeyHandler(out var handler) == true)
                    function = handler.Action;

                if (function == ReverseSearchHistory)
                {
                    UpdateHistoryDuringInteractiveSearch(toMatch.ToString(), -1, ref searchFromPoint);
                }
                else if (function == ForwardSearchHistory)
                {
                    UpdateHistoryDuringInteractiveSearch(toMatch.ToString(), +1, ref searchFromPoint);
                }
                else if (function == BackwardDeleteChar
                    || key == Keys.Backspace
                    || key == Keys.CtrlH)
                {
                    if (toMatch.Length > 0)
                    {
                        toMatch.Remove(toMatch.Length - 1, 1);
                        _statusBuffer.Remove(_statusBuffer.Length - 2, 1);
                        searchPositions.Pop();
                        searchFromPoint = _currentHistoryIndex = searchPositions.Peek();
                        var moveCursor = Options.HistorySearchCursorMovesToEnd
                            ? HistoryMoveCursor.ToEnd
                            : HistoryMoveCursor.DontMove;
                        UpdateFromHistory(moveCursor);

                        if (_hashedHistory != null)
                        {
                            // Remove any entries with index < searchFromPoint because
                            // we are starting the search from this new index - we always
                            // want to find the latest entry that matches the search string
                            foreach (var pair in _hashedHistory.ToArray())
                            {
                                if (pair.Value < searchFromPoint)
                                {
                                    _hashedHistory.Remove(pair.Key);
                                }
                            }
                        }

                        // Prompt may need to have 'failed-' removed.
                        var toMatchStr = toMatch.ToString();
                        var startIndex = _buffer.ToString().IndexOf(toMatchStr, Options.HistoryStringComparison);
                        if (startIndex >= 0)
                        {
                            _statusLinePrompt = direction > 0 ? _forwardISearchPrompt : _backwardISearchPrompt;
                            _current = startIndex;
                            _emphasisStart = startIndex;
                            _emphasisLength = toMatch.Length;
                            Render();
                        }
                    }
                    else
                    {
                        Ding();
                    }
                }
                else if (key == Keys.Escape)
                {
                    // End search
                    break;
                }
                else if (function == Abort)
                {
                    // Abort search
                    GoToEndOfHistory();
                    break;
                }
                else
                {
                    char toAppend = key.KeyChar;
                    if (char.IsControl(toAppend))
                    {
                        PrependQueuedKeys(key);
                        break;
                    }
                    toMatch.Append(toAppend);
                    _statusBuffer.Insert(_statusBuffer.Length - 1, toAppend);

                    var toMatchStr = toMatch.ToString();
                    var startIndex = _buffer.ToString().IndexOf(toMatchStr, Options.HistoryStringComparison);
                    if (startIndex < 0)
                    {
                        UpdateHistoryDuringInteractiveSearch(toMatchStr, direction, ref searchFromPoint);
                    }
                    else
                    {
                        _current = startIndex;
                        _emphasisStart = startIndex;
                        _emphasisLength = toMatch.Length;
                        Render();
                    }
                    searchPositions.Push(_currentHistoryIndex);
                }
            }
        }

        private void InteractiveHistorySearch(int direction)
        {
            using var _ = _prediction.DisableScoped();
            SaveCurrentLine();

            // Add a status line that will contain the search prompt and string
            _statusLinePrompt = direction > 0 ? _forwardISearchPrompt : _backwardISearchPrompt;
            _statusBuffer.Append("_");

            Render(); // Render prompt
            InteractiveHistorySearchLoop(direction);

            _emphasisStart = -1;
            _emphasisLength = 0;

            // Remove our status line, this will render
            ClearStatusMessage(render: true);
        }

        /// <summary>
        /// Perform an incremental forward search through history.
        /// </summary>
        public static void ForwardSearchHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.InteractiveHistorySearch(+1);
        }

        /// <summary>
        /// Perform an incremental backward search through history.
        /// </summary>
        public static void ReverseSearchHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.InteractiveHistorySearch(-1);
        }
    }
}
