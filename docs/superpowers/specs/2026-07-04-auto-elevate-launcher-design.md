# Auto Elevate Launcher Design

## Purpose

Build a lightweight Windows tray application that automatically starts selected PowerShell scripts and executable programs with administrator privileges after the user logs in, without showing a UAC prompt on every login.

The application uses Windows Task Scheduler's official "run with highest privileges" mechanism. It is an authorized elevation launcher, not a UAC bypass exploit.

## Goals

- Provide a lightweight tray application for Windows.
- Support multiple startup items.
- Support PowerShell scripts (`.ps1`) and executable programs (`.exe`).
- Run enabled startup items automatically after user login.
- Run startup items with administrator privileges without prompting on every login.
- Support startup arguments for each item.
- Save full logs for startup attempts and PowerShell output.
- Provide a simple management window for adding, editing, enabling, disabling, and deleting startup items.
- Deliver first as a portable green `.exe`, with installer packaging left for later.

## Non-Goals

- No Windows service in the first version.
- No always-visible desktop window.
- No manual administrator launcher as a primary feature.
- No per-item ordinary-permission mode in the first version. Managed startup items are intended to run elevated.
- No plugin system or remote management.

## Product Shape

The app is a C#/.NET WinForms tray program.

- A tray icon stays resident while the app is running.
- Double-clicking the tray icon opens the manager window.
- The manager window is only shown when needed.
- The app itself may be started at login through a normal non-elevated scheduled task.
- Managed startup items are started by their own elevated scheduled tasks.

## Startup Item Model

Each startup item contains:

- `Id`: stable unique identifier.
- `Name`: display name.
- `Type`: `PowerShellScript` or `Executable`.
- `Path`: full path to the `.ps1` or `.exe`.
- `Arguments`: user-provided startup arguments, passed to the script or executable.
- `WorkingDirectory`: directory used when launching the item.
- `Enabled`: whether the item should run after login.
- `TaskName`: Windows scheduled task name generated from the item ID.
- `TaskSyncStatus`: whether the scheduled task is synchronized with the saved config.
- `LastTaskError`: latest scheduled task creation or update error when synchronization fails.
- `LastRunStartedAt`: latest known start time.
- `LastRunFinishedAt`: latest known finish time when available.
- `LastExitCode`: latest known exit code when available.
- `LastStatus`: latest known status, such as `NeverRun`, `Running`, `Succeeded`, `Failed`, or `Unknown`.

Configuration is stored at:

```text
%AppData%\AutoElevateLauncher\config.json
```

Logs are stored under:

```text
%AppData%\AutoElevateLauncher\logs\
```

Each startup item gets its own log directory.

## Scheduled Task Strategy

Each startup item maps to one Windows scheduled task.

- Trigger: current user logon.
- Privilege: run with highest privileges.
- Action: run the application in internal runner mode with the startup item ID.
- State: enabled or disabled according to the startup item.

Using one task per startup item keeps task state, enable/disable behavior, and stop behavior clear. It also makes it easier to diagnose one failing item without affecting the rest.

Creating or updating elevated tasks may require a UAC prompt at configuration time. After the task is created, logon-triggered runs should not prompt again.

## Runner Mode

The application executable supports two modes:

- `manager` mode: normal tray and management UI.
- `runner` mode: hidden execution mode invoked by scheduled tasks.

Example runner invocation:

```text
AutoElevateLauncher.exe --run-item <item-id>
```

Runner mode loads the config, finds the startup item, launches the target, records status, and writes logs.

For PowerShell scripts, runner mode starts PowerShell with the script path and user arguments. The first version should prefer Windows PowerShell (`powershell.exe`) for compatibility, with room to add a `pwsh.exe` option later.

For executable programs, runner mode starts the executable path with the user arguments and configured working directory.

## Argument Handling

Each item has a plain text argument field.

Examples:

```text
-Mode Auto -Config "D:\configs\my config.json"
--profile default --silent
```

The app must preserve spaces and quotes when passing arguments to the target process. The app does not validate the semantic meaning of arguments; it only validates that the target path exists and that the item type matches the file extension.

Logs include the resolved target path, working directory, and argument string used for launch.

## Logging

Runner mode writes one log file per run.

PowerShell script logs include:

- Start time.
- End time when the process exits.
- Target path.
- Working directory.
- Arguments.
- Standard output.
- Standard error.
- Exit code.

Executable logs include:

- Start time.
- Target path.
- Working directory.
- Arguments.
- Process ID when launch succeeds.
- Launch exception when launch fails.

Some executable programs may continue running indefinitely or detach from the parent process. In that case, the log records that the process was started, and the final exit code may remain unknown.

## User Interface

The manager window uses a left-right split layout.

Left side:

- Startup item list.
- Columns or visible fields: name, type, enabled state, latest status.
- Buttons: add script, add program, delete, enable/disable.

Right side:

- Selected item details.
- Fields: name, path, arguments, working directory, enabled state, latest start time, latest finish time, latest exit code, latest status.
- Buttons: save, run now, stop, open log directory.

Tray menu:

- Open manager.
- Start tray app at login toggle.
- Exit.

The `run now` and `stop` buttons exist for management and troubleshooting. The core product behavior is automatic logon startup.

## Data Flow

1. User adds or edits a startup item in the manager window.
2. The app validates the path and saves `config.json`.
3. The app creates or updates the corresponding scheduled task.
4. On user login, Windows Task Scheduler triggers enabled item tasks.
5. Each item task starts `AutoElevateLauncher.exe --run-item <item-id>` with highest privileges.
6. Runner mode reads config, launches the configured script or executable, and writes logs.
7. The manager window reads config and logs to show status.

## Error Handling

- Missing path: prevent saving and show a clear validation message.
- Unsupported file type: prevent adding the item.
- Scheduled task creation failure: show the error details and mark the item as not synchronized.
- UAC denied during task creation: leave existing task unchanged when possible and show a retryable error.
- PowerShell launch failure: log exception details.
- PowerShell script failure: log stderr and exit code.
- Executable launch failure: log exception details.
- Config read failure: show an error and avoid overwriting the invalid config automatically.

## Testing Plan

- Add a `.ps1` startup item and verify its scheduled task is created.
- Add a `.exe` startup item and verify its scheduled task is created.
- Verify startup arguments containing spaces and quotes are passed correctly.
- Verify enabled tasks run after user login without a UAC prompt.
- Verify disabled tasks do not run after user login.
- Verify deleting an item deletes its scheduled task.
- Verify PowerShell stdout, stderr, and exit code are logged.
- Verify executable launch success and launch failure are logged.
- Verify the app handles a missing target path with a user-facing error.

## First Version Scope

First version includes:

- WinForms tray app.
- Manager window with split layout.
- Config storage in `%AppData%`.
- Multiple startup items.
- PowerShell script and executable startup items.
- Per-item arguments and working directory.
- Elevated scheduled task creation per item.
- Runner mode.
- Log files per run.
- Portable executable delivery.

Later versions may add:

- Installer package.
- PowerShell 7 (`pwsh.exe`) selection.
- Log cleanup policy.
- Startup delay and ordering.
- Import/export configuration.
