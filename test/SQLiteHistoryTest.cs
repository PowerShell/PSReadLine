using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        /// <summary>
        /// Helper to configure SQLite history mode for tests.
        /// Sets up a temp .db file, switches HistoryType to SQLite with SaveIncrementally,
        /// and returns a disposable that restores original settings and cleans up.
        /// </summary>
        private SQLiteTestContext SetupSQLiteHistory()
        {
            var options = PSConsoleReadLine.GetOptions();
            var ctx = new SQLiteTestContext
            {
                OriginalHistorySavePathText = options.HistorySavePathText,
                OriginalHistorySavePathSQLite = options.HistorySavePathSQLite,
                OriginalHistorySaveStyle = options.HistorySaveStyle,
                OriginalHistoryType = options.HistoryType,
                TempDbPath = Path.Combine(Path.GetTempPath(), $"PSReadLineTest_{Guid.NewGuid():N}.db"),
            };

            // Point HistorySavePathSQLite to our temp DB before switching mode,
            // because SetOptionsInternal reads HistorySavePathSQLite when HistoryType is set to SQLite.
            options.HistorySavePathSQLite = ctx.TempDbPath;

            // Point HistorySavePathText to a non-existent file so migration doesn't
            // accidentally pull from the real production text history.
            options.HistorySavePathText = Path.ChangeExtension(ctx.TempDbPath, ".txt");

            // Switch to SQLite mode — this creates the DB and clears in-memory history.
            var setOptions = new SetPSReadLineOption
            {
                HistoryType = HistoryType.SQLite,
                HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
            };
            PSConsoleReadLine.SetOptions(setOptions);

            return ctx;
        }

        /// <summary>
        /// Holds the original settings so they can be restored after a SQLite test.
        /// </summary>
        private class SQLiteTestContext : IDisposable
        {
            public string OriginalHistorySavePathText;
            public string OriginalHistorySavePathSQLite;
            public HistorySaveStyle OriginalHistorySaveStyle;
            public HistoryType OriginalHistoryType;
            public string TempDbPath;

            public void Dispose()
            {
                var options = PSConsoleReadLine.GetOptions();

                // Restore original settings
                options.HistorySavePathSQLite = OriginalHistorySavePathSQLite;
                options.HistorySavePathText = OriginalHistorySavePathText;
                options.HistorySaveStyle = OriginalHistorySaveStyle;
                options.HistoryType = OriginalHistoryType;

                PSConsoleReadLine.ClearHistory();

                // Clean up temp DB file
                try { if (File.Exists(TempDbPath)) File.Delete(TempDbPath); }
                catch { /* best effort */ }
            }
        }

        /// <summary>
        /// Helper to count rows in a SQLite table.
        /// </summary>
        private long CountSQLiteRows(string dbPath, string tableName)
        {
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={dbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
            return (long)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Helper to query command lines from the SQLite database.
        /// </summary>
        private string[] QuerySQLiteCommandLines(string dbPath)
        {
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={dbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT CommandLine FROM HistoryView ORDER BY LastExecuted ASC";
            using var reader = cmd.ExecuteReader();
            var results = new System.Collections.Generic.List<string>();
            while (reader.Read())
            {
                results.Add(reader.GetString(0));
            }
            return results.ToArray();
        }

        /// <summary>
        /// Helper to query command lines with timestamps and execution counts from the SQLite database.
        /// </summary>
        private (string CommandLine, long LastExecuted, long ExecutionCount)[] QuerySQLiteHistoryWithTimestamps(string dbPath)
        {
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={dbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT CommandLine, LastExecuted, ExecutionCount FROM HistoryView ORDER BY LastExecuted ASC";
            using var reader = cmd.ExecuteReader();
            var results = new System.Collections.Generic.List<(string CommandLine, long LastExecuted, long ExecutionCount)>();
            while (reader.Read())
            {
                results.Add((reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2)));
            }
            return results.ToArray();
        }

        // =====================================================================
        // SQLite Database Persistence Tests
        // =====================================================================

        [SkippableFact]
        public void SQLiteHistory_WritesToDatabase()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // Run commands that get saved to history
            Test("echo hello", Keys("echo hello"));
            Test("dir", Keys("dir"));
            Test("ps", Keys("ps"));

            // Verify the database has the expected commands
            var commands = QuerySQLiteCommandLines(ctx.TempDbPath);
            Assert.Contains("echo hello", commands);
            Assert.Contains("dir", commands);
            Assert.Contains("ps", commands);
        }

        [SkippableFact]
        public void SQLiteHistory_DatabaseSchemaCreated()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // Verify the database file was created
            Assert.True(File.Exists(ctx.TempDbPath), "SQLite database file should exist");

            // Verify the schema by checking tables exist
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={ctx.TempDbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // Check all three tables exist
            foreach (var table in new[] { "Commands", "Locations", "ExecutionHistory" })
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@Name";
                cmd.Parameters.AddWithValue("@Name", table);
                Assert.NotNull(cmd.ExecuteScalar());
            }

            // Check the view exists
            using var viewCmd = connection.CreateCommand();
            viewCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='view' AND name='HistoryView'";
            Assert.NotNull(viewCmd.ExecuteScalar());
        }

        [SkippableFact]
        public void SQLiteHistory_DeduplicatesCommands()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // Need to disable in-memory no-duplicates to ensure both reach SQLite
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption { HistoryNoDuplicates = false });

            // Run the same command multiple times
            Test("echo hello", Keys("echo hello"));
            Test("echo hello", Keys("echo hello"));
            Test("echo hello", Keys("echo hello"));

            // The Commands table should only have one row for "echo hello"
            long commandCount = CountSQLiteRows(ctx.TempDbPath, "Commands");
            Assert.Equal(1, commandCount);

            // But ExecutionHistory should track the execution count
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={ctx.TempDbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ExecutionCount FROM ExecutionHistory";
            var executionCount = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.True(executionCount >= 3, $"ExecutionCount should be >= 3, was {executionCount}");
        }

        [SkippableFact]
        public void SQLiteHistory_StoresLocation()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // Run a command - location defaults to "Unknown" in test harness
            // because _engineIntrinsics is null
            Test("echo hello", Keys("echo hello"));

            // Verify location was stored
            long locationCount = CountSQLiteRows(ctx.TempDbPath, "Locations");
            Assert.True(locationCount >= 1, "Should have at least one location");
        }

        // =====================================================================
        // SQLite Migration Tests
        // =====================================================================

        [SkippableFact]
        public void SQLiteHistory_MigratesFromTextFile()
        {
            TestSetup(KeyMode.Cmd);

            var options = PSConsoleReadLine.GetOptions();
            var originalHistorySavePathText = options.HistorySavePathText;
            var originalHistorySavePathSQLite = options.HistorySavePathSQLite;
            var originalHistorySaveStyle = options.HistorySaveStyle;
            var originalHistoryType = options.HistoryType;

            var tempDbPath = Path.Combine(Path.GetTempPath(), $"PSReadLineTest_{Guid.NewGuid():N}.db");
            var tempTxtPath = Path.ChangeExtension(tempDbPath, ".txt");

            try
            {
                // Create a text history file with known content
                File.WriteAllLines(tempTxtPath, new[]
                {
                    "get-process",
                    "cd /tmp",
                    "echo hello"
                });

                // Point both paths so migration can find the text file
                options.HistorySavePathText = tempTxtPath;
                options.HistorySavePathSQLite = tempDbPath;

                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };
                PSConsoleReadLine.SetOptions(setOptions);

                // The migration should have imported the text history
                long commandCount = CountSQLiteRows(tempDbPath, "Commands");
                Assert.Equal(3, commandCount);

                // Verify the specific commands were migrated
                var commands = QuerySQLiteCommandLines(tempDbPath);
                Assert.Contains("get-process", commands);
                Assert.Contains("cd /tmp", commands);
                Assert.Contains("echo hello", commands);
            }
            finally
            {
                // Restore original settings
                options.HistorySavePathSQLite = originalHistorySavePathSQLite;
                options.HistorySavePathText = originalHistorySavePathText;
                options.HistorySaveStyle = originalHistorySaveStyle;
                options.HistoryType = originalHistoryType;
                PSConsoleReadLine.ClearHistory();

                try { if (File.Exists(tempDbPath)) File.Delete(tempDbPath); } catch { }
                try { if (File.Exists(tempTxtPath)) File.Delete(tempTxtPath); } catch { }
            }
        }

        [SkippableFact]
        public void SQLiteHistory_MigrationSkippedWhenNoTextFile()
        {
            TestSetup(KeyMode.Cmd);

            var options = PSConsoleReadLine.GetOptions();
            var originalHistorySavePathText = options.HistorySavePathText;
            var originalHistorySavePathSQLite = options.HistorySavePathSQLite;
            var originalHistorySaveStyle = options.HistorySaveStyle;
            var originalHistoryType = options.HistoryType;

            // Use a temp path where no .txt file exists
            var tempDbPath = Path.Combine(Path.GetTempPath(), $"PSReadLineTest_{Guid.NewGuid():N}.db");

            try
            {
                options.HistorySavePathSQLite = tempDbPath;
                // Point text path to a non-existent file so migration won't import from the real history.
                options.HistorySavePathText = Path.ChangeExtension(tempDbPath, ".txt");

                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };

                // Should not throw even without a text file
                Exception ex = Record.Exception(() => PSConsoleReadLine.SetOptions(setOptions));
                Assert.Null(ex);

                // Database should exist but with no command rows
                Assert.True(File.Exists(tempDbPath));
                long commandCount = CountSQLiteRows(tempDbPath, "Commands");
                Assert.Equal(0, commandCount);
            }
            finally
            {
                options.HistorySavePathSQLite = originalHistorySavePathSQLite;
                options.HistorySavePathText = originalHistorySavePathText;
                options.HistorySaveStyle = originalHistorySaveStyle;
                options.HistoryType = originalHistoryType;
                PSConsoleReadLine.ClearHistory();

                try { if (File.Exists(tempDbPath)) File.Delete(tempDbPath); } catch { }
            }
        }

        [SkippableFact]
        public void SQLiteHistory_MigrationOnlyHappensOnce()
        {
            TestSetup(KeyMode.Cmd);

            var options = PSConsoleReadLine.GetOptions();
            var originalHistorySavePathText = options.HistorySavePathText;
            var originalHistorySavePathSQLite = options.HistorySavePathSQLite;
            var originalHistorySaveStyle = options.HistorySaveStyle;
            var originalHistoryType = options.HistoryType;

            var tempDbPath = Path.Combine(Path.GetTempPath(), $"PSReadLineTest_{Guid.NewGuid():N}.db");
            var tempTxtPath = Path.ChangeExtension(tempDbPath, ".txt");

            try
            {
                // Create a text history file
                File.WriteAllLines(tempTxtPath, new[] { "cmd1", "cmd2" });

                // First switch to SQLite - should migrate
                options.HistorySavePathText = tempTxtPath;
                options.HistorySavePathSQLite = tempDbPath;
                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };
                PSConsoleReadLine.SetOptions(setOptions);

                long countAfterFirstMigration = CountSQLiteRows(tempDbPath, "Commands");
                Assert.Equal(2, countAfterFirstMigration);

                // Add another command to the text file (simulating continued text use)
                File.AppendAllText(tempTxtPath, "cmd3\n");

                // Switch back to Text, then back to SQLite again
                // Since the DB already exists, migration should NOT run again
                options.HistoryType = originalHistoryType;
                PSConsoleReadLine.ClearHistory();

                PSConsoleReadLine.SetOptions(setOptions);

                long countAfterSecondSwitch = CountSQLiteRows(tempDbPath, "Commands");
                // Should still be 2, not 3 — the migration didn't re-run
                Assert.Equal(2, countAfterSecondSwitch);
            }
            finally
            {
                options.HistorySavePathSQLite = originalHistorySavePathSQLite;
                options.HistorySavePathText = originalHistorySavePathText;
                options.HistorySaveStyle = originalHistorySaveStyle;
                options.HistoryType = originalHistoryType;
                PSConsoleReadLine.ClearHistory();

                try { if (File.Exists(tempDbPath)) File.Delete(tempDbPath); } catch { }
                try { if (File.Exists(tempTxtPath)) File.Delete(tempTxtPath); } catch { }
            }
        }

        // =====================================================================
        // SQLite Location-Aware History Recall (new feature)
        // =====================================================================

        [SkippableFact]
        public void SQLiteHistory_LocationIsStoredPerCommand()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // Run commands — in test harness, Location defaults to "Unknown"
            Test("echo hello", Keys("echo hello"));
            Test("dir", Keys("dir"));

            // Verify that different commands share the same location ("Unknown")
            long locationCount = CountSQLiteRows(ctx.TempDbPath, "Locations");
            Assert.Equal(1, locationCount);

            // Verify both commands appear in ExecutionHistory
            long ehCount = CountSQLiteRows(ctx.TempDbPath, "ExecutionHistory");
            Assert.Equal(2, ehCount);
        }

        // =====================================================================
        // Migration Timestamp Ordering Tests (Bug Fix)
        // =====================================================================

        /// <summary>
        /// After migration, timestamps must be chronological (oldest text line = earliest time)
        /// and all must be older than UtcNow.
        /// Bug: Previously, timestamps were assigned during collection using
        /// DateTime.UtcNow.AddMinutes(-historyItems.Count), which gave the first text line
        /// (oldest) the newest timestamp and the last text line (newest) the oldest timestamp.
        /// </summary>
        [SkippableFact]
        public void SQLiteHistory_MigrationTimestampsAreChronologicalAndOlderThanNow()
        {
            TestSetup(KeyMode.Cmd);

            var options = PSConsoleReadLine.GetOptions();
            var originalHistorySavePathText = options.HistorySavePathText;
            var originalHistorySavePathSQLite = options.HistorySavePathSQLite;
            var originalHistorySaveStyle = options.HistorySaveStyle;
            var originalHistoryType = options.HistoryType;

            var tempDbPath = Path.Combine(Path.GetTempPath(), $"PSReadLineTest_{Guid.NewGuid():N}.db");
            var tempTxtPath = Path.ChangeExtension(tempDbPath, ".txt");

            try
            {
                // Create text history with known ordering: "first" is oldest, "third" is newest
                File.WriteAllLines(tempTxtPath, new[] { "first", "second", "third" });

                options.HistorySavePathText = tempTxtPath;
                options.HistorySavePathSQLite = tempDbPath;

                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };
                PSConsoleReadLine.SetOptions(setOptions);

                var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Query timestamps from DB in insertion order
                var entries = QuerySQLiteHistoryWithTimestamps(tempDbPath);

                Assert.Equal(3, entries.Length);

                // Find timestamps by command
                var firstTs = entries.First(e => e.CommandLine == "first").LastExecuted;
                var secondTs = entries.First(e => e.CommandLine == "second").LastExecuted;
                var thirdTs = entries.First(e => e.CommandLine == "third").LastExecuted;

                // Chronological order: oldest text line must have earliest timestamp
                Assert.True(firstTs < secondTs,
                    $"'first' timestamp ({firstTs}) should be before 'second' ({secondTs})");
                Assert.True(secondTs < thirdTs,
                    $"'second' timestamp ({secondTs}) should be before 'third' ({thirdTs})");

                // All timestamps must be older than "now"
                Assert.True(thirdTs < nowUnix,
                    $"Newest migrated timestamp ({thirdTs}) should be older than now ({nowUnix})");
            }
            finally
            {
                options.HistorySavePathSQLite = originalHistorySavePathSQLite;
                options.HistorySavePathText = originalHistorySavePathText;
                options.HistorySaveStyle = originalHistorySaveStyle;
                options.HistoryType = originalHistoryType;
                PSConsoleReadLine.ClearHistory();

                try { if (File.Exists(tempDbPath)) File.Delete(tempDbPath); } catch { }
                try { if (File.Exists(tempTxtPath)) File.Delete(tempTxtPath); } catch { }
            }
        }

        /// <summary>
        /// After migrating text history to SQLite, Up arrow should show the newest
        /// text line first (reverse chronological order).
        /// </summary>
        [SkippableFact]
        public void SQLiteHistory_UpArrowShowsNewestFirstAfterMigration()
        {
            TestSetup(KeyMode.Cmd);

            var options = PSConsoleReadLine.GetOptions();
            var originalHistorySavePathText = options.HistorySavePathText;
            var originalHistorySavePathSQLite = options.HistorySavePathSQLite;
            var originalHistorySaveStyle = options.HistorySaveStyle;
            var originalHistoryType = options.HistoryType;

            var tempDbPath = Path.Combine(Path.GetTempPath(), $"PSReadLineTest_{Guid.NewGuid():N}.db");
            var tempTxtPath = Path.ChangeExtension(tempDbPath, ".txt");

            try
            {
                // Create text history: "first" is oldest, "third" is newest
                File.WriteAllLines(tempTxtPath, new[] { "first", "second", "third" });

                options.HistorySavePathText = tempTxtPath;
                options.HistorySavePathSQLite = tempDbPath;

                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };
                PSConsoleReadLine.SetOptions(setOptions);

                // Up arrow should recall newest first: third, second, first
                Test("", Keys(
                    _.UpArrow, CheckThat(() => AssertLineIs("third")),
                    _.UpArrow, CheckThat(() => AssertLineIs("second")),
                    _.UpArrow, CheckThat(() => AssertLineIs("first")),
                    _.DownArrow, _.DownArrow, _.DownArrow));
            }
            finally
            {
                options.HistorySavePathSQLite = originalHistorySavePathSQLite;
                options.HistorySavePathText = originalHistorySavePathText;
                options.HistorySaveStyle = originalHistorySaveStyle;
                options.HistoryType = originalHistoryType;
                PSConsoleReadLine.ClearHistory();

                try { if (File.Exists(tempDbPath)) File.Delete(tempDbPath); } catch { }
                try { if (File.Exists(tempTxtPath)) File.Delete(tempTxtPath); } catch { }
            }
        }

        // =====================================================================
        // _saved Flag Tests (Bug Fix)
        // =====================================================================

        /// <summary>
        /// Items loaded from SQLite must have _saved=true so that IncrementalHistoryWrite
        /// doesn't re-write them with fresh UtcNow timestamps.
        /// Bug: Without _saved=true, the next IncrementalHistoryWrite re-wrote all loaded
        /// items, overwriting LastExecuted with UtcNow and incrementing ExecutionCount.
        /// </summary>
        [SkippableFact]
        public void SQLiteHistory_LoadedItemsNotRewrittenByIncrementalWrite()
        {
            TestSetup(KeyMode.Cmd);

            var options = PSConsoleReadLine.GetOptions();
            var originalHistorySavePathText = options.HistorySavePathText;
            var originalHistorySavePathSQLite = options.HistorySavePathSQLite;
            var originalHistorySaveStyle = options.HistorySaveStyle;
            var originalHistoryType = options.HistoryType;

            var tempDbPath = Path.Combine(Path.GetTempPath(), $"PSReadLineTest_{Guid.NewGuid():N}.db");
            var tempTxtPath = Path.ChangeExtension(tempDbPath, ".txt");

            try
            {
                // Create text history and migrate to SQLite
                File.WriteAllLines(tempTxtPath, new[] { "old-cmd1", "old-cmd2" });

                options.HistorySavePathText = tempTxtPath;
                options.HistorySavePathSQLite = tempDbPath;

                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };
                PSConsoleReadLine.SetOptions(setOptions);

                // Record execution counts after migration
                var beforeEntries = QuerySQLiteHistoryWithTimestamps(tempDbPath);
                var cmd1CountBefore = beforeEntries.First(e => e.CommandLine == "old-cmd1").ExecutionCount;
                var cmd2CountBefore = beforeEntries.First(e => e.CommandLine == "old-cmd2").ExecutionCount;

                // Run a NEW command — this triggers IncrementalHistoryWrite.
                // If loaded items lacked _saved=true, they'd all be re-written here.
                Test("new-cmd", Keys("new-cmd"));

                // Verify old commands' ExecutionCount hasn't been incremented
                var afterEntries = QuerySQLiteHistoryWithTimestamps(tempDbPath);
                var cmd1CountAfter = afterEntries.First(e => e.CommandLine == "old-cmd1").ExecutionCount;
                var cmd2CountAfter = afterEntries.First(e => e.CommandLine == "old-cmd2").ExecutionCount;

                Assert.Equal(cmd1CountBefore, cmd1CountAfter);
                Assert.Equal(cmd2CountBefore, cmd2CountAfter);

                // Also verify the new command was written
                Assert.Contains(afterEntries, e => e.CommandLine == "new-cmd");
            }
            finally
            {
                options.HistorySavePathSQLite = originalHistorySavePathSQLite;
                options.HistorySavePathText = originalHistorySavePathText;
                options.HistorySaveStyle = originalHistorySaveStyle;
                options.HistoryType = originalHistoryType;
                PSConsoleReadLine.ClearHistory();

                try { if (File.Exists(tempDbPath)) File.Delete(tempDbPath); } catch { }
                try { if (File.Exists(tempTxtPath)) File.Delete(tempTxtPath); } catch { }
            }
        }

        /// <summary>
        /// After writing commands to SQLite and reloading, the new command typed after
        /// reload should appear newest in Up-arrow recall, not interleaved with old ones.
        /// </summary>
        [SkippableFact]
        public void SQLiteHistory_MigratedTextHistoryOlderThanNewSQLiteEntries()
        {
            TestSetup(KeyMode.Cmd);

            var options = PSConsoleReadLine.GetOptions();
            var originalHistorySavePathText = options.HistorySavePathText;
            var originalHistorySavePathSQLite = options.HistorySavePathSQLite;
            var originalHistorySaveStyle = options.HistorySaveStyle;
            var originalHistoryType = options.HistoryType;

            var tempDbPath = Path.Combine(Path.GetTempPath(), $"PSReadLineTest_{Guid.NewGuid():N}.db");
            var tempTxtPath = Path.ChangeExtension(tempDbPath, ".txt");

            try
            {
                // Migrate text history
                File.WriteAllLines(tempTxtPath, new[] { "old-a", "old-b" });

                options.HistorySavePathText = tempTxtPath;
                options.HistorySavePathSQLite = tempDbPath;

                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };
                PSConsoleReadLine.SetOptions(setOptions);

                // Type a new command after migration
                Test("new-x", Keys("new-x"));

                // Up arrow should show new-x first (newest), then old-b, then old-a
                Test("", Keys(
                    _.UpArrow, CheckThat(() => AssertLineIs("new-x")),
                    _.UpArrow, CheckThat(() => AssertLineIs("old-b")),
                    _.UpArrow, CheckThat(() => AssertLineIs("old-a")),
                    _.DownArrow, _.DownArrow, _.DownArrow));
            }
            finally
            {
                options.HistorySavePathSQLite = originalHistorySavePathSQLite;
                options.HistorySavePathText = originalHistorySavePathText;
                options.HistorySaveStyle = originalHistorySaveStyle;
                options.HistoryType = originalHistoryType;
                PSConsoleReadLine.ClearHistory();

                try { if (File.Exists(tempDbPath)) File.Delete(tempDbPath); } catch { }
                try { if (File.Exists(tempTxtPath)) File.Delete(tempTxtPath); } catch { }
            }
        }

        // =====================================================================
        // Type Acceptance Test
        // =====================================================================

        [SkippableFact]
        public void SQLiteHistory_SetPSReadLineOptionAcceptsSQLite()
        {
            TestSetup(KeyMode.Cmd);

            var psrlOptions = PSConsoleReadLine.GetOptions();
            var originalSqlitePath = psrlOptions.HistorySavePathSQLite;
            var originalHistorySavePathText = psrlOptions.HistorySavePathText;
            var originalHistoryType = psrlOptions.HistoryType;
            var tempDbPath = Path.Combine(Path.GetTempPath(), $"PSReadLineTest_{Guid.NewGuid():N}.db");

            try
            {
                psrlOptions.HistorySavePathSQLite = tempDbPath;

                var optionsType = typeof(SetPSReadLineOption);
                var historyTypeProperty = optionsType.GetProperty("HistoryType");
                Assert.NotNull(historyTypeProperty);

                var options = new SetPSReadLineOption();
                historyTypeProperty.SetValue(options, HistoryType.SQLite);

                Exception ex = Record.Exception(() => PSConsoleReadLine.SetOptions(options));
                Assert.Null(ex);

                Assert.Equal(HistoryType.SQLite, historyTypeProperty.GetValue(options));

                // Restore to default
                historyTypeProperty.SetValue(options, HistoryType.Text);
                PSConsoleReadLine.SetOptions(options);
            }
            finally
            {
                psrlOptions.HistorySavePathSQLite = originalSqlitePath;
                psrlOptions.HistorySavePathText = originalHistorySavePathText;
                psrlOptions.HistoryType = originalHistoryType;
                PSConsoleReadLine.ClearHistory();
                try { if (File.Exists(tempDbPath)) File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
