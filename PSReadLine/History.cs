using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.PSReadLine;

public class History
{
    static History()
    {
        Singleton = new History();
    }

    private History()
    {
        RecentHistory = new HistoryQueue<string>(5);
    }

    // When cycling through history, the current line (not yet added to history)
    // is saved here so it can be restored.
    public static History Singleton { get; }

    // Pattern used to check for sensitive inputs.
    private static Regex SensitivePattern { get; } = new(
        "password|asplaintext|token|apikey|secret",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static HashSet<string> SecretMgmtCommands { get; } = new(StringComparer.OrdinalIgnoreCase)
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
        "Unregister-SecretVault"
    };

    public int AnyHistoryCommandCount { get; set; }

    // History state
    public HistoryQueue<HistoryItem> Historys { get; set; }

    public int GetNextHistoryIndex { get; set; }

    public Dictionary<string, int> HashedHistory { get; set; }

    public long HistoryFileLastSavedSize { get; set; }

    public Mutex HistoryFileMutex { get; set; }

    public HistoryItem PreviousHistoryItem { get; private set; }

    public int RecallHistoryCommandCount { get; set; }

    public HistoryQueue<string> RecentHistory { get; set; }

    public int SearchHistoryCommandCount { get; set; }

    public string SearchHistoryPrefix { get; set; }

    private int HistoryErrorReportedCount { get; set; }

