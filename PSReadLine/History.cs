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
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.PSReadLine;
using Microsoft.Data.Sqlite;

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

            /// <summary>
            /// The location where the command was run, if available.
            /// </summary>
            public string Location { get; internal set; }

            /// <summary>
            /// The number of times this command has been executed (from SQLite history).
            /// Defaults to 1 for text-based or in-session history.
            /// </summary>
            public int ExecutionCount { get; internal set; } = 1;

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
        private int _locationHistoryCommandCount;
        private List<int> _locationSortedIndices;
        private int _locationSortedPosition;
        // True while location-mode (Alt+Up/Down) is "sticky" — plain Up/Down should
        // continue to navigate the same sorted list. Cleared when the user does any
        // non-history action (handled in the ReadLine main loop's anyHistory reset branch).
        private bool _locationHistoryActive;
        // True while we are showing the "[BOOK N/M]" history navigation status line.
        // Used so the main loop knows to clear the status when the user stops navigating.
        private bool _historyNavStatusActive;
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

        private const string _forwardLocationISearchPrompt = "fwd-i-search (location): ";
        private const string _backwardLocationISearchPrompt = "bck-i-search (location): ";
        private const string _failedForwardLocationISearchPrompt = "failed-fwd-i-search (location): ";
        private const string _failedBackwardLocationISearchPrompt = "failed-bck-i-search (location): ";

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

            if (Options.HistoryType is HistoryType.SQLite)
            {
                return AddToHistoryOption.SQLite; 
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

        private void InitializeSQLiteDatabase(bool migrateTextHistory = false)
        {
            string baseConnectionString = $"Data Source={_options.HistorySavePath}";
            var connectionString = new SqliteConnectionStringBuilder(baseConnectionString)
            {
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            try
            {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // Check if the "Commands" table exists (our primary table for new schema)
                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT name
FROM sqlite_master
WHERE type='table' AND name=@TableName";
                command.Parameters.AddWithValue("@TableName", "Commands");

                var result = command.ExecuteScalar();
                bool isNewDatabase = result == null;

                // If the table doesn't exist, create the normalized schema
                if (isNewDatabase)
                {
                    using var createTablesCommand = connection.CreateCommand();
                    createTablesCommand.CommandText = @"
-- Table for storing unique command lines
CREATE TABLE Commands (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CommandLine TEXT NOT NULL UNIQUE,
    CommandHash TEXT NOT NULL UNIQUE
);

-- Table for storing unique locations
CREATE TABLE Locations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Path TEXT NOT NULL UNIQUE
);

-- Table for storing execution history with foreign keys
CREATE TABLE ExecutionHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CommandId INTEGER NOT NULL,
    LocationId INTEGER NOT NULL,
    StartTime INTEGER NOT NULL,
    ElapsedTime INTEGER NOT NULL,
    ExecutionCount INTEGER DEFAULT 1,
    LastExecuted INTEGER NOT NULL,
    FOREIGN KEY (CommandId) REFERENCES Commands(Id),
    FOREIGN KEY (LocationId) REFERENCES Locations(Id),
    UNIQUE(CommandId, LocationId)
);

-- Create indexes for optimal performance
CREATE INDEX idx_commands_hash ON Commands(CommandHash);
CREATE INDEX idx_locations_path ON Locations(Path);
CREATE INDEX idx_execution_last_executed ON ExecutionHistory(LastExecuted DESC);
CREATE INDEX idx_execution_count ON ExecutionHistory(ExecutionCount DESC);
CREATE INDEX idx_execution_location_time ON ExecutionHistory(LocationId, LastExecuted DESC);

-- Create a view for easy querying (mimics the old single-table structure)
CREATE VIEW HistoryView AS
SELECT 
    eh.Id,
    c.CommandLine,
    c.CommandHash,
    l.Path as Location,
    eh.StartTime,
    eh.ElapsedTime,
    eh.ExecutionCount,
    eh.LastExecuted
FROM ExecutionHistory eh
JOIN Commands c ON eh.CommandId = c.Id
JOIN Locations l ON eh.LocationId = l.Id;";
                    createTablesCommand.ExecuteNonQuery();

                    // Only migrate text history on initial Text -> SQLite switch,
                    // not when relocating an existing SQLite database.
                    if (migrateTextHistory)
                    {
                        MigrateTextHistoryToSQLite(connection);
                    }
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"SQLite error initializing database: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing SQLite database: {ex.Message}");
            }
        }

        private void MigrateTextHistoryToSQLite(SqliteConnection connection)
        {
            // Use the dedicated text history path — no need to derive it from the SQLite path.
            string textHistoryPath = _options.HistorySavePathText;
            if (string.IsNullOrEmpty(textHistoryPath) || !File.Exists(textHistoryPath))
            {
                return; // No text history to migrate
            }

            try
            {
                // Read existing text history using the existing connection (don't open a new one)
                var historyLines = ReadHistoryLinesImpl(textHistoryPath, int.MaxValue);
                var historyItems = new List<HistoryItem>();

                // Convert text lines to HistoryItems
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
                        historyItems.Add(new HistoryItem
                        {
                            CommandLine = sb.ToString(),
                            ApproximateElapsedTime = TimeSpan.Zero,
                            Location = "Unknown"
                        });
                        sb.Clear();
                    }
                    else
                    {
                        historyItems.Add(new HistoryItem
                        {
                            CommandLine = line,
                            ApproximateElapsedTime = TimeSpan.Zero,
                            Location = "Unknown"
                        });
                    }
                }

                // Assign timestamps so that:
                // 1. All migrated items are older than any future SQLite entry
                // 2. The first text line (oldest) gets the earliest timestamp
                // 3. The last text line (newest) gets the latest migrated timestamp
                // Each item is spaced 1 minute apart, ending 2 minutes before "now".
                var migrationBase = DateTime.UtcNow.AddMinutes(-(historyItems.Count + 1));
                for (int idx = 0; idx < historyItems.Count; idx++)
                {
                    historyItems[idx].StartTime = migrationBase.AddMinutes(idx);
                }

                // Insert into SQLite database using the new normalized schema
                using var transaction = connection.BeginTransaction();

                foreach (var item in historyItems)
                {
                    try
                    {
                        // Generate command hash using SHA256
                        string commandHash = ComputeCommandHash(item.CommandLine);
                        string location = item.Location ?? "Unknown";

                        // Get or create command and location IDs
                        long commandId = GetOrCreateCommandId(connection, item.CommandLine, commandHash);
                        long locationId = GetOrCreateLocationId(connection, location);

                        // Convert DateTime to Unix timestamp (INTEGER)
                        long startTimeUnix = ((DateTimeOffset)item.StartTime).ToUnixTimeSeconds();
                        long lastExecutedUnix = startTimeUnix;

                        // Insert or update execution history using the new schema
                        using var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = @"
INSERT INTO ExecutionHistory (CommandId, LocationId, StartTime, ElapsedTime, ExecutionCount, LastExecuted)
VALUES (@CommandId, @LocationId, @StartTime, @ElapsedTime, 1, @LastExecuted)
ON CONFLICT(CommandId, LocationId) DO UPDATE SET
    ExecutionCount = ExecutionCount + 1,
    LastExecuted = excluded.LastExecuted";

                        command.Parameters.AddWithValue("@CommandId", commandId);
                        command.Parameters.AddWithValue("@LocationId", locationId);
                        command.Parameters.AddWithValue("@StartTime", startTimeUnix);
                        command.Parameters.AddWithValue("@ElapsedTime", item.ApproximateElapsedTime.Ticks);
                        command.Parameters.AddWithValue("@LastExecuted", lastExecutedUnix);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception itemEx)
                    {
                        Console.WriteLine($"Error migrating history item: {itemEx.Message}");
                        // Continue with next item
                    }
                }

                transaction.Commit();
                Console.WriteLine($"Migrated {historyItems.Count} history items from text file to SQLite");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error migrating text history: {ex.Message}");
            }
        }

        private string MaybeAddToHistory(
            string result,
            List<EditItem> edits,
            int undoEditIndex,
            string location = null,
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
                    Location = location ?? _engineIntrinsics?.SessionState?.Path?.CurrentLocation?.Path ?? "Unknown",
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

            if (_options.HistoryType == HistoryType.Text)
            {
                WriteHistoryRange(i + 1, _history.Count - 1, overwritten: false);
            }

            if (_options.HistoryType == HistoryType.SQLite)
            {
                WriteHistoryToSQLite(i + 1, _history.Count - 1);
            }
        }

        // Helper method to get or create a command ID
        private long GetOrCreateCommandId(SqliteConnection connection, string commandLine, string commandHash)
        {
            // First try to get existing command
            using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT Id FROM Commands WHERE CommandHash = @CommandHash";
            selectCommand.Parameters.AddWithValue("@CommandHash", commandHash);

            var existingId = selectCommand.ExecuteScalar();
            if (existingId != null)
            {
                return Convert.ToInt64(existingId);
            }

            // Insert new command
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
INSERT INTO Commands (CommandLine, CommandHash) 
VALUES (@CommandLine, @CommandHash)";
            insertCommand.Parameters.AddWithValue("@CommandLine", commandLine);
            insertCommand.Parameters.AddWithValue("@CommandHash", commandHash);
            insertCommand.ExecuteNonQuery();

            // Get the inserted row ID
            using var lastIdCommand = connection.CreateCommand();
            lastIdCommand.CommandText = "SELECT last_insert_rowid()";
            return Convert.ToInt64(lastIdCommand.ExecuteScalar());
        }

        // Helper method to get or create a location ID
        private long GetOrCreateLocationId(SqliteConnection connection, string location)
        {
            // First try to get existing location
            using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT Id FROM Locations WHERE Path = @Path";
            selectCommand.Parameters.AddWithValue("@Path", location);

            var existingId = selectCommand.ExecuteScalar();
            if (existingId != null)
            {
                return Convert.ToInt64(existingId);
            }

            // Insert new location
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
INSERT INTO Locations (Path) 
VALUES (@Path)";
            insertCommand.Parameters.AddWithValue("@Path", location);
            insertCommand.ExecuteNonQuery();

            // Get the inserted row ID
            using var lastIdCommand = connection.CreateCommand();
            lastIdCommand.CommandText = "SELECT last_insert_rowid()";
            return Convert.ToInt64(lastIdCommand.ExecuteScalar());
        }

        private void WriteHistoryToSQLite(int start, int end)
        {
            _historyFileMutex ??= new Mutex(false, GetHistorySaveFileMutexName());

            WithHistoryFileMutexDo(1000, () =>
            {
                try
                {
                    string baseConnectionString = $"Data Source={_options.HistorySavePath}";
                    var connectionString = new SqliteConnectionStringBuilder(baseConnectionString)
                    {
                        Mode = SqliteOpenMode.ReadWrite
                    }.ToString();

                    using var connection = new SqliteConnection(connectionString);
                    connection.Open();

                    using var transaction = connection.BeginTransaction();

                    for (var i = start; i <= end; i++)
                    {
                        var item = _history[i];
                        item._saved = true;

                        if (item._sensitive)
                        {
                            continue;
                        }

                        // Generate command hash using SHA256
                        string commandHash = ComputeCommandHash(item.CommandLine);
                        string location = item.Location ?? _engineIntrinsics?.SessionState?.Path?.CurrentLocation?.Path ?? "Unknown";

                        // Get or create command and location IDs
                        long commandId = GetOrCreateCommandId(connection, item.CommandLine, commandHash);
                        long locationId = GetOrCreateLocationId(connection, location);

                        // Convert DateTime to Unix timestamp (INTEGER)
                        long startTimeUnix = ((DateTimeOffset)item.StartTime).ToUnixTimeSeconds();
                        long lastExecutedUnix = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();

                        // Insert or update execution history
                        using var command = connection.CreateCommand();
                        command.CommandText = @"
INSERT INTO ExecutionHistory (CommandId, LocationId, StartTime, ElapsedTime, ExecutionCount, LastExecuted)
VALUES (@CommandId, @LocationId, @StartTime, @ElapsedTime, 1, @LastExecuted)
ON CONFLICT(CommandId, LocationId) DO UPDATE SET
    ExecutionCount = ExecutionCount + 1,
    LastExecuted = excluded.LastExecuted,
    ElapsedTime = excluded.ElapsedTime";

                        command.Parameters.AddWithValue("@CommandId", commandId);
                        command.Parameters.AddWithValue("@LocationId", locationId);
                        command.Parameters.AddWithValue("@StartTime", startTimeUnix);
                        command.Parameters.AddWithValue("@ElapsedTime", item.ApproximateElapsedTime.Ticks);
                        command.Parameters.AddWithValue("@LastExecuted", lastExecutedUnix);
                        command.ExecuteNonQuery();

                        // Read back the total ExecutionCount across all locations so the in-memory item stays in sync
                        using var countCmd = connection.CreateCommand();
                        countCmd.Transaction = transaction;
                        countCmd.CommandText = "SELECT SUM(ExecutionCount) FROM ExecutionHistory WHERE CommandId = @CommandId";
                        countCmd.Parameters.AddWithValue("@CommandId", commandId);
                        var count = countCmd.ExecuteScalar();
                        if (count != null)
                        {
                            item.ExecutionCount = Convert.ToInt32(count);
                        }
                    }

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    ReportHistoryFileError(e);
                }
            });
        }

        private static string ComputeCommandHash(string command)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(command));
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }

        private void SaveHistoryAtExit()
        {
            if (_options.HistoryType == HistoryType.SQLite)
            {
                WriteHistoryToSQLite(0, _history.Count - 1);
            }
            else
            {
                WriteHistoryRange(0, _history.Count - 1, overwritten: true);
            }
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
            // Read history from a text file
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

        private List<HistoryItem> ReadHistorySQLiteIncrementally()
        {
            var historyItems = new List<HistoryItem>();
            try
            {
                string baseConnectionString = $"Data Source={_options.HistorySavePath}";
                var connectionString = new SqliteConnectionStringBuilder(baseConnectionString)
                {
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    // Use the HistoryView to get all the joined data, filtering by ExecutionHistory.Id.
                    // ExecutionCount is the SUM across all locations (total runs).
                    command.CommandText = @"
SELECT hv.CommandLine, hv.StartTime, hv.ElapsedTime, hv.Location,
       (SELECT SUM(eh2.ExecutionCount) FROM ExecutionHistory eh2
        JOIN Commands c2 ON eh2.CommandId = c2.Id
        WHERE c2.CommandLine = hv.CommandLine) AS TotalExecutionCount
FROM HistoryView hv
WHERE hv.Id > @LastId
ORDER BY hv.Id ASC";
                    command.Parameters.AddWithValue("@LastId", _historyFileLastSavedSize);

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var item = new HistoryItem
                        {
                            CommandLine = reader.GetString(0),
                            StartTime = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)).DateTime,
                            ApproximateElapsedTime = TimeSpan.FromTicks(reader.GetInt64(2)),
                            Location = reader.GetString(3),
                            ExecutionCount = reader.GetInt32(4),
                            FromHistoryFile = true,
                            FromOtherSession = true,
                            _saved = true,
                            _edits = new List<EditItem> { EditItemInsertString.Create(reader.GetString(0), 0) },
                            _undoEditIndex = 1,
                            _editGroupStart = -1
                        };
                        historyItems.Add(item);
                    }
                }

                // Update the last saved size to the latest ID in the ExecutionHistory table
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT MAX(Id) FROM ExecutionHistory";
                    var result = command.ExecuteScalar();
                    if (result != DBNull.Value)
                    {
                        _historyFileLastSavedSize = Convert.ToInt64(result);
                    }
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"SQLite error reading history: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading history from SQLite: {ex.Message}");
            }

            return historyItems.Count > 0 ? historyItems : null;
        }

        private bool MaybeReadHistoryFile()
        {
            if (Options.HistorySaveStyle == HistorySaveStyle.SaveIncrementally)
            {
                return WithHistoryFileMutexDo(1000, () =>
                {
                    if (_options.HistoryType == HistoryType.SQLite)
                    {
                        List<HistoryItem> historyItems = ReadHistorySQLiteIncrementally();
                        if (historyItems != null)
                        {
                            foreach (var item in historyItems)
                            {
                                _history.Enqueue(item);
                                _currentHistoryIndex = _history.Count;
                            }
                        }
                    }
                    else
                    {
                        List<string> historyLines = ReadHistoryFileIncrementally();
                        if (historyLines != null)
                        {
                            UpdateHistoryFromFile(historyLines, fromDifferentSession: true, fromInitialRead: false);
                        }
                    }
                });
            }

            // true means no errors, not that we actually read the file
            return true;
}

        private void ReadSQLiteHistory(bool fromOtherSession)
        {
            _historyFileMutex ??= new Mutex(false, GetHistorySaveFileMutexName());

            WithHistoryFileMutexDo(1000, () =>
            {
                try
                {
                    string baseConnectionString = $"Data Source={_options.HistorySavePath}";
                    var connectionString = new SqliteConnectionStringBuilder(baseConnectionString)
                    {
                        Mode = SqliteOpenMode.ReadOnly
                    }.ToString();

                    using var connection = new SqliteConnection(connectionString);
                    connection.Open();

                    using var command = connection.CreateCommand();

                    int limit = Options.MaximumHistoryCount switch
                    {
                        <= 10000 => 10000,   // Similar to 0.5MB text optimization
                        <= 20000 => 20000,   // Similar to 1MB text optimization  
                        _ => Options.MaximumHistoryCount
                    };

                    // Load history in chronological order, deduplicated by CommandLine.
                    // When the same command exists at multiple locations, keep the most
                    // recently executed entry so that basic Up/Down recall is simple
                    // reverse-chronological navigation with no duplicates.
                    // ExecutionCount is the SUM across all locations (total runs).
                    command.CommandText = @"
WITH Ranked AS (
    SELECT CommandLine, StartTime, ElapsedTime, Location, ExecutionCount, LastExecuted,
           ROW_NUMBER() OVER (PARTITION BY CommandLine ORDER BY LastExecuted DESC) AS rn
    FROM HistoryView
),
TotalCounts AS (
    SELECT c.CommandLine, SUM(eh.ExecutionCount) AS TotalExecutionCount
    FROM ExecutionHistory eh
    JOIN Commands c ON eh.CommandId = c.Id
    GROUP BY c.CommandLine
)
SELECT r.CommandLine, r.StartTime, r.ElapsedTime, r.Location, tc.TotalExecutionCount
FROM Ranked r
JOIN TotalCounts tc ON r.CommandLine = tc.CommandLine
WHERE r.rn = 1
ORDER BY r.LastExecuted DESC
LIMIT @Limit";
                    command.Parameters.AddWithValue("@Limit", limit);

                    var historyItems = new List<HistoryItem>();
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var item = new HistoryItem
                        {
                            CommandLine = reader.GetString(0),
                            StartTime = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)).DateTime,
                            ApproximateElapsedTime = TimeSpan.FromTicks(reader.GetInt64(2)),
                            Location = reader.GetString(3),
                            ExecutionCount = reader.GetInt32(4),
                            FromHistoryFile = true,
                            FromOtherSession = fromOtherSession,
                            _saved = true,
                            _edits = new List<EditItem> { EditItemInsertString.Create(reader.GetString(0), 0) },
                            _undoEditIndex = 1,
                            _editGroupStart = -1
                        };

                        historyItems.Add(item);
                    }

                    historyItems.Reverse();

                    foreach (var item in historyItems)
                    {
                        _history.Enqueue(item);
                    }

                    // Update the last saved size to the latest ID in the database
                    using var idCommand = connection.CreateCommand();
                    idCommand.CommandText = "SELECT MAX(Id) FROM ExecutionHistory";
                    var result = idCommand.ExecuteScalar();
                    if (result != DBNull.Value)
                    {
                        _historyFileLastSavedSize = Convert.ToInt64(result);
                    }
                }
                catch (SqliteException ex)
                {
                    ReportHistoryFileError(ex);
                }
                catch (Exception ex)
                {
                    ReportHistoryFileError(ex);
                }
            });
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
        }

        private IEnumerable<string> ReadHistoryLinesImpl(string path, int historyCount)
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
                    MaybeAddToHistory(l, editItems, 1, null, fromDifferentSession, fromInitialRead);
                    sb.Clear();
                }
                else
                {
                    var editItems = new List<EditItem> {EditItemInsertString.Create(line, 0)};
                    MaybeAddToHistory(line, editItems, 1, null, fromDifferentSession, fromInitialRead);
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
        /// Add a command to the history with a specified location.
        /// </summary>
        internal static void AddToHistory(string command, string location)
        {
            command = command.Replace("\r\n", "\n");
            var editItems = new List<EditItem> {EditItemInsertString.Create(command, 0)};
            _singleton.MaybeAddToHistory(command, editItems, 1, location: location);
        }

        /// <summary>
        /// Remove a specific command from history (both in-memory and SQLite if applicable).
        /// Returns true if any items were removed.
        /// </summary>
        public static bool RemoveHistoryItem(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine))
                return false;

            bool removed = false;

            // Remove from in-memory history
            var history = _singleton._history;
            if (history != null)
            {
                var itemsToKeep = new List<HistoryItem>();
                for (int i = 0; i < history.Count; i++)
                {
                    if (!string.Equals(history[i].CommandLine, commandLine, StringComparison.Ordinal))
                    {
                        itemsToKeep.Add(history[i]);
                    }
                    else
                    {
                        removed = true;
                    }
                }

                if (removed)
                {
                    history.Clear();
                    foreach (var item in itemsToKeep)
                    {
                        history.Enqueue(item);
                    }
                    _singleton._currentHistoryIndex = history.Count;
                }
            }

            // Remove from SQLite database if using SQLite history
            if (_singleton._options?.HistoryType == HistoryType.SQLite &&
                !string.IsNullOrEmpty(_singleton._options.HistorySavePath))
            {
                removed |= _singleton.RemoveFromSQLiteHistory(commandLine);
            }

            return removed;
        }

        private bool RemoveFromSQLiteHistory(string commandLine)
        {
            try
            {
                string baseConnectionString = $"Data Source={_options.HistorySavePath}";
                var connectionString = new SqliteConnectionStringBuilder(baseConnectionString)
                {
                    Mode = SqliteOpenMode.ReadWrite
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // Find the command ID
                using var findCmd = connection.CreateCommand();
                findCmd.CommandText = "SELECT Id FROM Commands WHERE CommandLine = @CommandLine";
                findCmd.Parameters.AddWithValue("@CommandLine", commandLine);
                var commandIdObj = findCmd.ExecuteScalar();

                if (commandIdObj == null)
                    return false;

                long commandId = Convert.ToInt64(commandIdObj);

                using var transaction = connection.BeginTransaction();

                // Delete execution history entries first (foreign key constraint)
                using var deleteEH = connection.CreateCommand();
                deleteEH.CommandText = "DELETE FROM ExecutionHistory WHERE CommandId = @CommandId";
                deleteEH.Parameters.AddWithValue("@CommandId", commandId);
                deleteEH.ExecuteNonQuery();

                // Delete the command itself
                using var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM Commands WHERE Id = @CommandId";
                deleteCmd.Parameters.AddWithValue("@CommandId", commandId);
                int deletedRows = deleteCmd.ExecuteNonQuery();

                transaction.Commit();
                return deletedRows > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Remove a specific command from history scoped to a single location (both in-memory and SQLite if applicable).
        /// In-memory items are removed only when both <c>CommandLine</c> and <c>Location</c> match; in SQLite the
        /// <c>ExecutionHistory</c> row for the matching <c>(Command, Location)</c> pair is deleted, and the
        /// <c>Commands</c> row is dropped only when no other location still references it.
        /// Returns true if any items were removed.
        /// </summary>
        public static bool RemoveHistoryItemAtLocation(string commandLine, string location)
        {
            if (string.IsNullOrEmpty(commandLine) || string.IsNullOrEmpty(location))
                return false;

            bool removed = false;

            // Remove from in-memory history — only items whose Location matches.
            var history = _singleton._history;
            if (history != null)
            {
                var itemsToKeep = new List<HistoryItem>();
                for (int i = 0; i < history.Count; i++)
                {
                    var item = history[i];
                    if (string.Equals(item.CommandLine, commandLine, StringComparison.Ordinal) &&
                        string.Equals(item.Location, location, StringComparison.Ordinal))
                    {
                        removed = true;
                    }
                    else
                    {
                        itemsToKeep.Add(item);
                    }
                }

                if (removed)
                {
                    history.Clear();
                    foreach (var item in itemsToKeep)
                    {
                        history.Enqueue(item);
                    }
                    _singleton._currentHistoryIndex = history.Count;
                }
            }

            // Remove from SQLite database if using SQLite history
            if (_singleton._options?.HistoryType == HistoryType.SQLite &&
                !string.IsNullOrEmpty(_singleton._options.HistorySavePath))
            {
                removed |= _singleton.RemoveFromSQLiteHistoryAtLocation(commandLine, location);
            }

            return removed;
        }

        private bool RemoveFromSQLiteHistoryAtLocation(string commandLine, string location)
        {
            try
            {
                string baseConnectionString = $"Data Source={_options.HistorySavePath}";
                var connectionString = new SqliteConnectionStringBuilder(baseConnectionString)
                {
                    Mode = SqliteOpenMode.ReadWrite
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // Find the command ID
                using var findCmd = connection.CreateCommand();
                findCmd.CommandText = "SELECT Id FROM Commands WHERE CommandLine = @CommandLine";
                findCmd.Parameters.AddWithValue("@CommandLine", commandLine);
                var commandIdObj = findCmd.ExecuteScalar();
                if (commandIdObj == null)
                    return false;
                long commandId = Convert.ToInt64(commandIdObj);

                // Find the location ID. If the location doesn't exist in the DB, there's nothing to delete.
                using var findLoc = connection.CreateCommand();
                findLoc.CommandText = "SELECT Id FROM Locations WHERE Path = @Path";
                findLoc.Parameters.AddWithValue("@Path", location);
                var locationIdObj = findLoc.ExecuteScalar();
                if (locationIdObj == null)
                    return false;
                long locationId = Convert.ToInt64(locationIdObj);

                using var transaction = connection.BeginTransaction();

                // Delete the ExecutionHistory row for this (Command, Location) only.
                using var deleteEH = connection.CreateCommand();
                deleteEH.CommandText = "DELETE FROM ExecutionHistory WHERE CommandId = @CommandId AND LocationId = @LocationId";
                deleteEH.Parameters.AddWithValue("@CommandId", commandId);
                deleteEH.Parameters.AddWithValue("@LocationId", locationId);
                int deletedRows = deleteEH.ExecuteNonQuery();

                if (deletedRows == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                // If no other location still references this command, drop the Commands row too
                // so it stops appearing in global queries.
                using var orphanCheck = connection.CreateCommand();
                orphanCheck.CommandText = "SELECT COUNT(*) FROM ExecutionHistory WHERE CommandId = @CommandId";
                orphanCheck.Parameters.AddWithValue("@CommandId", commandId);
                long remaining = Convert.ToInt64(orphanCheck.ExecuteScalar());
                if (remaining == 0)
                {
                    using var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM Commands WHERE Id = @CommandId";
                    deleteCmd.Parameters.AddWithValue("@CommandId", commandId);
                    deleteCmd.ExecuteNonQuery();
                }

                transaction.Commit();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Query per-location execution counts for all commands at the given location.
        /// Returns a dictionary mapping CommandLine -> ExecutionCount for that location.
        /// </summary>
        private Dictionary<string, long> GetLocationExecutionCounts(string location)
        {
            var counts = new Dictionary<string, long>(StringComparer.Ordinal);
            try
            {
                string baseConnectionString = $"Data Source={_options.HistorySavePath}";
                var connectionString = new SqliteConnectionStringBuilder(baseConnectionString)
                {
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT c.CommandLine, eh.ExecutionCount
                    FROM ExecutionHistory eh
                    JOIN Commands c ON eh.CommandId = c.Id
                    JOIN Locations l ON eh.LocationId = l.Id
                    WHERE l.Path = @Location";
                cmd.Parameters.AddWithValue("@Location", location);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    counts[reader.GetString(0)] = reader.GetInt64(1);
                }
            }
            catch (Exception)
            {
                // On failure, return empty — caller falls back to total ExecutionCount
            }
            return counts;
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

            if (Options.HistoryNoDuplicates && _hashedHistory == null)
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

            // Show position indicator while navigating chronological history.
            // Position 1 = newest navigable item; total = number of items reachable
            // by Up/Down (i.e., excluding FromOtherSession entries, which HistoryRecall
            // skips above). Counting raw _history slots would make the indicator jump
            // by hundreds when many cross-session items sit between two in-session
            // commands.
            if (_history.Count > 0 && _currentHistoryIndex < _history.Count)
            {
                int navigableTotal = 0;
                int navigableFromOldest = 0;
                for (int i = 0; i < _history.Count; i++)
                {
                    if (_history[i].FromOtherSession)
                    {
                        continue;
                    }
                    navigableTotal++;
                    if (i <= _currentHistoryIndex)
                    {
                        navigableFromOldest++;
                    }
                }

                if (navigableTotal > 0 && navigableFromOldest > 0)
                {
                    int positionFromNewest = navigableTotal - navigableFromOldest + 1;
                    ShowHistoryNavStatus(positionFromNewest, navigableTotal, locationMode: false);
                }
            }
        }

        // Renders a small "[<emoji> pos/total]" status line below the prompt while
        // the user is navigating history. Cleared by the ReadLine main loop once
        // any non-history key is pressed.
        private void ShowHistoryNavStatus(int position, int total, bool locationMode)
        {
            if (total <= 0)
            {
                return;
            }

            // ⏱ (U+23F1, BMP, 1 char / 1 cell) for chronological recency-based recall,
            // 📂 (U+1F4C2, surrogate pair = 2 chars / 2 cells) for location-filtered
            // recall. Both fit the buffer-width math in Render.cs.
            // Brackets stay in the status line's default color; inner text uses the
            // ListPredictionColor (gold/yellow by default — matches F2 list metadata).
            var color = _options?._listPredictionColor ?? "\x1b[33m";
            var innerText = locationMode
                ? $"\uD83D\uDCC2 {position}/{total}"
                : $"\u23F1 {position}/{total}";
            _statusLinePrompt = $"[{color}{innerText}\x1b[0m]";
            _statusBuffer.Clear();
            _statusIsErrorMessage = false;
            _historyNavStatusActive = true;
            RenderWithPredictionQueryPaused();
        }

        /// <summary>
        /// Remove the currently displayed history item from history (both in-memory and SQLite if applicable).
        /// Works when browsing history with Up/Down arrows or when an item is selected in the F2 list view.
        /// </summary>
        public static void RemoveFromHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            var history = _singleton._history;
            if (history == null || history.Count == 0)
            {
                Ding();
                return;
            }

            // Check if we're in the F2 list prediction view with a selected item.
            // Handle this path separately — no history recall counters needed since
            // we stay in the list view, and incrementing them would leave _hashedHistory
            // null for the next HistoryRecall call (causing NullReferenceException).
            if (_singleton._prediction.ActiveView is PredictionListView listView
                && listView.HasActiveSuggestion
                && listView.SelectedItemIndex >= 0)
            {
                string commandToRemove = listView.SelectedItemText;
                if (commandToRemove == null)
                {
                    Ding();
                    return;
                }

                RemoveHistoryItem(commandToRemove);

                if (!listView.RemoveSelectedItem())
                {
                    // List became empty — close the list view
                    RevertLine();
                }
                else
                {
                    // Re-render so the user sees the item disappear immediately.
                    ReplaceSelection(listView.SelectedItemText);
                }

                return;
            }

            // Normal history browsing path (Up/Down arrows).
            // Signal to the main ReadLine loop that this is a history command,
            // so it doesn't reset _currentHistoryIndex after we set it.
            _singleton._recallHistoryCommandCount += 1;
            _singleton._anyHistoryCommandCount += 1;

            string commandLine = null;
            if (_singleton._currentHistoryIndex < history.Count)
            {
                commandLine = history[_singleton._currentHistoryIndex].CommandLine;
            }

            if (commandLine == null)
            {
                Ding();
                return;
            }

            // Save position before RemoveHistoryItem resets _currentHistoryIndex to Count
            int savedIndex = _singleton._currentHistoryIndex;

            RemoveHistoryItem(commandLine);

            // In normal history browsing: advance to the next older item
            // (same direction as Up arrow) so the user can keep deleting
            // consecutive items without bouncing back to the top.
            if (history.Count == 0)
            {
                _singleton._currentHistoryIndex = 0;
                RevertLine();
            }
            else
            {
                // Items below savedIndex didn't move, so the next older item
                // is at savedIndex - 1. If we were at the oldest item already,
                // show whatever is now at index 0 (the former next-newer item).
                _singleton._currentHistoryIndex = Math.Max(savedIndex - 1, 0);
                _singleton.UpdateFromHistory(HistoryMoveCursor.ToEnd);
            }
        }

        /// <summary>
        /// Remove the currently displayed history item from history at the *current location only*.
        /// In SQLite mode this deletes only the <c>ExecutionHistory</c> row for the current directory
        /// (and the <c>Commands</c> row only if no other location still references it). In-memory items
        /// run at other locations are preserved. In Text mode this falls back to a global removal because
        /// per-location data isn't tracked.
        /// </summary>
        public static void RemoveFromHistoryAtCurrentLocation(ConsoleKeyInfo? key = null, object arg = null)
        {
            var history = _singleton._history;
            if (history == null || history.Count == 0)
            {
                Ding();
                return;
            }

            string currentLocation = _singleton.GetCurrentLocation();

            // Text mode (or no current location available): fall back to the global removal so the user
            // still gets a useful action. Per-location semantics require SQLite.
            if (string.IsNullOrEmpty(currentLocation) ||
                _singleton._options?.HistoryType != HistoryType.SQLite)
            {
                RemoveFromHistory(key, arg);
                return;
            }

            // F2 list view path — same handling as RemoveFromHistory but scoped to current location.
            if (_singleton._prediction.ActiveView is PredictionListView listView
                && listView.HasActiveSuggestion
                && listView.SelectedItemIndex >= 0)
            {
                string commandToRemove = listView.SelectedItemText;
                if (commandToRemove == null)
                {
                    Ding();
                    return;
                }

                if (!RemoveHistoryItemAtLocation(commandToRemove, currentLocation))
                {
                    // Nothing was removed (item isn't recorded at this location). Don't disturb the list.
                    Ding();
                    return;
                }

                if (!listView.RemoveSelectedItem())
                {
                    RevertLine();
                }
                else
                {
                    ReplaceSelection(listView.SelectedItemText);
                }

                return;
            }

            // Normal history browsing path. See RemoveFromHistory for the counter-increment rationale.
            _singleton._recallHistoryCommandCount += 1;
            _singleton._anyHistoryCommandCount += 1;

            string commandLine = null;
            if (_singleton._currentHistoryIndex < history.Count)
            {
                commandLine = history[_singleton._currentHistoryIndex].CommandLine;
            }

            if (commandLine == null)
            {
                Ding();
                return;
            }

            int savedIndex = _singleton._currentHistoryIndex;
            bool wasInLocationMode = _singleton._locationHistoryActive;
            int savedLocationPos = _singleton._locationSortedPosition;

            if (!RemoveHistoryItemAtLocation(commandLine, currentLocation))
            {
                // The displayed item wasn't run at this location, so location-scoped delete is a no-op.
                // Ding to signal "nothing happened" without falling through to a destructive global delete.
                Ding();
                return;
            }

            if (history.Count == 0)
            {
                _singleton._currentHistoryIndex = 0;
                _singleton._locationSortedIndices = null;
                _singleton._locationSortedPosition = -1;
                RevertLine();
                return;
            }

            if (wasInLocationMode)
            {
                // RemoveHistoryItemAtLocation rebuilt _history (Clear + Enqueue), so every
                // index in _locationSortedIndices is now stale. Rebuild the sorted list against
                // the new _history and reposition to the next item in the location list (clamped).
                // Bump _locationHistoryCommandCount so the main loop's sticky-mode teardown
                // doesn't fire on the next key press.
                _singleton._locationHistoryCommandCount += 1;
                _singleton._locationSortedIndices = null;
                _singleton.BuildLocationSortedIndices(currentLocation);

                if (_singleton._locationSortedIndices.Count == 0)
                {
                    // No more items at this location — exit location mode and clear the line.
                    _singleton._locationSortedPosition = -1;
                    _singleton._locationHistoryActive = false;
                    _singleton._currentHistoryIndex = history.Count;
                    RevertLine();
                    _singleton.ClearStatusMessage(render: true);
                    return;
                }

                // The item at savedLocationPos was just removed; whatever was at savedLocationPos+1
                // now sits at savedLocationPos. Stay on that slot, clamped to the new end.
                int newPos = Math.Min(Math.Max(savedLocationPos, 0), _singleton._locationSortedIndices.Count - 1);
                _singleton._locationSortedPosition = newPos;
                _singleton._currentHistoryIndex = _singleton._locationSortedIndices[newPos];
                _singleton.UpdateFromHistory(HistoryMoveCursor.ToEnd);
                _singleton.ShowHistoryNavStatus(newPos + 1, _singleton._locationSortedIndices.Count, locationMode: true);
                return;
            }

            _singleton._currentHistoryIndex = Math.Max(savedIndex - 1, 0);
            _singleton.UpdateFromHistory(HistoryMoveCursor.ToEnd);
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
            // Sticky location mode: if the user entered location-filtered navigation
            // (Alt+Up), keep filtering by location even when they release Alt and
            // press plain Up/Down. They exit by editing or doing any non-history op.
            if (_singleton._locationHistoryActive)
            {
                _singleton.LocationHistoryRecall(numericArg);
            }
            else
            {
                _singleton.HistoryRecall(numericArg);
            }
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
            if (_singleton._locationHistoryActive)
            {
                _singleton.LocationHistoryRecall(numericArg);
            }
            else
            {
                _singleton.HistoryRecall(numericArg);
            }
        }

        /// <summary>
        /// Replace the current input with the 'previous' item from PSReadLine history
        /// that was executed from the same location (directory).
        /// </summary>
        public static void PreviousLocationHistory(ConsoleKeyInfo? key = null, object arg = null)
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
            _singleton.LocationHistoryRecall(numericArg);
        }

        /// <summary>
        /// Replace the current input with the 'next' item from PSReadLine history
        /// that was executed from the same location (directory).
        /// </summary>
        public static void NextLocationHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, +1);
            if (UpdateListSelection(numericArg))
            {
                return;
            }

            _singleton.SaveCurrentLine();
            _singleton.LocationHistoryRecall(numericArg);
        }

        private string GetCurrentLocation()
        {
            return _testCurrentLocation ?? _engineIntrinsics?.SessionState?.Path?.CurrentLocation?.Path;
        }

        // For unit testing: allows tests to simulate a current directory
        internal static string _testCurrentLocation;

        private void LocationHistoryRecall(int direction)
        {
            if (_locationHistoryCommandCount == 0 && !_locationHistoryActive && LineIsMultiLine())
            {
                MoveToLine(direction);
                return;
            }

            var currentLocation = GetCurrentLocation();
            if (string.IsNullOrEmpty(currentLocation))
            {
                // Fall back to normal recall if we can't determine location
                HistoryRecall(direction);
                return;
            }

            // First entry into location mode (or after sorted list was cleared by exit):
            // build a weighted index of location-matching history items.
            // Ordering: location match (primary), then frequency DESC, then recency DESC.
            if (_locationSortedIndices == null)
            {
                BuildLocationSortedIndices(currentLocation);
                _locationSortedPosition = -1;
            }

            _locationHistoryActive = true;
            _locationHistoryCommandCount += 1;

            // Navigate: Alt+Up (direction < 0) advances forward through sorted list,
            // Alt+Down (direction > 0) goes back.
            int count = Math.Abs(direction);
            int step = direction < 0 ? 1 : -1;
            int newPosition = _locationSortedPosition;

            while (count > 0)
            {
                newPosition += step;
                if (newPosition < 0 || newPosition >= _locationSortedIndices.Count)
                {
                    break;
                }
                --count;
            }

            if (newPosition >= 0 && newPosition < _locationSortedIndices.Count)
            {
                _locationSortedPosition = newPosition;
                _currentHistoryIndex = _locationSortedIndices[newPosition];
                var moveCursor = InViCommandMode() && !_options.HistorySearchCursorMovesToEnd
                    ? HistoryMoveCursor.ToBeginning
                    : HistoryMoveCursor.ToEnd;
                UpdateFromHistory(moveCursor);
            }

            // Show position indicator: [BOOK pos/total] (location-filtered).
            ShowHistoryNavStatus(_locationSortedPosition + 1, _locationSortedIndices.Count, locationMode: true);
        }

        // Builds _locationSortedIndices for the given location, applying the same
        // dedup + frecency sort used by LocationHistoryRecall. Caller is responsible
        // for resetting _locationSortedPosition.
        private void BuildLocationSortedIndices(string currentLocation)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            _locationSortedIndices = new List<int>();

            for (int i = 0; i < _history.Count; i++)
            {
                if (string.Equals(_history[i].Location, currentLocation, StringComparison.OrdinalIgnoreCase))
                {
                    if (seen.Add(_history[i].CommandLine))
                    {
                        _locationSortedIndices.Add(i);
                    }
                }
            }

            // In SQLite mode, query per-location execution counts so that sorting
            // reflects how often each command was run *in this directory* rather than
            // the total across all locations (which inflates commands like "code ."
            // that were run once here but many times elsewhere).
            Dictionary<string, long> localCounts = null;
            if (_options.HistoryType == HistoryType.SQLite && !string.IsNullOrEmpty(_options.HistorySavePath))
            {
                localCounts = GetLocationExecutionCounts(currentLocation);
            }

            // Sort by per-location frequency DESC (SQLite) or total frequency DESC (Text),
            // then by position DESC (more recent first).
            _locationSortedIndices.Sort((a, b) =>
            {
                long countA, countB;
                if (localCounts != null)
                {
                    localCounts.TryGetValue(_history[a].CommandLine, out countA);
                    localCounts.TryGetValue(_history[b].CommandLine, out countB);
                }
                else
                {
                    countA = _history[a].ExecutionCount;
                    countB = _history[b].ExecutionCount;
                }
                int freqCmp = countB.CompareTo(countA);
                if (freqCmp != 0) return freqCmp;
                return b.CompareTo(a);
            });
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
                var key = ReadKey();
                _dispatchTable.TryGetValue(key, out var handler);
                var function = handler?.Action;
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

        private void UpdateLocationHistoryDuringInteractiveSearch(string toMatch, int direction, string currentLocation, ref int searchFromPoint)
        {
            searchFromPoint += direction;
            for (; searchFromPoint >= 0 && searchFromPoint < _history.Count; searchFromPoint += direction)
            {
                // Filter by location
                if (!string.Equals(_history[searchFromPoint].Location, currentLocation, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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
                    _statusLinePrompt = direction > 0 ? _forwardLocationISearchPrompt : _backwardLocationISearchPrompt;
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

            if (searchFromPoint < 0)
                searchFromPoint = -1;
            else if (searchFromPoint >= _history.Count)
                searchFromPoint = _history.Count;

            _emphasisStart = -1;
            _emphasisLength = 0;
            _statusLinePrompt = direction > 0 ? _failedForwardLocationISearchPrompt : _failedBackwardLocationISearchPrompt;
            Render();
        }

        private void InteractiveLocationHistorySearchLoop(int direction, string currentLocation)
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
                var key = ReadKey();
                _dispatchTable.TryGetValue(key, out var handler);
                var function = handler?.Action;
                if (function == ReverseLocationSearchHistory)
                {
                    UpdateLocationHistoryDuringInteractiveSearch(toMatch.ToString(), -1, currentLocation, ref searchFromPoint);
                }
                else if (function == ForwardLocationSearchHistory)
                {
                    UpdateLocationHistoryDuringInteractiveSearch(toMatch.ToString(), +1, currentLocation, ref searchFromPoint);
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
                            foreach (var pair in _hashedHistory.ToArray())
                            {
                                if (pair.Value < searchFromPoint)
                                {
                                    _hashedHistory.Remove(pair.Key);
                                }
                            }
                        }

                        var toMatchStr = toMatch.ToString();
                        var startIndex = _buffer.ToString().IndexOf(toMatchStr, Options.HistoryStringComparison);
                        if (startIndex >= 0)
                        {
                            _statusLinePrompt = direction > 0 ? _forwardLocationISearchPrompt : _backwardLocationISearchPrompt;
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
                    break;
                }
                else if (function == Abort)
                {
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
                        UpdateLocationHistoryDuringInteractiveSearch(toMatchStr, direction, currentLocation, ref searchFromPoint);
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

        private void InteractiveLocationHistorySearch(int direction)
        {
            var currentLocation = GetCurrentLocation();
            if (string.IsNullOrEmpty(currentLocation))
            {
                // Fall back to regular interactive search if location unavailable
                InteractiveHistorySearch(direction);
                return;
            }

            using var _ = _prediction.DisableScoped();
            SaveCurrentLine();

            _statusLinePrompt = direction > 0 ? _forwardLocationISearchPrompt : _backwardLocationISearchPrompt;
            _statusBuffer.Append("_");

            Render();
            InteractiveLocationHistorySearchLoop(direction, currentLocation);

            _emphasisStart = -1;
            _emphasisLength = 0;

            ClearStatusMessage(render: true);
        }

        /// <summary>
        /// Perform an incremental forward search through history, filtered to commands executed from the current location.
        /// </summary>
        public static void ForwardLocationSearchHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.InteractiveLocationHistorySearch(+1);
        }

        /// <summary>
        /// Perform an incremental backward search through history, filtered to commands executed from the current location.
        /// </summary>
        public static void ReverseLocationSearchHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.InteractiveLocationHistorySearch(-1);
        }
    }
}
