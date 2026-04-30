# SQLite History (PSReadLine 3.0+)

PSReadLine 3.0 introduces a SQLite-backed command history as an alternative to
the traditional text-file history. The text history remains the default; SQLite
mode unlocks **per-directory recall**, **frequency-aware suggestions**, **fast
indexed queries**, and **richer F2 list view metadata**.

| | |
|---|---|
| **Module version** | `3.0.0` |
| **Required PowerShell** | `7.4` (LTS) or later |
| **Target framework** | `net8.0` |
| **Backwards compatibility** | Users on PowerShell &lt; 7.4 must stay on PSReadLine 2.x |

> ⚠️ **Breaking change.** PSReadLine 3.0 drops support for `netstandard2.0` and
> for PowerShell 5.1 / 7.0 / 7.2 because `Microsoft.Data.Sqlite` requires
> .NET 6+ APIs (`NativeLibrary.SetDllImportResolver`, populated `runtimeTargets`
> in `deps.json`) for cross-platform native deployment.

---

## Quick start

```powershell
# One-time switch to SQLite (also auto-migrates your existing text history)
Set-PSReadLineOption -HistoryType SQLite

# Verify
(Get-PSReadLineOption).HistoryType        # SQLite
(Get-PSReadLineOption).HistorySavePath    # …\PSReadLine\<host>_history.db
```

To make it permanent, add `Set-PSReadLineOption -HistoryType SQLite` to your
PowerShell profile (`$PROFILE`).

To revert to text history at any time:

```powershell
Set-PSReadLineOption -HistoryType Text
```

The `.db` file is preserved when switching back; switching to SQLite again
later picks up where you left off.

---

## Configuration options

Set via `Set-PSReadLineOption`, inspect via `Get-PSReadLineOption`.

| Option | Type | Default | Purpose |
|---|---|---|---|
| `-HistoryType` | `HistoryType` enum | `Text` | `Text` &#124; `SQLite`. Switches the active history backend. |
| `-HistorySavePathText` | `string` | platform-default `.txt` | Path to the text history file. Set independently of the active mode. |
| `-HistorySavePathSQLite` | `string` | platform-default `.db` | Path to the SQLite database. Set independently of the active mode. |
| `-AccessibleHistoryDisplay` | `switch` | `false` (or `true` if a screen reader is detected at startup) | Replaces emoji icons in the F2 stats tooltip and history navigation indicator with plain text labels (`Runs`, `Last`, `Dir`, `History`, `Location`). |

The read-only computed property `(Get-PSReadLineOption).HistorySavePath` always
returns the path of the *currently active* backend — it's `HistorySavePathText`
when `HistoryType=Text`, `HistorySavePathSQLite` when `HistoryType=SQLite`.

### `AddToHistoryOption.SQLite`

Custom `AddToHistoryHandler` script blocks may now return the new
`AddToHistoryOption.SQLite` value alongside the existing `SkipAdding`,
`MemoryOnly`, and `MemoryAndFile` options. When SQLite mode is active, the
default handler treats it interchangeably with `MemoryAndFile`.

### Default file paths

| Platform | Path |
|---|---|
| Windows | `%APPDATA%\Microsoft\Windows\PowerShell\PSReadLine\<host>_history.db` |
| Linux/macOS (XDG) | `$XDG_DATA_HOME/powershell/PSReadLine/<host>_history.db` |
| Linux/macOS (HOME) | `~/.local/share/powershell/PSReadLine/<host>_history.db` |
| Fallback | `/dev/null` |

`<host>` is the PowerShell host name (typically `ConsoleHost`). The text history
file uses the same path with a `.txt` extension.

---

## Key chords

The SQLite feature adds five new key bindings (and re-binds `Alt+Delete`).
All are available in **Windows** and **Emacs** key modes; **Vi** mode is
unchanged.

### Windows mode (default)