    public void DelayedInit()
    {
        Historys = new HistoryQueue<HistoryItem>(_rl.Options.MaximumHistoryCount);
        HistoryFileMutex = new Mutex(false, GetHistorySaveFileMutexName());
        ReadHistoryFile1();

        void ReadHistoryFile1()
        {
            var readHistoryFile = true;
            try
            {
                if (_rl.Options.HistorySaveStyle == HistorySaveStyle.SaveNothing &&
                    Runspace.DefaultRunspace != null)
                    using (var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
                    {
                        ps.AddCommand("Microsoft.PowerShell.Core\\Get-History");
                        foreach (var historyInfo in ps.Invoke<HistoryInfo>())
                            AddToHistory(historyInfo.CommandLine);

                        readHistoryFile = false;
                    }
            }
            catch
            {
                // ignored
            }

            if (readHistoryFile) ReadHistoryFile();
        }
    }

    public static void SetRenderData(int startIndex, int length, CursorPosition p)
    {
        EP.SetEmphasisData(new EmphasisRange[] {new(startIndex, length)});
        var endIndex = startIndex + length;
        _renderer.Current = p switch
        {
            CursorPosition.Start => startIndex,
            CursorPosition.End => endIndex,
            _ => throw new ArgumentException(@"Invalid enum value for CursorPosition", nameof(p))
        };
    }

    private void HistorySearch(int direction)
    {
        if (SearchHistoryCommandCount == 0)
        {
            if (_renderer.LineIsMultiLine())
            {
                _rl.MoveToLine(direction);
                return;
            }

            SearchHistoryPrefix = _rl.buffer.ToString(0, _renderer.Current);

            SetRenderData(0, _renderer.Current, CursorPosition.End);

            if (_rl.Options.HistoryNoDuplicates) HashedHistory = new Dictionary<string, int>();
        }

        SearchHistoryCommandCount += 1;

        var count = Math.Abs(direction);
        direction = direction < 0 ? -1 : +1;
        var newHistoryIndex = SearcherReadLine.CurrentHistoryIndex;
        while (count > 0)
        {
            newHistoryIndex += direction;
            if (newHistoryIndex < 0 || newHistoryIndex >= Historys.Count) break;

            if (Historys[newHistoryIndex].FromOtherSession && SearchHistoryPrefix.Length == 0) continue;

            var line = Historys[newHistoryIndex].CommandLine;
            if (line.StartsWith(SearchHistoryPrefix, _rl.Options.HistoryStringComparison))
            {
                if (_rl.Options.HistoryNoDuplicates)
                {
                    if (!HashedHistory.TryGetValue(line, out var index))
                    {
                        HashedHistory.Add(line, newHistoryIndex);
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

        if (newHistoryIndex >= 0 && newHistoryIndex <= Historys.Count)
        {
            // Set '_current' back to where it was when starting the first search, because
            // it might be changed during the rendering of the last matching history command.
            // _renderer.Current = HistorySearcherReadLine.EmphasisLength;
            SearcherReadLine.CurrentHistoryIndex = newHistoryIndex;
            var moveCursor = RL.InViCommandMode()
                ? HistorySearcherReadLine.HistoryMoveCursor.ToBeginning
                : _rl.Options.HistorySearchCursorMovesToEnd
                    ? HistorySearcherReadLine.HistoryMoveCursor.ToEnd
                    : HistorySearcherReadLine.HistoryMoveCursor.DontMove;
            SearcherReadLine.UpdateBufferFromHistory(moveCursor);
        }
    }


    /// <summary>
    ///     Replace the current input with the 'previous' item from PSReadLine history
    ///     that matches the characters between the start and the input and the cursor.
    /// </summary>
    public static void HistorySearchBackward(ConsoleKeyInfo? key = null, object arg = null)
    {
        RL.TryGetArgAsInt(arg, out var numericArg, -1);
        if (numericArg > 0) numericArg = -numericArg;
        if (RL.UpdateListSelection(numericArg)) return;
        SearcherReadLine.SaveCurrentLine();
        Singleton.HistorySearch(numericArg);
    }

    /// <summary>
    ///     Replace the current input with the 'previous' item from PSReadLine history.
    /// </summary>
    public static void PreviousHistory(ConsoleKeyInfo? key = null, object arg = null)
    {
        RL.TryGetArgAsInt(arg, out var numericArg, -1);
        if (numericArg > 0) numericArg = -numericArg;

        if (RL.UpdateListSelection(numericArg)) return;

        SearcherReadLine.SaveCurrentLine();
        Singleton.HistoryRecall(numericArg);
    }

    /// <summary>
    ///     Replace the current input with the 'next' item from PSReadLine history.
    /// </summary>
    public static void NextHistory(ConsoleKeyInfo? key = null, object arg = null)
    {
        RL.TryGetArgAsInt(arg, out var numericArg, +1);
        if (RL.UpdateListSelection(numericArg)) return;

        SearcherReadLine.SaveCurrentLine();
        Singleton.HistoryRecall(numericArg);
    }

    /// <summary>
    ///     Return a collection of history items.
    /// </summary>
    public static HistoryItem[] GetHistoryItems()
    {
        return Singleton.Historys.ToArray();
    }

    /// <summary>
    ///     Move to the first item in the history.
    /// </summary>
    public static void BeginningOfHistory(ConsoleKeyInfo? key = null, object arg = null)
    {
        SearcherReadLine.SaveCurrentLine();
        SearcherReadLine.ResetCurrentHistoryIndex(true);
        SearcherReadLine.UpdateBufferFromHistory(HistorySearcherReadLine.HistoryMoveCursor.ToEnd);
    }


    /// <summary>
    ///     Clears history in PSReadLine.  This does not affect PowerShell history.
    /// </summary>
    public static void ClearHistory(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton.Historys?.Clear();
        Singleton.RecentHistory?.Clear();
        SearcherReadLine.ResetCurrentHistoryIndex();
    }

    /// <summary>
    ///     Move to the last item (the current input) in the history.
    /// </summary>
    public static void EndOfHistory(ConsoleKeyInfo? key = null, object arg = null)
    {
        SearcherReadLine.SaveCurrentLine();
        GoToEndOfHistory();
    }

    /// <summary>
    ///     Add a command to the history - typically used to restore
    ///     history from a previous session.
    /// </summary>
    public static void AddToHistory(string command)
    {
        command = command.Replace("\r\n", "\n");
        var editItems = new List<EditItem> {PSConsoleReadLine.EditItemInsertString.Create(command, 0)};
        Singleton.MaybeAddToHistory(command, editItems, 1);
    }

    private static ExpressionAst GetArgumentForParameter(CommandParameterAst param)
    {
        if (param.Argument is not null) return param.Argument;

        var command = (CommandAst) param.Parent;
        var index = 1;
        for (; index < command.CommandElements.Count; index++)
            if (ReferenceEquals(command.CommandElements[index], param))
                break;

        var argIndex = index + 1;
        if (argIndex < command.CommandElements.Count
            && command.CommandElements[argIndex] is ExpressionAst arg)
            return arg;

        return null;
    }

    private static bool IsSecretMgmtCommand(StringConstantExpressionAst strConst, out CommandAst command)
    {
        var result = false;
        command = strConst.Parent as CommandAst;

        if (command is not null)
            result = ReferenceEquals(command.CommandElements[0], strConst)
                     && SecretMgmtCommands.Contains(strConst.Value);

        return result;
    }

    private static bool IsOnLeftSideOfAnAssignment(Ast ast, out Ast rhs)
    {
        var result = false;
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
        } while (ast.Parent is not null);

        return result;
    }

    public void SaveHistoryAtExit()
    {
        var end = Historys.Count - 1;
        WriteHistoryRange(0, end, true);
    }

    public void ReadHistoryFile()
    {
        if (File.Exists(_rl.Options.HistorySavePath))
            WithHistoryFileMutexDo(1000, () =>
            {
                var historyLines = File.ReadAllLines(_rl.Options.HistorySavePath);
                UpdateHistoryFromFile(historyLines, false, true);
                var fileInfo = new FileInfo(_rl.Options.HistorySavePath);
                HistoryFileLastSavedSize = fileInfo.Length;
            });
    }

    public string GetHistorySaveFileMutexName()
    {
        // Return a reasonably unique name - it's not too important as there will rarely
        // be any contention.
        var hashFromPath = FNV1a32Hash.ComputeHash(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? _rl.Options.HistorySavePath.ToLower()
                : _rl.Options.HistorySavePath);
        return "PSReadLineHistoryFile_" + hashFromPath;
    }

    /// <summary>
    ///     Delete the character before the cursor.
    /// </summary>
    private static void BackwardDeleteChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (_rl._visualSelectionCommandCount > 0)
        {
            _renderer.GetRegion(out var start, out var length);
            PSConsoleReadLine.Delete(start, length);
            return;
        }

        if (_rl.buffer.Length > 0 && _renderer.Current > 0)
        {
            var qty = arg as int? ?? 1;
            if (qty < 1) return; // Ignore useless counts
            qty = Math.Min(qty, _renderer.Current);

            var startDeleteIndex = _renderer.Current - qty;

            _rl.RemoveTextToViRegister(startDeleteIndex, qty, BackwardDeleteChar, arg,
                !PSConsoleReadLine.InViEditMode());
            _renderer.Current = startDeleteIndex;
            _renderer.Render();
        }
    }


    private void UpdateHistoryFromFile(IEnumerable<string> historyLines, bool fromDifferentSession,
        bool fromInitialRead)
    {
        var sb = new StringBuilder();
        foreach (var line in historyLines)
            if (line.EndsWith("`", StringComparison.Ordinal))
            {
                sb.Append(line, 0, line.Length - 1);
                sb.Append('\n');
            }
            else if (sb.Length > 0)
            {
                sb.Append(line);
                var l = sb.ToString();
                var editItems = new List<EditItem> {PSConsoleReadLine.EditItemInsertString.Create(l, 0)};
                MaybeAddToHistory(l, editItems, 1, fromDifferentSession, fromInitialRead);
                sb.Clear();
            }
            else
            {
                var editItems = new List<EditItem> {PSConsoleReadLine.EditItemInsertString.Create(line, 0)};
                MaybeAddToHistory(line, editItems, 1, fromDifferentSession, fromInitialRead);
            }
    }

    private bool WithHistoryFileMutexDo(int timeout, Action action)
    {
        var retryCount = 0;
        do
        {
            try
            {
                if (HistoryFileMutex.WaitOne(timeout))
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
                        HistoryFileMutex.ReleaseMutex();
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
                HistoryFileMutex.ReleaseMutex();
            }
        } while (retryCount is > 0 and < 3);

        // If we reach here, that means we've done the retries but always got the 'AbandonedMutexException'.
        return false;
    }

    private void WriteHistoryRange(int start, int end, bool overwritten)
    {
        WithHistoryFileMutexDo(100, () =>
        {
            var retry = true;
            // Get the new content since the last sync.
            var historyLines = overwritten ? null : ReadHistoryFileIncrementally();

            try
            {
                retry_after_creating_directory:
                try
                {
                    using (var file = overwritten
                               ? File.CreateText(_rl.Options.HistorySavePath)
                               : File.AppendText(_rl.Options.HistorySavePath))
                    {
                        for (var i = start; i <= end; i++)
                        {
                            var item = Historys[i];
                            item._saved = true;

                            // Actually, skip writing sensitive items to file.
                            if (item._sensitive) continue;

                            var line = item.CommandLine.Replace("\n", "`\n");
                            file.WriteLine(line);
                        }
                    }

                    var fileInfo = new FileInfo(_rl.Options.HistorySavePath);
                    HistoryFileLastSavedSize = fileInfo.Length;
                }
                catch (DirectoryNotFoundException)
                {
                    // Try making the directory, but just once
                    if (retry)
                    {
                        retry = false;
                        Directory.CreateDirectory(Path.GetDirectoryName(_rl.Options.HistorySavePath));
                        goto retry_after_creating_directory;
                    }
                }
            }
            finally
            {
                if (historyLines != null)
                    // Populate new history from other sessions to the history queue after we are done
                    // with writing the specified range to the file.
                    // We do it at this point to make sure the range of history items from 'start' to
                    // 'end' do not get changed before the writing to the file.
                    UpdateHistoryFromFile(historyLines, true, false);
            }
        });
    }

    private void ReportHistoryFileError(Exception e)
    {
        if (HistoryErrorReportedCount == 2)
            return;

        HistoryErrorReportedCount += 1;
        Console.Write(_rl.Options._errorColor);
        Console.WriteLine(PSReadLineResources.HistoryFileErrorMessage, _rl.Options.HistorySavePath, e.Message);
        if (HistoryErrorReportedCount == 2) Console.WriteLine(PSReadLineResources.HistoryFileErrorFinalMessage);
        Console.Write("\x1b0m");
    }

    private void HistoryRecall(int direction)
    {
        if (RecallHistoryCommandCount == 0 && _renderer.LineIsMultiLine())
        {
            _rl.MoveToLine(direction);
            return;
        }

        if (_rl.Options.HistoryNoDuplicates && RecallHistoryCommandCount == 0)
            HashedHistory = new Dictionary<string, int>();

        var count = Math.Abs(direction);
        direction = direction < 0 ? -1 : +1;
        var newHistoryIndex = SearcherReadLine.CurrentHistoryIndex;
        while (count > 0)
        {
            newHistoryIndex += direction;
            if (newHistoryIndex < 0 || newHistoryIndex >= Historys.Count) break;

            if (Historys[newHistoryIndex].FromOtherSession) continue;

            if (_rl.Options.HistoryNoDuplicates)
            {
                var line = Historys[newHistoryIndex].CommandLine;
                if (!HashedHistory.TryGetValue(line, out var index))
                {
                    HashedHistory.Add(line, newHistoryIndex);
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

        RecallHistoryCommandCount = RecallHistoryCommandCount + 1;
        if (newHistoryIndex >= 0 && newHistoryIndex <= Historys.Count)
        {
            SearcherReadLine.CurrentHistoryIndex = newHistoryIndex;
            var moveCursor = RL.InViCommandMode() && !_rl.Options.HistorySearchCursorMovesToEnd
                ? HistorySearcherReadLine.HistoryMoveCursor.ToBeginning
                : HistorySearcherReadLine.HistoryMoveCursor.ToEnd;
            SearcherReadLine.UpdateBufferFromHistory(moveCursor);
        }
    }

    public static AddToHistoryOption GetDefaultAddToHistoryOption(string line)
    {
        if (string.IsNullOrEmpty(line)) return AddToHistoryOption.SkipAdding;

        var sSensitivePattern = SensitivePattern;
        var match = sSensitivePattern.Match(line);
        if (ReferenceEquals(match, Match.Empty)) return AddToHistoryOption.MemoryAndFile;

        // The input contains at least one match of some sensitive patterns, so now we need to further
        // analyze the input using the ASTs to see if it should actually be considered sensitive.
        var isSensitive = false;
        var parseErrors = _rl.ParseErrors;

        // We need to compare the text here, instead of simply checking whether or not '_ast' is null.
        // This is because we may need to update from history file in the middle of editing an input,
        // and in that case, the '_ast' may be not-null, but it was not parsed from 'line'.
        var ast = string.Equals(_rl.RLAst?.Extent.Text, line)
            ? _rl.RLAst
            : Parser.ParseInput(line, out _, out parseErrors);

        if (parseErrors is {Length: > 0})
            // If the input has any parsing errors, we cannot reliably analyze the AST. We just consider
            // it sensitive in this case, given that it contains matches of our sensitive pattern.
            return AddToHistoryOption.MemoryOnly;

        do
        {
            var start = match.Index;
            var end = start + match.Length;

            var asts = ast.FindAll(
                ast => ast.Extent.StartOffset <= start && ast.Extent.EndOffset >= end,
                true);

            var innerAst = asts.Last();
            switch (innerAst)
            {
                case VariableExpressionAst:
                    // It's a variable with sensitive name. Using the variable is fine, but assigning to
                    // the variable could potentially expose sensitive content.
                    // If it appears on the left-hand-side of an assignment, and the right-hand-side is
                    // not a command invocation, we consider it sensitive.
                    // e.g. `$token = Get-Secret` vs. `$token = 'token-text'` or `$token, $url = ...`
                    isSensitive = IsOnLeftSideOfAnAssignment(innerAst, out var rhs)
                                  && rhs is not PipelineAst;

                    if (!isSensitive) match = match.NextMatch();
                    break;

                case StringConstantExpressionAst strConst:
                    // If it's not a command name, or it's not one of the secret management commands that
                    // we can ignore, we consider it sensitive.
                    isSensitive = !IsSecretMgmtCommand(strConst, out var command);

                    if (!isSensitive)
                        // We can safely skip the whole command text.
                        match = sSensitivePattern.Match(line, command.Extent.EndOffset);
                    break;

                case CommandParameterAst param:
                    // Special-case the '-AsPlainText' parameter.
                    if (string.Equals(param.ParameterName, "AsPlainText"))
                    {
                        isSensitive = true;
                        break;
                    }

                    var arg = GetArgumentForParameter(param);
                    if (arg is null)
                        // If no argument is found following the parameter, then it could be a switching parameter
                        // such as '-UseDefaultPassword' or '-SaveToken', which we assume will not expose sensitive information.
                        match = match.NextMatch();
                    else if (arg is VariableExpressionAst)
                        // Argument is a variable. It's fine to use a variable for a sensitive parameter.
                        // e.g. `Invoke-WebRequest -Token $token`
                        match = sSensitivePattern.Match(line, arg.Extent.EndOffset);
                    else if (arg is ParenExpressionAst {Pipeline: PipelineAst pipeline} &&
                             pipeline.PipelineElements[0] is not CommandExpressionAst)
                        // Argument is a command invocation, such as `Invoke-WebRequest -Token (Get-Secret)`.
                        match = match.NextMatch();
                    else
                        // We consider all other arguments sensitive.
                        isSensitive = true;
                    break;

                default:
                    isSensitive = true;
                    break;
            }
        } while (!isSensitive && !ReferenceEquals(match, Match.Empty));

        return isSensitive ? AddToHistoryOption.MemoryOnly : AddToHistoryOption.MemoryAndFile;
    }

    private AddToHistoryOption GetAddToHistoryOption(string line)
    {
        // Whitespace only is useless, never add.
        if (string.IsNullOrWhiteSpace(line)) return AddToHistoryOption.SkipAdding;

        // Under "no dupes" (which is on by default), immediately drop dupes of the previous line.
        if (_rl.Options.HistoryNoDuplicates && Historys.Count > 0 &&
            string.Equals(Historys[Historys.Count - 1].CommandLine, line, StringComparison.Ordinal))
            return AddToHistoryOption.SkipAdding;

        if (_rl.Options.AddToHistoryHandler != null)
        {
            if (_rl.Options.AddToHistoryHandler == PSConsoleReadLineOptions.DefaultAddToHistoryHandler)
                // Avoid boxing if it's the default handler.
                return GetDefaultAddToHistoryOption(line);

            var value = _rl.Options.AddToHistoryHandler(line);
            if (value is PSObject psObj) value = psObj.BaseObject;

            if (value is bool boolValue)
                return boolValue ? AddToHistoryOption.MemoryAndFile : AddToHistoryOption.SkipAdding;

            if (value is AddToHistoryOption enumValue) return enumValue;

            if (value is string strValue && Enum.TryParse(strValue, out enumValue)) return enumValue;

            // 'TryConvertTo' incurs exception handling when the value cannot be converted to the target type.
            // It's expensive, especially when we need to process lots of history items from file during the
            // initialization. So do the conversion as the last resort.
            if (LanguagePrimitives.TryConvertTo(value, out enumValue)) return enumValue;
        }

        // Add to both history queue and file by default.
        return AddToHistoryOption.MemoryAndFile;
    }

    private void IncrementalHistoryWrite()
    {
        var i = SearcherReadLine.CurrentHistoryIndex - 1;
        while (i >= 0)
        {
            if (Historys[i]._saved) break;
            i -= 1;
        }

        WriteHistoryRange(i + 1, Historys.Count - 1, false);
    }

    public string MaybeAddToHistory(
        string result,
        List<EditItem> edits,
        int undoEditIndex,
        bool fromDifferentSession = false,
        bool fromInitialRead = false)
    {
        var addToHistoryOption = GetAddToHistoryOption(result);
        if (addToHistoryOption != AddToHistoryOption.SkipAdding)
        {
            var fromHistoryFile = fromDifferentSession || fromInitialRead;
            PreviousHistoryItem = new HistoryItem
            {
                CommandLine = result,
                _edits = edits,
                _undoEditIndex = undoEditIndex,
                _editGroupStart = -1,
                _saved = fromHistoryFile,
                FromOtherSession = fromDifferentSession,
                FromHistoryFile = fromInitialRead
            };

            if (!fromHistoryFile)
            {
                // Add to the recent history queue, which is used when querying for prediction.
                RecentHistory.Enqueue(result);
                // 'MemoryOnly' indicates sensitive content in the command line
                PreviousHistoryItem._sensitive = addToHistoryOption == AddToHistoryOption.MemoryOnly;
                PreviousHistoryItem.StartTime = DateTime.UtcNow;
            }

            Historys.Enqueue(PreviousHistoryItem);

            SearcherReadLine.ResetCurrentHistoryIndex();

            if (_rl.Options.HistorySaveStyle == HistorySaveStyle.SaveIncrementally && !fromHistoryFile)
                IncrementalHistoryWrite();
        }
        else
        {
            PreviousHistoryItem = null;
        }

        // Clear the saved line unless we used AcceptAndGetNext in which
        // case we're really still in middle of history and might want
        // to recall the saved line.
        if (GetNextHistoryIndex == 0) SearcherReadLine.ClearSavedCurrentLine();
        return result;
    }

    public bool MaybeReadHistoryFile()
    {
        if (_rl.Options.HistorySaveStyle == HistorySaveStyle.SaveIncrementally)
            return WithHistoryFileMutexDo(1000, () =>
            {
                var historyLines = ReadHistoryFileIncrementally();
                if (historyLines != null) UpdateHistoryFromFile(historyLines, true, false);
            });

        // true means no errors, not that we actually read the file
        return true;
    }


    /// <summary>
    ///     Helper method to read the incremental part of the history file.
    ///     Note: the call to this method should be guarded by the mutex that protects the history file.
    /// </summary>
    private List<string> ReadHistoryFileIncrementally()
    {
        var fileInfo = new FileInfo(_rl.Options.HistorySavePath);
        if (fileInfo.Exists && fileInfo.Length != HistoryFileLastSavedSize)
        {
            var historyLines = new List<string>();
            using (var fs = new FileStream(_rl.Options.HistorySavePath, FileMode.Open))
            using (var sr = new StreamReader(fs))
            {
                fs.Seek(HistoryFileLastSavedSize, SeekOrigin.Begin);

                while (!sr.EndOfStream) historyLines.Add(sr.ReadLine());
            }

            HistoryFileLastSavedSize = fileInfo.Length;
            return historyLines.Count > 0 ? historyLines : null;
        }

        return null;
    }

    /// <summary>
    ///     Replace the current input with the 'next' item from PSReadLine history
    ///     that matches the characters between the start and the input and the cursor.
    /// </summary>
    public static void HistorySearchForward(ConsoleKeyInfo? key = null, object arg = null)
    {
        PSConsoleReadLine.TryGetArgAsInt(arg, out var numericArg, +1);
        if (RL.UpdateListSelection(numericArg)) return;
        SearcherReadLine.SaveCurrentLine();
        Singleton.HistorySearch(numericArg);
    }

    private static void GoToEndOfHistory()
    {
        SearcherReadLine.ResetCurrentHistoryIndex();
        SearcherReadLine.UpdateBufferFromHistory(HistorySearcherReadLine.HistoryMoveCursor.ToEnd);
    }

    //class start
    /// <summary>
    ///     FNV-1a hashing algorithm: http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a
    /// </summary>
    private class FNV1a32Hash
    {
        // FNV-1a algorithm parameters: http://www.isthe.com/chongo/tech/comp/fnv/#FNV-param
        private const uint FNV32_PRIME = 16777619;
        private const uint FNV32_OFFSETBASIS = 2166136261;

        internal static uint ComputeHash(string input)
        {
            var hash = FNV32_OFFSETBASIS;

            foreach (var ch in input)
            {
                var lowByte = (uint) (ch & 0x00FF);
                hash = unchecked((hash ^ lowByte) * FNV32_PRIME);

                var highByte = (uint) (ch >> 8);
                hash = unchecked((hash ^ highByte) * FNV32_PRIME);
            }

            return hash;
        }
    }
}