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

        // =====================================================================
        // Location-Based History Recall Tests
        // =====================================================================

        /// <summary>
        /// Helper to add history items with specific locations.
        /// Uses the internal AddToHistory(string, string) overload via reflection.
        /// </summary>
        private void SetHistoryWithLocations(params (string command, string location)[] items)
        {
            PSConsoleReadLine.ClearHistory();
            foreach (var (command, location) in items)
            {
                typeof(PSConsoleReadLine)
                    .GetMethod("AddToHistory", BindingFlags.Static | BindingFlags.NonPublic,
                        null, new[] { typeof(string), typeof(string) }, null)
                    .Invoke(null, new object[] { command, location });
            }
        }

        /// <summary>
        /// Sets the mock current location for test purposes.
        /// </summary>
        private IDisposable SetTestLocation(string location)
        {
            typeof(PSConsoleReadLine)
                .GetField("_testCurrentLocation", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, location);
            return new TestLocationGuard();
        }

        private class TestLocationGuard : IDisposable
        {
            public void Dispose()
            {
                typeof(PSConsoleReadLine)
                    .GetField("_testCurrentLocation", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, null);
            }
        }

        [SkippableFact]
        public void SQLiteHistory_LocationRecall_MultipleItemsSameLocation()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory));

            using var location = SetTestLocation(@"C:\Projects\MyRepo");

            // Add 8 commands at the target location and 3 at another location
            SetHistoryWithLocations(
                ("git status", @"C:\Projects\MyRepo"),
                ("dotnet build", @"C:\Projects\MyRepo"),
                ("ls -la", @"C:\Other"),
                ("git log --oneline", @"C:\Projects\MyRepo"),
                ("dotnet test", @"C:\Projects\MyRepo"),
                ("cd ..", @"C:\Other"),
                ("git diff", @"C:\Projects\MyRepo"),
                ("dotnet publish", @"C:\Projects\MyRepo"),
                ("pwd", @"C:\Other"),
                ("git push", @"C:\Projects\MyRepo"),
                ("git pull --rebase", @"C:\Projects\MyRepo")
            );

            // Alt+Up should walk through ALL 8 commands at C:\Projects\MyRepo,
            // most recent first, skipping non-matching locations.
            // We verify all 8 are reachable, then navigate back and submit.
            Test("dotnet build", Keys(
                _.UpArrow, CheckThat(() => AssertLineIs("git pull --rebase")),
                _.UpArrow, CheckThat(() => AssertLineIs("git push")),
                _.UpArrow, CheckThat(() => AssertLineIs("dotnet publish")),
                _.UpArrow, CheckThat(() => AssertLineIs("git diff")),
                _.UpArrow, CheckThat(() => AssertLineIs("dotnet test")),
                _.UpArrow, CheckThat(() => AssertLineIs("git log --oneline")),
                _.UpArrow, CheckThat(() => AssertLineIs("dotnet build")),
                _.UpArrow, CheckThat(() => AssertLineIs("git status")),
                // Should stay at the oldest item when pressing Up again
                _.UpArrow, CheckThat(() => AssertLineIs("git status")),
                // Navigate forward to verify Down also works correctly
                _.DownArrow, CheckThat(() => AssertLineIs("dotnet build"))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_LocationRecall_NoLocationFallsBackToNormalRecall()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory));

            // Do NOT set test location - _testCurrentLocation is null, _engineIntrinsics is null
            // This should fall back to normal HistoryRecall

            SetHistory("cmd1", "cmd2", "cmd3");

            // PreviousLocationHistory falls back to normal HistoryRecall when
            // no location is available, so all 3 items should be reachable.
            Test("cmd1", Keys(
                _.UpArrow, CheckThat(() => AssertLineIs("cmd3")),
                _.UpArrow, CheckThat(() => AssertLineIs("cmd2")),
                _.UpArrow, CheckThat(() => AssertLineIs("cmd1"))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_LocationRecall_CaseInsensitivePaths()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory));

            using var location = SetTestLocation(@"C:\PROJECTS\myrepo");

            // Add commands with different path casings - should all match
            SetHistoryWithLocations(
                ("git status", @"c:\projects\myrepo"),
                ("git log", @"C:\Projects\MyRepo"),
                ("git diff", @"C:\PROJECTS\MYREPO"),
                ("unrelated", @"C:\Other")
            );

            // All three git commands should be accessible via location recall
            // regardless of path casing
            // All three git commands should be accessible via location recall
            // regardless of path casing
            Test("git status", Keys(
                _.UpArrow, CheckThat(() => AssertLineIs("git diff")),
                _.UpArrow, CheckThat(() => AssertLineIs("git log")),
                _.UpArrow, CheckThat(() => AssertLineIs("git status")),
                // Should stay at the oldest when pressing Up again
                _.UpArrow, CheckThat(() => AssertLineIs("git status"))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_LocationRecall_DifferentLocationsFiltered()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory));

            using var location = SetTestLocation(@"C:\Projects\RepoA");

            SetHistoryWithLocations(
                ("cmd-a1", @"C:\Projects\RepoA"),
                ("cmd-b1", @"C:\Projects\RepoB"),
                ("cmd-a2", @"C:\Projects\RepoA"),
                ("cmd-b2", @"C:\Projects\RepoB"),
                ("cmd-a3", @"C:\Projects\RepoA")
            );

            // Only commands from RepoA should appear
            // Only commands from RepoA should appear
            Test("cmd-a1", Keys(
                _.UpArrow, CheckThat(() => AssertLineIs("cmd-a3")),
                _.UpArrow, CheckThat(() => AssertLineIs("cmd-a2")),
                _.UpArrow, CheckThat(() => AssertLineIs("cmd-a1")),
                // Should stay at the oldest when pressing Up again
                _.UpArrow, CheckThat(() => AssertLineIs("cmd-a1"))
            ));
        }

        // =====================================================================
        // Frequency-Weighted History Ordering Tests
        // =====================================================================

        [SkippableFact]
        public void SQLiteHistory_FrequentCommandRanksHigher()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // Disable in-memory dedup so all writes reach SQLite
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption { HistoryNoDuplicates = false });

            // Run an erroneous command once
            Test("git log --one-line", Keys("git log --one-line"));

            // Run the correct command many times to boost its ExecutionCount
            for (int i = 0; i < 10; i++)
            {
                Test("git log --oneline", Keys("git log --oneline"));
            }

            // Verify ExecutionCount in the database
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={ctx.TempDbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // Check ExecutionCount for the correct command
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT ExecutionCount FROM HistoryView 
WHERE CommandLine = 'git log --oneline'";
            var correctCount = Convert.ToInt64(cmd.ExecuteScalar());

            using var cmd2 = connection.CreateCommand();
            cmd2.CommandText = @"
SELECT ExecutionCount FROM HistoryView 
WHERE CommandLine = 'git log --one-line'";
            var wrongCount = Convert.ToInt64(cmd2.ExecuteScalar());

            Assert.True(correctCount >= 10,
                $"Expected 'git log --oneline' ExecutionCount >= 10, got {correctCount}");
            Assert.True(wrongCount <= 1,
                $"Expected 'git log --one-line' ExecutionCount <= 1, got {wrongCount}");

            // Verify weighted ordering: the frequent command should appear first
            // (highest weighted score) so it's found first during backward search
            using var orderCmd = connection.CreateCommand();
            orderCmd.CommandText = @"
SELECT CommandLine FROM HistoryView 
WHERE CommandLine IN ('git log --oneline', 'git log --one-line')
ORDER BY (LastExecuted + MIN(ExecutionCount, 100) * 1800) DESC";
            using var reader = orderCmd.ExecuteReader();
            reader.Read();
            string firstResult = reader.GetString(0);
            Assert.Equal("git log --oneline", firstResult);
        }

        [SkippableFact]
        public void SQLiteHistory_ExecutionCountStoredOnHistoryItem()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // Disable in-memory dedup
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption { HistoryNoDuplicates = false });

            // Run a command 5 times
            for (int i = 0; i < 5; i++)
            {
                Test("echo repeated", Keys("echo repeated"));
            }

            // Verify ExecutionCount is stored in the database
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={ctx.TempDbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT ExecutionCount FROM HistoryView 
WHERE CommandLine = 'echo repeated'";
            var count = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.True(count >= 5, $"Expected ExecutionCount >= 5, got {count}");
        }

        [SkippableFact]
        public void SQLiteHistory_WeightedOrderPreservesChronologyForSingleUse()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // When all commands have ExecutionCount=1, ordering should be purely chronological
            Test("cmd1", Keys("cmd1"));
            Test("cmd2", Keys("cmd2"));
            Test("cmd3", Keys("cmd3"));

            // Verify chronological order in DB
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={ctx.TempDbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT CommandLine FROM HistoryView 
ORDER BY (LastExecuted + MIN(ExecutionCount, 100) * 1800) ASC";
            using var reader = cmd.ExecuteReader();

            var results = new System.Collections.Generic.List<string>();
            while (reader.Read())
                results.Add(reader.GetString(0));

            Assert.Equal("cmd1", results[0]);
            Assert.Equal("cmd2", results[1]);
            Assert.Equal("cmd3", results[2]);
        }

        // =====================================================================
        // History Item Removal Tests
        // =====================================================================

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItem_FromMemory()
        {
            TestSetup(KeyMode.Cmd);

            SetHistory("good-command", "bad-typo", "another-good");

            // Remove the typo from memory
            bool removed = PSConsoleReadLine.RemoveHistoryItem("bad-typo");
            Assert.True(removed, "RemoveHistoryItem should return true");

            var items = PSConsoleReadLine.GetHistoryItems();
            Assert.Equal(2, items.Length);
            Assert.DoesNotContain(items, i => i.CommandLine == "bad-typo");
            Assert.Contains(items, i => i.CommandLine == "good-command");
            Assert.Contains(items, i => i.CommandLine == "another-good");
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItem_FromSQLite()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // Add commands
            Test("git log --oneline", Keys("git log --oneline"));
            Test("git log --one-line", Keys("git log --one-line"));
            Test("git status", Keys("git status"));

            // Verify all three are in the database
            var commandsBefore = QuerySQLiteCommandLines(ctx.TempDbPath);
            Assert.Contains("git log --one-line", commandsBefore);

            // Remove the erroneous command
            bool removed = PSConsoleReadLine.RemoveHistoryItem("git log --one-line");
            Assert.True(removed, "RemoveHistoryItem should return true for SQLite removal");

            // Verify it's removed from the database
            var commandsAfter = QuerySQLiteCommandLines(ctx.TempDbPath);
            Assert.DoesNotContain("git log --one-line", commandsAfter);
            Assert.Contains("git log --oneline", commandsAfter);
            Assert.Contains("git status", commandsAfter);

            // Verify it's removed from memory too
            var historyItems = PSConsoleReadLine.GetHistoryItems();
            Assert.DoesNotContain(historyItems, i => i.CommandLine == "git log --one-line");
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItem_NonExistent()
        {
            TestSetup(KeyMode.Cmd);

            SetHistory("cmd1", "cmd2");

            // Removing a non-existent command should return false
            bool removed = PSConsoleReadLine.RemoveHistoryItem("does-not-exist");
            Assert.False(removed);

            // History should be unchanged
            var items = PSConsoleReadLine.GetHistoryItems();
            Assert.Equal(2, items.Length);
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItem_NullOrEmpty()
        {
            TestSetup(KeyMode.Cmd);

            SetHistory("cmd1");

            Assert.False(PSConsoleReadLine.RemoveHistoryItem(null));
            Assert.False(PSConsoleReadLine.RemoveHistoryItem(""));

            // History should be unchanged
            var items = PSConsoleReadLine.GetHistoryItems();
            Assert.Single(items);
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItem_ThenRecallWorks()
        {
            TestSetup(KeyMode.Cmd);

            SetHistory("cmd1", "bad-command", "cmd3");

            // Remove the bad command
            PSConsoleReadLine.RemoveHistoryItem("bad-command");

            // History recall should skip the removed item:
            // Up → cmd3, Up → cmd1 (no bad-command in between)
            Test("cmd1", Keys(
                _.UpArrow, CheckThat(() => AssertLineIs("cmd3")),
                _.UpArrow, CheckThat(() => AssertLineIs("cmd1"))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItem_SQLitePersistence()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();

            // Add and then remove a command
            Test("keep-this", Keys("keep-this"));
            Test("remove-this", Keys("remove-this"));

            PSConsoleReadLine.RemoveHistoryItem("remove-this");

            // Verify the ExecutionHistory entry is also removed (not just Commands)
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={ctx.TempDbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var ehCmd = connection.CreateCommand();
            ehCmd.CommandText = @"
SELECT COUNT(*) FROM ExecutionHistory eh
JOIN Commands c ON eh.CommandId = c.Id
WHERE c.CommandLine = 'remove-this'";
            long ehCount = (long)ehCmd.ExecuteScalar();
            Assert.Equal(0, ehCount);

            // But the kept command should still exist
            using var keepCmd = connection.CreateCommand();
            keepCmd.CommandText = @"
SELECT COUNT(*) FROM ExecutionHistory eh
JOIN Commands c ON eh.CommandId = c.Id
WHERE c.CommandLine = 'keep-this'";
            long keepCount = (long)keepCmd.ExecuteScalar();
            Assert.True(keepCount >= 1);
        }

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
                // Create a text history file with known content (oldest first)
                File.WriteAllLines(tempTxtPath, new[]
                {
                    "cmd-oldest",
                    "cmd-middle",
                    "cmd-newest"
                });

                var beforeMigration = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                options.HistorySavePathText = tempTxtPath;
                options.HistorySavePathSQLite = tempDbPath;

                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };
                PSConsoleReadLine.SetOptions(setOptions);

                // Query timestamps from the database in insertion order
                var connectionString = new SqliteConnectionStringBuilder($"Data Source={tempDbPath}")
                {
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT CommandLine, LastExecuted FROM HistoryView ORDER BY LastExecuted ASC";
                using var reader = cmd.ExecuteReader();

                var entries = new System.Collections.Generic.List<(string command, long lastExecuted)>();
                while (reader.Read())
                {
                    entries.Add((reader.GetString(0), reader.GetInt64(1)));
                }

                Assert.Equal(3, entries.Count);

                // 1. Chronological order preserved: oldest text line has smallest timestamp
                Assert.Equal("cmd-oldest", entries[0].command);
                Assert.Equal("cmd-middle", entries[1].command);
                Assert.Equal("cmd-newest", entries[2].command);

                // 2. Timestamps are strictly increasing
                Assert.True(entries[0].lastExecuted < entries[1].lastExecuted,
                    "oldest should have smaller timestamp than middle");
                Assert.True(entries[1].lastExecuted < entries[2].lastExecuted,
                    "middle should have smaller timestamp than newest");

                // 3. All migrated timestamps are older than "now"
                foreach (var entry in entries)
                {
                    Assert.True(entry.lastExecuted < beforeMigration,
                        $"Migrated entry '{entry.command}' has timestamp {entry.lastExecuted} which is not older than migration time {beforeMigration}");
                }
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
                // Create text history (these are "old" commands)
                File.WriteAllLines(tempTxtPath, new[]
                {
                    "old-cmd-1",
                    "old-cmd-2"
                });

                options.HistorySavePathText = tempTxtPath;
                options.HistorySavePathSQLite = tempDbPath;

                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };
                PSConsoleReadLine.SetOptions(setOptions);

                // Now add a new command via the normal path (simulates running a command after migration)
                Test("new-cmd-after-migration", Keys("new-cmd-after-migration"));

                // Query all entries ordered by LastExecuted
                var connectionString = new SqliteConnectionStringBuilder($"Data Source={tempDbPath}")
                {
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT CommandLine, LastExecuted FROM HistoryView ORDER BY LastExecuted ASC";
                using var reader = cmd.ExecuteReader();

                var entries = new System.Collections.Generic.List<(string command, long lastExecuted)>();
                while (reader.Read())
                {
                    entries.Add((reader.GetString(0), reader.GetInt64(1)));
                }

                Assert.Equal(3, entries.Count);

                // Migrated items should be first (oldest), new item should be last (newest)
                Assert.Equal("old-cmd-1", entries[0].command);
                Assert.Equal("old-cmd-2", entries[1].command);
                Assert.Equal("new-cmd-after-migration", entries[2].command);

                // The new entry's timestamp must be strictly greater than all migrated ones
                Assert.True(entries[2].lastExecuted > entries[1].lastExecuted,
                    "New SQLite entry must be newer than migrated text history");
                Assert.True(entries[2].lastExecuted > entries[0].lastExecuted,
                    "New SQLite entry must be newer than migrated text history");
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
                // Create text history (oldest first in the file)
                File.WriteAllLines(tempTxtPath, new[]
                {
                    "old-text-cmd-1",
                    "old-text-cmd-2",
                    "old-text-cmd-3"
                });

                options.HistorySavePathText = tempTxtPath;
                options.HistorySavePathSQLite = tempDbPath;

                var setOptions = new SetPSReadLineOption
                {
                    HistoryType = HistoryType.SQLite,
                    HistorySaveStyle = HistorySaveStyle.SaveIncrementally,
                };
                PSConsoleReadLine.SetOptions(setOptions);

                // Add a new entry after migration
                Test("new-sqlite-cmd", Keys("new-sqlite-cmd"));

                // Up arrow should show entries newest-first:
                // new-sqlite-cmd → old-text-cmd-3 → old-text-cmd-2 → old-text-cmd-1
                Test("old-text-cmd-1", Keys(
                    _.UpArrow, CheckThat(() => AssertLineIs("new-sqlite-cmd")),
                    _.UpArrow, CheckThat(() => AssertLineIs("old-text-cmd-3")),
                    _.UpArrow, CheckThat(() => AssertLineIs("old-text-cmd-2")),
                    _.UpArrow, CheckThat(() => AssertLineIs("old-text-cmd-1"))
                ));
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
    }
}
