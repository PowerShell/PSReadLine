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

        // =====================================================================
        // Location-Scoped History Removal Tests (RemoveHistoryItemAtLocation
        // and the Alt+Delete -> RemoveFromHistoryAtCurrentLocation handler)
        // =====================================================================

        /// <summary>
        /// Helper to count ExecutionHistory rows for a given (CommandLine, Location) pair.
        /// </summary>
        private long CountExecutionHistoryAt(string dbPath, string commandLine, string location)
        {
            var connectionString = new SqliteConnectionStringBuilder($"Data Source={dbPath}")
            {
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT COUNT(*) FROM ExecutionHistory eh
JOIN Commands c ON eh.CommandId = c.Id
JOIN Locations l ON eh.LocationId = l.Id
WHERE c.CommandLine = @CommandLine AND l.Path = @Location";
            cmd.Parameters.AddWithValue("@CommandLine", commandLine);
            cmd.Parameters.AddWithValue("@Location", location);
            return (long)cmd.ExecuteScalar();
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItemAtLocation_RemovesOnlyMatchingLocation()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();
            using var loc = SetTestLocation(@"C:\Projects\A");

            // Same command run at two locations.  An interleaved different command
            // is required so HistoryNoDuplicates (on by default) doesn't skip the
            // second "git status" entry as a consecutive dup.
            SetHistoryWithLocations(
                ("git status", @"C:\Projects\A"),
                ("unrelated",  @"C:\Projects\A"),
                ("git status", @"C:\Projects\B"));

            // Sanity: both ExecutionHistory rows exist.
            Assert.Equal(1, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\A"));
            Assert.Equal(1, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\B"));

            bool removed = PSConsoleReadLine.RemoveHistoryItemAtLocation("git status", @"C:\Projects\A");
            Assert.True(removed);

            // Only the A-location row should be gone; B-location row stays.
            Assert.Equal(0, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\A"));
            Assert.Equal(1, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\B"));

            // Commands row must still exist because location B still references it.
            var commands = QuerySQLiteCommandLines(ctx.TempDbPath);
            Assert.Contains("git status", commands);
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItemAtLocation_DropsOrphanedCommandRow()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();
            using var loc = SetTestLocation(@"C:\Only");

            // Command run at exactly one location.
            SetHistoryWithLocations(("only-here", @"C:\Only"));
            Assert.Contains("only-here", QuerySQLiteCommandLines(ctx.TempDbPath));

            bool removed = PSConsoleReadLine.RemoveHistoryItemAtLocation("only-here", @"C:\Only");
            Assert.True(removed);

            // Both ExecutionHistory and Commands rows should be gone (orphan cleanup).
            Assert.Equal(0, CountExecutionHistoryAt(ctx.TempDbPath, "only-here", @"C:\Only"));
            Assert.DoesNotContain("only-here", QuerySQLiteCommandLines(ctx.TempDbPath));
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItemAtLocation_InMemoryRespectsLocation()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();
            using var loc = SetTestLocation(@"C:\Projects\A");

            // Two in-memory items with the same command line but different locations.
            // Interleave a different command so HistoryNoDuplicates doesn't skip
            // the second "git pull" as a consecutive dup of the first.
            SetHistoryWithLocations(
                ("git pull", @"C:\Projects\A"),
                ("sep",      @"C:\Projects\A"),
                ("git pull", @"C:\Projects\B"));

            PSConsoleReadLine.RemoveHistoryItemAtLocation("git pull", @"C:\Projects\A");

            // The B-location "git pull" should still be in memory; the
            // separator "sep" stays too. The A-location "git pull" is gone.
            var items = PSConsoleReadLine.GetHistoryItems();
            Assert.Equal(2, items.Length);
            var pullItem = Assert.Single(items, i => i.CommandLine == "git pull");
            Assert.Equal(@"C:\Projects\B", pullItem.Location);
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItemAtLocation_NoMatchReturnsFalse()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();
            using var loc = SetTestLocation(@"C:\Projects\A");

            SetHistoryWithLocations(("git status", @"C:\Projects\A"));

            // Wrong location — should be a no-op.
            bool removed = PSConsoleReadLine.RemoveHistoryItemAtLocation("git status", @"C:\Nowhere");
            Assert.False(removed);

            // Original row still intact.
            Assert.Equal(1, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\A"));
            Assert.Single(PSConsoleReadLine.GetHistoryItems());
        }

        [SkippableFact]
        public void SQLiteHistory_RemoveHistoryItemAtLocation_NullOrEmpty()
        {
            TestSetup(KeyMode.Cmd);

            SetHistory("cmd1");

            Assert.False(PSConsoleReadLine.RemoveHistoryItemAtLocation(null, "loc"));
            Assert.False(PSConsoleReadLine.RemoveHistoryItemAtLocation("", "loc"));
            Assert.False(PSConsoleReadLine.RemoveHistoryItemAtLocation("cmd1", null));
            Assert.False(PSConsoleReadLine.RemoveHistoryItemAtLocation("cmd1", ""));

            Assert.Single(PSConsoleReadLine.GetHistoryItems());
        }

        [SkippableFact]
        public void SQLiteHistory_AltDelete_RemovesAtCurrentLocationOnly()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();
            using var loc = SetTestLocation(@"C:\Projects\A");

            // Same command at current location and elsewhere; an interleaved entry
            // is needed so HistoryNoDuplicates doesn't drop the second "git status".
            // Up arrow surfaces the most-recently-added item first ("git status" at A).
            SetHistoryWithLocations(
                ("git status", @"C:\Projects\B"),
                ("sep",        @"C:\Projects\B"),
                ("git status", @"C:\Projects\A"));

            // Up arrow surfaces "git status" (the A-location entry, most recent).
            // Alt+Delete should remove only the A entry. After deletion, the
            // separator "sep" is now the most recent in-memory item; the B-location
            // "git status" is still further back. RemoveFromHistoryAtCurrentLocation
            // advances to savedIndex-1 which is now the "sep" entry.
            Test("sep", Keys(
                _.UpArrow,
                CheckThat(() => AssertLineIs("git status")),
                _.Alt_Delete,
                CheckThat(() => AssertLineIs("sep"))
            ));

            // DB: A-row deleted, B-row preserved, Commands row still present.
            Assert.Equal(0, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\A"));
            Assert.Equal(1, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\B"));
            Assert.Contains("git status", QuerySQLiteCommandLines(ctx.TempDbPath));
        }

        [SkippableFact]
        public void SQLiteHistory_AltDelete_DingsWhenItemNotAtCurrentLocation()
        {
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();
            using var loc = SetTestLocation(@"C:\Projects\Current");

            // Item exists in history but was run at a different location.
            SetHistoryWithLocations(("git status", @"C:\Projects\Other"));

            // Up arrow shows it; Alt+Delete should NOT delete (not run here),
            // and the line should remain unchanged.
            Test("git status", Keys(
                _.UpArrow,
                CheckThat(() => AssertLineIs("git status")),
                _.Alt_Delete,
                CheckThat(() => AssertLineIs("git status"))
            ));

            // DB row at the other location must still exist.
            Assert.Equal(1, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\Other"));
            Assert.Contains("git status", QuerySQLiteCommandLines(ctx.TempDbPath));
        }

        [SkippableFact]
        public void SQLiteHistory_AltDelete_FallsBackToGlobalInTextMode()
        {
            // Text mode: no SQLite, no current location concept.
            // Alt+Delete must still do something useful — falls back to global removal.
            TestSetup(KeyMode.Cmd);

            SetHistory("cmd1", "cmd2", "cmd3");

            Test("cmd1", Keys(
                _.UpArrow,                                  // recall cmd3
                CheckThat(() => AssertLineIs("cmd3")),
                _.Alt_Delete,                               // global fallback removes cmd3
                CheckThat(() => AssertLineIs("cmd2")),
                _.Alt_Delete,                               // and cmd2
                CheckThat(() => AssertLineIs("cmd1"))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_GlobalRemove_StillWipesAllLocations()
        {
            // The global RemoveHistoryItem path (used by Ctrl+Shift+Delete) must
            // continue to wipe every (Command, Location) pair — regression check.
            TestSetup(KeyMode.Cmd);
            using var ctx = SetupSQLiteHistory();
            using var loc = SetTestLocation(@"C:\Projects\A");

            SetHistoryWithLocations(
                ("git status", @"C:\Projects\A"),
                ("sep1",       @"C:\Projects\A"),
                ("git status", @"C:\Projects\B"),
                ("sep2",       @"C:\Projects\B"),
                ("git status", @"C:\Projects\C"));

            bool removed = PSConsoleReadLine.RemoveHistoryItem("git status");
            Assert.True(removed);

            Assert.Equal(0, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\A"));
            Assert.Equal(0, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\B"));
            Assert.Equal(0, CountExecutionHistoryAt(ctx.TempDbPath, "git status", @"C:\Projects\C"));
            Assert.DoesNotContain("git status", QuerySQLiteCommandLines(ctx.TempDbPath));
        }

        // =====================================================================
        // Sticky Location Mode Tests
        //
        // When the user enters location-filtered navigation via Alt+Up
        // (PreviousLocationHistory), the sorted list and current position should
        // remain "sticky" so that plain Up / Down (PreviousHistory / NextHistory)
        // continue to navigate the same location-filtered set, instead of dropping
        // the user back into raw chronological history at an unrelated index.
        //
        // The tests below bind:
        //   UpArrow    -> PreviousLocationHistory  (simulates Alt+Up "enter mode")
        //   DownArrow  -> NextLocationHistory      (simulates Alt+Down)
        //   Ctrl+P     -> PreviousHistory          (simulates plain Up after Alt release)
        //   Ctrl+N     -> NextHistory              (simulates plain Down after Alt release)
        //   Ctrl+G     -> CancelLine               (used to break the sticky chain)
        // =====================================================================

        [SkippableFact]
        public void SQLiteHistory_LocationRecall_StickyMode_PlainUpContinuesLocationList()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory),
                new KeyHandler("Ctrl+p", PSConsoleReadLine.PreviousHistory),
                new KeyHandler("Ctrl+n", PSConsoleReadLine.NextHistory));

            using var loc = SetTestLocation(@"C:\Projects\Sticky");

            SetHistoryWithLocations(
                ("loc-cmd-1", @"C:\Projects\Sticky"),
                ("other-1",   @"C:\Other"),
                ("loc-cmd-2", @"C:\Projects\Sticky"),
                ("other-2",   @"C:\Other"),
                ("loc-cmd-3", @"C:\Projects\Sticky"));

            // UpArrow (location mode) lands on newest local entry, then plain
            // Ctrl+P (regular Up) should KEEP filtering by location instead of
            // jumping to "other-2" or some unrelated chronological neighbor.
            Test("loc-cmd-3", Keys(
                _.UpArrow,  CheckThat(() => AssertLineIs("loc-cmd-3")),
                _.Ctrl_p,   CheckThat(() => AssertLineIs("loc-cmd-2")),
                _.Ctrl_p,   CheckThat(() => AssertLineIs("loc-cmd-1")),
                // Plain Ctrl+N (Down) also stays in sticky location mode.
                _.Ctrl_n,   CheckThat(() => AssertLineIs("loc-cmd-2")),
                _.Ctrl_n,   CheckThat(() => AssertLineIs("loc-cmd-3"))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_LocationRecall_StickyMode_ClearedOnNonHistoryAction()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory),
                new KeyHandler("Ctrl+p", PSConsoleReadLine.PreviousHistory),
                new KeyHandler("Ctrl+n", PSConsoleReadLine.NextHistory));

            using var loc = SetTestLocation(@"C:\Projects\Sticky");

            SetHistoryWithLocations(
                ("loc-cmd-1", @"C:\Projects\Sticky"),
                ("other-1",   @"C:\Other"),
                ("loc-cmd-2", @"C:\Projects\Sticky"),
                ("other-2",   @"C:\Other"));

            // Enter location mode, then perform a non-history action (typing a
            // character). After that, plain Ctrl+P should walk regular
            // chronological history (which includes "other-*" entries),
            // NOT the location-filtered list.
            // After 'x' is typed the main loop's anyHistoryCommandCount reset branch
            // fires (no history command was issued for that key), which exits sticky
            // mode AND resets _currentHistoryIndex back to _history.Count. The next
            // Ctrl+P therefore replaces the buffer with the most-recent chronological
            // entry ("other-2") — losing the typed 'x'. That's the same behavior the
            // text-mode HistoryRecall has always had after editing a recalled line.
            Test("other-2", Keys(
                _.UpArrow,        CheckThat(() => AssertLineIs("loc-cmd-2")),
                'x',              CheckThat(() => AssertLineIs("loc-cmd-2x")),
                _.Ctrl_p,         CheckThat(() => AssertLineIs("other-2"))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_LocationRecall_StickyMode_AltUpStillAdvances()
        {
            // After plain Up has been used inside sticky mode, pressing Alt+Up
            // (PreviousLocationHistory) again should keep advancing through the
            // same sorted location list — not rebuild it from scratch.
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory),
                new KeyHandler("Ctrl+p", PSConsoleReadLine.PreviousHistory),
                new KeyHandler("Ctrl+n", PSConsoleReadLine.NextHistory));

            using var loc = SetTestLocation(@"C:\Projects\Sticky");

            SetHistoryWithLocations(
                ("loc-a", @"C:\Projects\Sticky"),
                ("nope",  @"C:\Other"),
                ("loc-b", @"C:\Projects\Sticky"),
                ("loc-c", @"C:\Projects\Sticky"));

            Test("loc-b", Keys(
                _.UpArrow,  CheckThat(() => AssertLineIs("loc-c")),
                _.Ctrl_p,   CheckThat(() => AssertLineIs("loc-b")),
                _.UpArrow,  CheckThat(() => AssertLineIs("loc-a")),
                _.Ctrl_n,   CheckThat(() => AssertLineIs("loc-b"))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_LocationRecall_StickyMode_NotEnteredWhenJustPlainUp()
        {
            // Plain Up alone (without ever pressing Alt+Up) must not behave like
            // location mode — it walks raw chronological history including items
            // from other directories.
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+p", PSConsoleReadLine.PreviousHistory),
                new KeyHandler("Ctrl+n", PSConsoleReadLine.NextHistory));

            using var loc = SetTestLocation(@"C:\Projects\Sticky");

            SetHistoryWithLocations(
                ("loc-1",   @"C:\Projects\Sticky"),
                ("other-1", @"C:\Other"),
                ("loc-2",   @"C:\Projects\Sticky"),
                ("other-2", @"C:\Other"));

            Test("loc-1", Keys(
                _.Ctrl_p, CheckThat(() => AssertLineIs("other-2")),
                _.Ctrl_p, CheckThat(() => AssertLineIs("loc-2")),
                _.Ctrl_p, CheckThat(() => AssertLineIs("other-1")),
                _.Ctrl_p, CheckThat(() => AssertLineIs("loc-1"))
            ));
        }

        // =====================================================================
        // History Navigation Position Indicator Tests
        //
        // The "[BOOK pos/total]" indicator is rendered into _statusLinePrompt
        // while the user is navigating history (chronological or location-mode).
        // It is cleared once the user does any non-history action.
        // =====================================================================

        private static string GetStatusLinePromptForTest()
        {
            var fld = typeof(PSConsoleReadLine).GetField(
                "_statusLinePrompt",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var singletonFld = typeof(PSConsoleReadLine).GetField(
                "_singleton",
                BindingFlags.Static | BindingFlags.NonPublic);
            var singleton = singletonFld.GetValue(null);
            return (string)fld.GetValue(singleton);
        }

        private static string ExpectedNavStatus(int pos, int total, bool locationMode)
        {
            // Mirror ShowHistoryNavStatus: brackets default-colored, inner uses ListPredictionColor.
            var color = (string)typeof(PSConsoleReadLineOptions)
                .GetField("_listPredictionColor", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(PSConsoleReadLine.GetOptions());
            var inner = locationMode
                ? $"\uD83D\uDCC2 {pos}/{total}"
                : $"\u23F1 {pos}/{total}";
            return $"[{color}{inner}\x1b[0m]";
        }

        [SkippableFact]
        public void SQLiteHistory_HistoryNavStatus_ShownDuringChronologicalRecall()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+p", PSConsoleReadLine.PreviousHistory),
                new KeyHandler("Ctrl+n", PSConsoleReadLine.NextHistory));

            SetHistory("a", "b", "c");

            Test("a", Keys(
                _.Ctrl_p, CheckThat(() => AssertLineIs("c")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(1, 3, false), GetStatusLinePromptForTest())),
                _.Ctrl_p, CheckThat(() => AssertLineIs("b")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(2, 3, false), GetStatusLinePromptForTest())),
                _.Ctrl_p, CheckThat(() => AssertLineIs("a")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(3, 3, false), GetStatusLinePromptForTest()))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_HistoryNavStatus_ShownDuringLocationRecallWithLocLabel()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory));

            using var loc = SetTestLocation(@"C:\Projects\Status");

            SetHistoryWithLocations(
                ("local-1", @"C:\Projects\Status"),
                ("other",   @"C:\Other"),
                ("local-2", @"C:\Projects\Status"),
                ("local-3", @"C:\Projects\Status"));

            Test("local-1", Keys(
                _.UpArrow,
                CheckThat(() => AssertLineIs("local-3")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(1, 3, true), GetStatusLinePromptForTest())),
                _.UpArrow,
                CheckThat(() => AssertLineIs("local-2")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(2, 3, true), GetStatusLinePromptForTest())),
                _.UpArrow,
                CheckThat(() => AssertLineIs("local-1")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(3, 3, true), GetStatusLinePromptForTest()))
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_HistoryNavStatus_ClearedAfterEditing()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+p", PSConsoleReadLine.PreviousHistory));

            SetHistory("alpha", "bravo");

            Test("bravox", Keys(
                _.Ctrl_p,
                CheckThat(() => AssertLineIs("bravo")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(1, 2, false), GetStatusLinePromptForTest())),
                'x',
                // Typing a character is a non-history action: the indicator must clear.
                CheckThat(() => Assert.Null(GetStatusLinePromptForTest()))
            ));
        }

        /// <summary>
        /// Marks the in-memory _history items at the given indices as FromOtherSession
        /// so they get skipped by HistoryRecall (mirroring cross-session SQLite items).
        /// </summary>
        private static void MarkHistoryItemsFromOtherSession(params int[] indices)
        {
            var singletonFld = typeof(PSConsoleReadLine).GetField(
                "_singleton", BindingFlags.Static | BindingFlags.NonPublic);
            var singleton = singletonFld.GetValue(null);
            var historyFld = typeof(PSConsoleReadLine).GetField(
                "_history", BindingFlags.Instance | BindingFlags.NonPublic);
            var history = historyFld.GetValue(singleton);
            // HistoryQueue<HistoryItem> exposes an indexer; use reflection to call it.
            var indexer = history.GetType().GetProperty("Item");
            var historyItemType = typeof(PSConsoleReadLine).GetNestedType(
                "HistoryItem", BindingFlags.Public | BindingFlags.NonPublic);
            var fromOtherProp = historyItemType.GetProperty(
                "FromOtherSession", BindingFlags.Public | BindingFlags.Instance);
            foreach (var i in indices)
            {
                var item = indexer.GetValue(history, new object[] { i });
                fromOtherProp.SetValue(item, true);
            }
        }

        [SkippableFact]
        public void SQLiteHistory_HistoryNavStatus_CountsOnlyNavigableItems()        {
            // Regression test: previously the indicator used the raw _history slot,
            // so cross-session items between two in-session items caused the
            // displayed position to "jump" by hundreds even though Up only moved
            // by one navigable item.
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+p", PSConsoleReadLine.PreviousHistory));

            // History layout (oldest -> newest):
            //   [0] mine-old    (this session)
            //   [1] other-1     (other session, skipped)
            //   [2] other-2     (other session, skipped)
            //   [3] other-3     (other session, skipped)
            //   [4] mine-new    (this session)
            // Navigable total = 2. Up #1 lands on "mine-new" => 1/2.
            // Up #2 must skip the three other-session items and land on
            // "mine-old" => 2/2 (NOT 5/5 or 4/5).
            SetHistory("mine-old", "other-1", "other-2", "other-3", "mine-new");
            MarkHistoryItemsFromOtherSession(1, 2, 3);

            Test("", Keys(
                _.Ctrl_p,
                CheckThat(() => AssertLineIs("mine-new")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(1, 2, false), GetStatusLinePromptForTest())),
                _.Ctrl_p,
                CheckThat(() => AssertLineIs("mine-old")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(2, 2, false), GetStatusLinePromptForTest())),
                _.Escape
            ));
        }

        [SkippableFact]
        public void SQLiteHistory_AltDelete_InLocationMode_RefreshesIndicatorAndAdvances()
        {
            // Regression test: previously Alt+Delete while navigating location-filtered
            // history left _locationSortedIndices stale (built against pre-deletion
            // _history) and never updated _locationSortedPosition. Result was the
            // indicator stuck at e.g. 1/3 instead of 1/2, and the next Alt+Up landed
            // on the wrong item because the cached indices were shifted.
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory));
            using var ctx = SetupSQLiteHistory();
            using var loc = SetTestLocation(@"C:\Projects\AltDel");

            // Three unique commands at the current location, equal frequency.
            // Sort = recency DESC: pos 0 = "gamma", pos 1 = "beta", pos 2 = "alpha".
            SetHistoryWithLocations(
                ("alpha", @"C:\Projects\AltDel"),
                ("beta",  @"C:\Projects\AltDel"),
                ("gamma", @"C:\Projects\AltDel"));

            Test("beta", Keys(
                _.UpArrow,                                  // pos 0 -> "gamma" (1/3)
                CheckThat(() => AssertLineIs("gamma")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(1, 3, true), GetStatusLinePromptForTest())),
                _.Alt_Delete,                               // remove "gamma"
                // Indicator must refresh: 1/2, line must advance to next location item.
                CheckThat(() => AssertLineIs("beta")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(1, 2, true), GetStatusLinePromptForTest())),
                // Subsequent Alt+Up must use the rebuilt sorted list, not the stale one.
                _.UpArrow,                                  // advance to next older
                CheckThat(() => AssertLineIs("alpha")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(2, 2, true), GetStatusLinePromptForTest())),
                _.DownArrow,                                // back to "beta"
                CheckThat(() => AssertLineIs("beta")),
                CheckThat(() => Assert.Equal(ExpectedNavStatus(1, 2, true), GetStatusLinePromptForTest()))
            ));

            // DB sanity: "gamma" gone at this location, others intact.
            Assert.Equal(0, CountExecutionHistoryAt(ctx.TempDbPath, "gamma", @"C:\Projects\AltDel"));
            Assert.Equal(1, CountExecutionHistoryAt(ctx.TempDbPath, "beta",  @"C:\Projects\AltDel"));
            Assert.Equal(1, CountExecutionHistoryAt(ctx.TempDbPath, "alpha", @"C:\Projects\AltDel"));
        }

        // =====================================================================
        // AccessibleHistoryDisplay option
        //
        // The SQLite-history-awareness UX uses emojis (⟳, ⏱, 📂) in two places:
        //   1) The F2 list-view stats tooltip rendered by RenderHistoryStatsTooltip
        //   2) The in-prompt navigation indicator rendered by ShowHistoryNavStatus
        //
        // Screen readers verbalize emoji as Unicode names ("clockwise gapped circle
        // arrow", "card index dividers"). The AccessibleHistoryDisplay option, when
        // enabled, swaps emoji for plain-text labels:
        //   * Tooltip:  "⟳ Runs N | ⏱ Last 2m ago | 📂 Dir <path>"
        //               -> "Runs N | Last 2m ago | Dir <path>"
        //   * Indicator: "[⏱ 3/15]" / "[📂 2/5]"
        //               -> "[History 3/15]" / "[Location 2/5]"
        //
        // Default value is seeded once at construction from ScreenReaderModeEnabled
        // and is independent thereafter.
        // =====================================================================

        // Emoji code points used by the SQLite history awareness UX.
        private const string TooltipRunsEmoji = "\u27f3";       // ⟳
        private const string TooltipLastEmoji = "\u23f1";       // ⏱
        private const string TooltipDirEmoji  = "\U0001F4C2";   // 📂
        private const string NavStatusChronoEmoji = "\u23F1";   // ⏱ (BMP, used by ShowHistoryNavStatus)
        private const string NavStatusLocEmoji    = "\uD83D\uDCC2"; // 📂 (surrogate pair)

        /// <summary>
        /// Renders the F2 stats tooltip into a fresh buffer and returns the resulting
        /// line as a string. RenderHistoryStatsTooltip is a private method on the
        /// nested PredictionListView class — invoke it via reflection.
        /// </summary>
        private static string CaptureRenderedHistoryStatsTooltip(string commandLine, int executionCount, DateTime startTime, string location)
        {
            // Build a HistoryItem with the requested fields. Setters are internal so
            // we go through reflection to keep this resilient.
            var historyItemType = typeof(PSConsoleReadLine).GetNestedType(
                "HistoryItem", BindingFlags.Public | BindingFlags.NonPublic);
            var historyItem = Activator.CreateInstance(historyItemType);
            historyItemType.GetProperty("CommandLine").SetValue(historyItem, commandLine);
            historyItemType.GetProperty("ExecutionCount").SetValue(historyItem, executionCount);
            historyItemType.GetProperty("StartTime").SetValue(historyItem, startTime);
            historyItemType.GetProperty("Location").SetValue(historyItem, location);

            // Get the singleton and instantiate PredictionListView via its internal ctor.
            var singletonFld = typeof(PSConsoleReadLine).GetField(
                "_singleton", BindingFlags.Static | BindingFlags.NonPublic);
            var singleton = singletonFld.GetValue(null);
            var listViewType = typeof(PSConsoleReadLine).GetNestedType(
                "PredictionListView", BindingFlags.NonPublic);
            var listView = Activator.CreateInstance(
                listViewType,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                binder: null,
                args: new[] { singleton },
                culture: null);

            var renderMethod = listViewType.GetMethod(
                "RenderHistoryStatsTooltip",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // NextBufferLine pre-increments `current`, then creates a new StringBuilder
            // when current == consoleBufferLines.Count. Start with an empty list and
            // current = -1 so the first call advances to index 0 and writes there.
            var buffer = new System.Collections.Generic.List<System.Text.StringBuilder>();
            object[] args = new object[] { historyItem, buffer, -1 };
            renderMethod.Invoke(listView, args);

            return buffer.Count > 0 ? buffer[0].ToString() : string.Empty;
        }

        /// <summary>Mirror ExpectedNavStatus for the accessible-display variant.</summary>
        private static string ExpectedAccessibleNavStatus(int pos, int total, bool locationMode)
        {
            var color = (string)typeof(PSConsoleReadLineOptions)
                .GetField("_listPredictionColor", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(PSConsoleReadLine.GetOptions());
            var inner = locationMode
                ? $"Location {pos}/{total}"
                : $"History {pos}/{total}";
            return $"[{color}{inner}\x1b[0m]";
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_DefaultMatchesScreenReaderModeAtConstruction()
        {
            // Construct a fresh options object directly — the constructor seeds
            // AccessibleHistoryDisplay from the screen-reader detection result.
            // Verify the seeded value matches whatever ScreenReaderModeEnabled was set to.
            var opts = new PSConsoleReadLineOptions("AccessibleDefaultHost", usingLegacyConsole: false);
            Assert.Equal(opts.ScreenReaderModeEnabled, opts.AccessibleHistoryDisplay);
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_RoundTripsViaSetPSReadLineOption()
        {
            TestSetup(KeyMode.Cmd);
            var originalAccessibleValue = PSConsoleReadLine.GetOptions().AccessibleHistoryDisplay;
            try
            {
                PSConsoleReadLine.SetOptions(new SetPSReadLineOption { AccessibleHistoryDisplay = true });
                Assert.True(PSConsoleReadLine.GetOptions().AccessibleHistoryDisplay);

                PSConsoleReadLine.SetOptions(new SetPSReadLineOption { AccessibleHistoryDisplay = false });
                Assert.False(PSConsoleReadLine.GetOptions().AccessibleHistoryDisplay);
            }
            finally
            {
                PSConsoleReadLine.GetOptions().AccessibleHistoryDisplay = originalAccessibleValue;
            }
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_IndependentFromScreenReaderModeAfterInit()
        {
            // Toggling EnableScreenReaderMode after construction must NOT auto-flip
            // AccessibleHistoryDisplay. Per design, the flag is only seeded once at
            // construction and is independent thereafter.
            TestSetup(KeyMode.Cmd);
            var opts = PSConsoleReadLine.GetOptions();
            var originalAccessible = opts.AccessibleHistoryDisplay;
            var originalScreenReader = opts.ScreenReaderModeEnabled;

            try
            {
                // Force AccessibleHistoryDisplay to a known-false state.
                PSConsoleReadLine.SetOptions(new SetPSReadLineOption { AccessibleHistoryDisplay = false });
                Assert.False(opts.AccessibleHistoryDisplay);

                // Flip the screen-reader option — accessible flag must NOT follow.
                PSConsoleReadLine.SetOptions(new SetPSReadLineOption { EnableScreenReaderMode = true });
                Assert.True(opts.ScreenReaderModeEnabled);
                Assert.False(opts.AccessibleHistoryDisplay);

                // And the reverse — flipping screen reader off must not auto-disable.
                PSConsoleReadLine.SetOptions(new SetPSReadLineOption { AccessibleHistoryDisplay = true });
                PSConsoleReadLine.SetOptions(new SetPSReadLineOption { EnableScreenReaderMode = false });
                Assert.False(opts.ScreenReaderModeEnabled);
                Assert.True(opts.AccessibleHistoryDisplay);
            }
            finally
            {
                opts.AccessibleHistoryDisplay = originalAccessible;
                opts.ScreenReaderModeEnabled = originalScreenReader;
            }
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_TooltipOmitsEmojiWhenEnabled()
        {
            TestSetup(KeyMode.Cmd);
            var opts = PSConsoleReadLine.GetOptions();
            var original = opts.AccessibleHistoryDisplay;
            try
            {
                opts.AccessibleHistoryDisplay = true;
                var rendered = CaptureRenderedHistoryStatsTooltip(
                    commandLine: "git status",
                    executionCount: 47,
                    startTime: DateTime.UtcNow.AddMinutes(-2),
                    location: @"C:\repos\PSReadline");

                // Emoji icons must be absent.
                Assert.DoesNotContain(TooltipRunsEmoji, rendered);
                Assert.DoesNotContain(TooltipLastEmoji, rendered);
                Assert.DoesNotContain(TooltipDirEmoji,  rendered);

                // Plain-text labels and value must remain.
                Assert.Contains("Runs ", rendered);
                Assert.Contains("47",    rendered);
                Assert.Contains("Last ", rendered);
                Assert.Contains("Dir ",  rendered);
                Assert.Contains(@"C:\repos\PSReadline", rendered);
            }
            finally
            {
                opts.AccessibleHistoryDisplay = original;
            }
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_TooltipKeepsEmojiWhenDisabled()
        {
            // Regression: with the option off, the existing emoji-decorated tooltip
            // must be unchanged.
            TestSetup(KeyMode.Cmd);
            var opts = PSConsoleReadLine.GetOptions();
            var original = opts.AccessibleHistoryDisplay;
            try
            {
                opts.AccessibleHistoryDisplay = false;
                var rendered = CaptureRenderedHistoryStatsTooltip(
                    commandLine: "git status",
                    executionCount: 5,
                    startTime: DateTime.UtcNow.AddMinutes(-3),
                    location: @"C:\repos\PSReadline");

                Assert.Contains(TooltipRunsEmoji, rendered);
                Assert.Contains(TooltipLastEmoji, rendered);
                Assert.Contains(TooltipDirEmoji,  rendered);
                Assert.Contains("Runs ", rendered);
                Assert.Contains("Last ", rendered);
                Assert.Contains("Dir ",  rendered);
            }
            finally
            {
                opts.AccessibleHistoryDisplay = original;
            }
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_TooltipOmitsAbsentFieldsWithoutStraySeparators()
        {
            // Edge case: an item with no StartTime and no Location must not leave
            // separator characters or stray emoji in the rendered output.
            TestSetup(KeyMode.Cmd);
            var opts = PSConsoleReadLine.GetOptions();
            var original = opts.AccessibleHistoryDisplay;
            try
            {
                opts.AccessibleHistoryDisplay = true;
                var rendered = CaptureRenderedHistoryStatsTooltip(
                    commandLine: "alone",
                    executionCount: 1,
                    startTime: default,
                    location: null);

                Assert.Contains("Runs ", rendered);
                Assert.Contains("1",      rendered);
                // No Last / Dir labels.
                Assert.DoesNotContain("Last ", rendered);
                Assert.DoesNotContain("Dir ",  rendered);
                // No separator characters between sections.
                Assert.DoesNotContain("\u2502", rendered);
                // No emoji at all.
                Assert.DoesNotContain(TooltipRunsEmoji, rendered);
                Assert.DoesNotContain(TooltipLastEmoji, rendered);
                Assert.DoesNotContain(TooltipDirEmoji,  rendered);
            }
            finally
            {
                opts.AccessibleHistoryDisplay = original;
            }
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_TooltipOmitsLocationWhenUnknown()
        {
            // "Unknown" location entries (legacy text-history migrations) must not
            // produce a Dir segment regardless of AccessibleHistoryDisplay.
            TestSetup(KeyMode.Cmd);
            var opts = PSConsoleReadLine.GetOptions();
            var original = opts.AccessibleHistoryDisplay;
            try
            {
                opts.AccessibleHistoryDisplay = true;
                var rendered = CaptureRenderedHistoryStatsTooltip(
                    commandLine: "legacy",
                    executionCount: 3,
                    startTime: DateTime.UtcNow.AddHours(-2),
                    location: "Unknown");

                Assert.Contains("Runs ", rendered);
                Assert.Contains("Last ", rendered);
                Assert.DoesNotContain("Dir ", rendered);
            }
            finally
            {
                opts.AccessibleHistoryDisplay = original;
            }
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_NavStatusUsesTextLabelsForChronologicalRecall()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+p", PSConsoleReadLine.PreviousHistory));

            var opts = PSConsoleReadLine.GetOptions();
            var original = opts.AccessibleHistoryDisplay;
            try
            {
                opts.AccessibleHistoryDisplay = true;

                SetHistory("a", "b", "c");

                Test("", Keys(
                    _.Ctrl_p,
                    CheckThat(() => AssertLineIs("c")),
                    CheckThat(() => Assert.Equal(
                        ExpectedAccessibleNavStatus(1, 3, locationMode: false),
                        GetStatusLinePromptForTest())),
                    // Status line must NOT contain the chronological emoji.
                    CheckThat(() => Assert.DoesNotContain(NavStatusChronoEmoji, GetStatusLinePromptForTest())),
                    _.Escape
                ));
            }
            finally
            {
                opts.AccessibleHistoryDisplay = original;
            }
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_NavStatusUsesTextLabelsForLocationRecall()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("UpArrow", PSConsoleReadLine.PreviousLocationHistory),
                new KeyHandler("DownArrow", PSConsoleReadLine.NextLocationHistory));

            var opts = PSConsoleReadLine.GetOptions();
            var original = opts.AccessibleHistoryDisplay;
            try
            {
                opts.AccessibleHistoryDisplay = true;

                using var loc = SetTestLocation(@"C:\Projects\Accessible");

                SetHistoryWithLocations(
                    ("local-1", @"C:\Projects\Accessible"),
                    ("other",   @"C:\Other"),
                    ("local-2", @"C:\Projects\Accessible"));

                Test("", Keys(
                    _.UpArrow,
                    CheckThat(() => AssertLineIs("local-2")),
                    CheckThat(() => Assert.Equal(
                        ExpectedAccessibleNavStatus(1, 2, locationMode: true),
                        GetStatusLinePromptForTest())),
                    // Status line must NOT contain the location emoji surrogate pair.
                    CheckThat(() => Assert.DoesNotContain(NavStatusLocEmoji, GetStatusLinePromptForTest())),
                    _.Escape
                ));
            }
            finally
            {
                opts.AccessibleHistoryDisplay = original;
            }
        }

        [SkippableFact]
        public void AccessibleHistoryDisplay_NavStatusKeepsEmojiWhenDisabled()
        {
            // Regression: with the option off, the existing emoji indicator must
            // continue to render as before.
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+p", PSConsoleReadLine.PreviousHistory));

            var opts = PSConsoleReadLine.GetOptions();
            var original = opts.AccessibleHistoryDisplay;
            try
            {
                opts.AccessibleHistoryDisplay = false;

                SetHistory("a", "b");

                Test("", Keys(
                    _.Ctrl_p,
                    CheckThat(() => AssertLineIs("b")),
                    CheckThat(() => Assert.Contains(NavStatusChronoEmoji, GetStatusLinePromptForTest())),
                    CheckThat(() => Assert.DoesNotContain("History ", GetStatusLinePromptForTest())),
                    _.Escape
                ));
            }
            finally
            {
                opts.AccessibleHistoryDisplay = original;
            }
        }
    }
}