| Chord | Action | Notes |
|---|---|---|
| `Alt+UpArrow` | `PreviousLocationHistory` | Recall the previous command run from the **current directory**, sorted by frequency × recency. Falls back to chronological recall if the location is unknown. |
| `Alt+DownArrow` | `NextLocationHistory` | Forward direction of `PreviousLocationHistory`. |
| `Alt+Delete` | `RemoveFromHistoryAtCurrentLocation` | Delete the currently displayed history item from the **current directory only**. Other locations that ran the same command are preserved. Also works in the F2 list. **Previously bound to `KillWord`.** |
| `Ctrl+Shift+Delete` | `RemoveFromHistory` | Delete the currently displayed history item from **all locations**. Windows-only. |
| `Alt+F7` | `ClearHistory` | (Pre-existing.) Now also drops every row from the SQLite database. Windows-only. |

`KillWord` remains available on `Alt+D` and `Ctrl+Delete` (Windows).

### Emacs mode

| Chord | Action | Notes |
|---|---|---|
| `Alt+UpArrow` | `PreviousLocationHistory` | Same as Windows mode. |
| `Alt+DownArrow` | `NextLocationHistory` | Same as Windows mode. |
| `Alt+Delete` | `RemoveFromHistoryAtCurrentLocation` | Same as Windows mode. |
| `Ctrl+Alt+R` | `ReverseLocationSearchHistory` | Incremental backward search restricted to commands run from the current directory. (`Ctrl+R` continues to search all history.) |
| `Ctrl+Shift+Delete` | `RemoveFromHistory` | Same as Windows mode. Windows-only. |

### Up / Down arrow (`PreviousHistory` / `NextHistory`)

The plain Up / Down arrow recall is **chronological** in both modes — same
behavior as text mode. SQLite-side deduplication via `ROW_NUMBER()` means an
identical command appears only once even if executed many times.

### F2 (`SwitchPredictionView`)

Pre-existing key. In SQLite mode, the F2 list view now shows a colored
**stats tooltip** (`Runs`, `Last`, `Dir`) for the selected entry, and the
top half of the list is filtered to commands run from the current directory.
See [F2 list view enhancements](#f2-list-view-enhancements).

### Known terminal collision

`Alt+UpArrow` / `Alt+DownArrow` collide with **VS Code's** integrated terminal
selection mode. Use Windows Terminal, iTerm2, or a standalone `pwsh.exe`
window for these chords. They work fine everywhere else.

---

## Public methods (programmatic API)

These `[Microsoft.PowerShell.PSConsoleReadLine]` static methods can be invoked
from scripts or bound to custom keys via `Set-PSReadLineKeyHandler -Function`.

| Method | Description |
|---|---|
| `RemoveHistoryItem(string commandLine)` → `bool` | Removes every occurrence of the given command from in-memory history and (when in SQLite mode) drops the matching `Commands` row plus all its `ExecutionHistory` entries. Returns `true` if anything was removed. |
| `RemoveHistoryItemAtLocation(string commandLine, string location)` → `bool` | Like `RemoveHistoryItem` but scoped to a single directory. In SQLite mode, only the matching `(Command, Location)` row in `ExecutionHistory` is deleted; the `Commands` row is dropped only when no other location still references it. |
| `RemoveFromHistory()` | Key-handler entry point. Removes the currently displayed history item globally and advances to the next older item. Also handles deletion when an item is selected in the F2 list view. |
| `RemoveFromHistoryAtCurrentLocation()` | Key-handler entry point. Removes the current item from the current location only (in SQLite mode). In text mode, falls back to a global remove because per-location data isn't tracked. |
| `PreviousLocationHistory()` / `NextLocationHistory()` | Key-handler entry points for `Alt+Up` / `Alt+Down` location-aware recall. |
| `ReverseLocationSearchHistory()` / `ForwardLocationSearchHistory()` | Incremental search filtered to the current directory. |
| `ClearHistory()` | Pre-existing. Now also wipes the SQLite database when in SQLite mode. |

---

## F2 list view enhancements

### Stats tooltip

When `HistoryType=SQLite`, selecting a history item in the F2 prediction list
view shows a colored stats line below the selection:

```
↻ Runs 47  │  ⏱ Last 2m ago  │  📂 Dir ~/repos/PSReadline
```

| Field | Source |
|---|---|
| `Runs` | `SUM(ExecutionCount)` across all locations — the total number of times this command has been executed anywhere. |
| `Last` | Relative time derived from `StartTime` (`just now` / `Nm ago` / `Nh ago` / `Nd ago` / `MMM d`). |
| `Dir` | `Location` — the directory where this command was last executed. Omitted when location is unknown. |

The tooltip uses the existing `ShowToolTips` infrastructure (default `true`)
and the `ListPredictionTooltip` color. In text history mode the tooltip
remains `null` (no per-command stats are tracked).

### Ordering

| Position | Sort key | Notes |
|---|---|---|
| Top half | **Per-location** frecency (`ExecutionCount` at this directory + recency) | Filtered to commands matching `$PWD`. |
| Bottom half | **Total** frecency (sum across all locations + recency) | Excludes items already shown in the top half. |
| Backfill | Bottom-half items | Used when fewer than half-capacity local matches exist. |
| Plugins active (3 slots) | Total frecency only | Too few slots to split into local / global partitions. |

In **text mode** the F2 list ordering is unchanged — pure recency.

---

## History navigation indicator

While you're stepping through history with Up / Down or `Alt+Up` / `Alt+Down`,
PSReadLine renders a small status indicator below the prompt:

| Mode | Indicator | Source |
|---|---|---|
| Chronological recall | `[⏱ 3/15]` | `Up` / `Down`, `Ctrl+P` / `Ctrl+N` |
| Location-filtered recall | `[📂 2/5]` | `Alt+Up` / `Alt+Down` |

The indicator is cleared as soon as you press a non-history key (typing
characters, Enter, Escape, etc.). The position counter only counts
**navigable** items — cross-session entries marked `FromOtherSession` are
skipped, so the count won't jump unexpectedly.

This indicator is rendered in both Text and SQLite modes whenever the
respective recall functions are invoked.

---

## Accessibility

`-AccessibleHistoryDisplay` swaps the emoji icons used by the SQLite history
UX for plain-text labels so screen readers don't verbalize Unicode character
names ("clockwise gapped circle arrow", "card index dividers").

```powershell
Set-PSReadLineOption -AccessibleHistoryDisplay
```

| Surface | Default rendering | With `-AccessibleHistoryDisplay` |
|---|---|---|
| F2 stats tooltip | `↻ Runs 47  │  ⏱ Last 2m ago  │  📂 Dir <path>` | `Runs 47  │  Last 2m ago  │  Dir <path>` |
| Chronological nav indicator | `[⏱ 3/15]` | `[History 3/15]` |
| Location-filtered nav indicator | `[📂 2/5]` | `[Location 2/5]` |

### Default value

The option is **seeded once at startup** from the value of
`ScreenReaderModeEnabled`, so users running with a screen reader active get
plain-text labels automatically.

After construction the two options are independent — toggling
`-EnableScreenReaderMode` later does **not** auto-flip
`-AccessibleHistoryDisplay`. This is intentional: it lets sighted users
opt in to plain text (e.g. on terminals without emoji-font support) without
touching the broader screen-reader rendering mode.

---

## Migration from text history

When you switch to SQLite mode for the first time and the configured
`HistorySavePathSQLite` file does **not** exist, PSReadLine automatically
imports your existing text history (`HistorySavePathText`) into the new
database.

### How timestamps are assigned

Migrated lines have **no original timestamps** in the text file, so PSReadLine
assigns synthetic ones such that:

1. All migrated entries are stamped **older than "now"**, so any new SQLite
   entries sort after them.
2. The original chronological order in the `.txt` file is preserved.

Concretely: the oldest line gets the earliest timestamp; each subsequent line
gets one minute later; the newest text-history line is still at least one
minute older than the first command you run after the switch.

### What happens to the `.txt` file

The text file is **not** deleted or modified — migration only reads from it.
You can switch back to text history at any time and resume using it. If you
want to keep both backends in sync, switch to SQLite mode after each session;
the most recent commands are picked up from the text file on each migration.

To migrate explicitly later (e.g. after editing the text file by hand), point
`HistorySavePathSQLite` at a path that doesn't exist yet and switch modes:

```powershell
Set-PSReadLineOption -HistorySavePathSQLite "$HOME\new-history.db"
Set-PSReadLineOption -HistoryType SQLite      # triggers migration
```

---

## Text vs SQLite — feature comparison

| | Text (default) | SQLite |
|---|---|---|
| Backend | Plain text file | Indexed SQLite database |
| Up / Down recall | Chronological | Chronological (DB-side dedup) |
| `Alt+Up` / `Alt+Down` | Same as Up / Down | **Per-directory** frequency × recency |
| F2 list ordering | Pure recency | Local frecency + global frecency split |
| F2 stats tooltip | Not shown | Runs / Last / Dir |
| History navigation indicator | Shown | Shown |
| `Alt+Delete` removal scope | Global (no per-directory data) | Current directory only |
| `Ctrl+Shift+Delete` removal scope | Global | Global (all directories) |
| `Ctrl+Alt+R` (Emacs) | All-history search | Current-directory search |
| Cross-session merging | Append-on-save | Incremental DB reads |
| Lookup complexity | O(n) linear scan | O(log n) indexed |
| Human-readable on disk | Yes | No (binary `.db`) |
| Native dependency | None | `e_sqlite3` (auto-deployed per RID) |

---

## Database schema (for advanced users)

The SQLite database uses a normalized three-table schema. You can query it
directly with the `sqlite3` CLI or any SQLite client.

| Table | Columns (key fields) |
|---|---|
| `Commands` | `Id`, `CommandLine`, `CommandHash` (indexed) |
| `Locations` | `Id`, `Path` (normalized) |
| `ExecutionHistory` | `Id`, `CommandId` (FK), `LocationId` (FK), `StartTime` (Unix seconds), `LastExecuted` (Unix seconds, indexed), `ElapsedTime`, `ExecutionCount` (indexed) |

A convenience view `HistoryView` joins the three tables and exposes
denormalized `CommandLine`, `Location`, `LastExecuted`, and `ExecutionCount`
columns, e.g.:

```sql
SELECT CommandLine, Location, datetime(LastExecuted, 'unixepoch'), ExecutionCount
FROM HistoryView
ORDER BY LastExecuted DESC
LIMIT 20;
```

Timestamps are stored as **Unix seconds** (`ToUnixTimeSeconds()`). Use
`datetime(col, 'unixepoch')` in SQL queries — do **not** treat them as .NET ticks.

---

## Limitations and known issues

1. **Same command at different directories, back-to-back.** `HistoryNoDuplicates`
   (default `true`) drops a new entry whose `CommandLine` exactly matches the
   *previous in-memory item*, regardless of `Location`. Typing `git status`
   in `~/repos/A`, then `cd ~/repos/B`, then `git status` again will record
   only the first occurrence.

2. **Cross-session live merge.** When multiple `pwsh` sessions are open at the
   same time, each session sees commands from other sessions only on its own
   incremental read. Items from other sessions are flagged `FromOtherSession`
   and are skipped by `Up` / `Down` recall (so you don't accidentally rerun a
   colleague's command from a different terminal).

3. **VS Code terminal `Alt+Up` collision.** As noted above, VS Code intercepts
   `Alt+Up` for terminal-selection mode. Use a different host or rebind.

4. **PowerShell &lt; 7.4 unsupported.** Trying to load PSReadLine 3.0 on an
   older PowerShell will fail at module-import time. Stay on PSReadLine 2.x
   if you need 5.1 / 7.0 / 7.2 support.

---

## Related skill notes

The implementation details (recall ordering algorithm, native library
deployment, framework-migration rationale, F2 list rendering pitfalls) are
documented separately in the maintainer-facing skill files under
`~/.copilot/skills/sqlite-history/` — see `SKILL.md` and the four
`references/*.md` files for architecture, schema-and-history logic,
API surface, and testing patterns.
