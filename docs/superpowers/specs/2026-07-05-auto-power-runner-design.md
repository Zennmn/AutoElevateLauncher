# Auto Power Runner Design

## Goal

Build a lightweight Windows desktop app that starts configured PowerShell scripts and executable files when the current user logs in. The app should provide both a tray icon and a normal window interface. It should support administrator-elevated autostart without requiring the user to manually approve UAC on every login.

## Decisions Confirmed

- Platform: C# with .NET 8 and WPF.
- App shape: tray-resident app with a window management interface.
- Managed item types: PowerShell scripts and EXE files.
- Task count: multiple managed tasks.
- Task run modes: run once or long-running.
- Long-running task exit behavior: record the exit; do not automatically restart.
- Autostart trigger: current user login.
- Elevated autostart: use Windows Task Scheduler with highest privileges.
- First version UI scope: basic management UI.

## Non-Goals For Version 1

- No Windows service.
- No system-boot-before-login mode.
- No automatic restart for exited long-running tasks.
- No schedule editor beyond user-login autostart.
- No task groups, search, import, or export.
- No advanced log viewer.
- No remote management.

## Architecture

The app will be a small WPF desktop program. The main process owns the tray icon, window lifecycle, configuration, autostart registration, and child process management.

Core areas:

- `App`: application startup, single-instance guard, tray lifecycle, and shutdown flow.
- `MainWindow`: task list, task editor, autostart controls, and action buttons.
- `Models`: task configuration, task type, run mode, runtime status, and recent result.
- `TaskConfigService`: load and save JSON configuration.
- `ProcessRunner`: start, monitor, and stop PowerShell scripts and EXE files.
- `StartupTaskService`: create, remove, and inspect the Windows scheduled task.
- `LogService`: append simple text logs for troubleshooting.

Configuration is stored at:

```text
%APPDATA%\AutoPowerRunner\config.json
```

Logs are stored at:

```text
%LOCALAPPDATA%\AutoPowerRunner\logs\app.log
```

## Administrator Autostart

The app will expose an "Enable administrator autostart" action. When the user enables it, the app requests administrator permission once so it can create a Windows scheduled task.

Scheduled task settings:

- Trigger: current user logon.
- Target: the WPF app executable.
- Privilege: run with highest privileges.
- User: current Windows user.

Expected behavior:

- Enabling or updating the scheduled task may require one UAC approval.
- After the task is registered, Windows starts the app at login with elevated privileges.
- The user should not need to approve UAC manually on every login.
- Child PowerShell scripts and EXE files inherit the elevated context from the main app by default.

Disabling administrator autostart removes the scheduled task. If removal requires elevation, the app asks for it at that time.

## Task Model

Each managed task contains:

- `Id`: stable internal identifier.
- `Name`: display name.
- `Type`: `PowerShellScript` or `Executable`.
- `Path`: `.ps1` or `.exe` path.
- `Arguments`: optional command-line arguments.
- `WorkingDirectory`: optional; defaults to the file's directory.
- `RunMode`: `RunOnce` or `LongRunning`.
- `IsEnabled`: whether it should start automatically when the app starts.
- `LastStatus`: not running, running, exited, or failed to start.
- `LastExitCode`: most recent process exit code, when available.
- `LastStartedAt`: most recent start time.
- `LastExitedAt`: most recent exit time.
- `LastError`: most recent startup or runtime error summary.

Startup behavior:

- When the app starts, it loads all configured tasks.
- All enabled tasks are started automatically.
- Disabled tasks remain listed but are not started.

Run-once behavior:

- Start the process.
- Wait for it to exit in the background.
- Record exit code and timestamps.
- Do not restart it.

Long-running behavior:

- Start the process.
- Keep a runtime handle so it can be stopped from the window or tray menu.
- If it exits on its own, record the exit and leave it stopped.
- Do not automatically restart it.

PowerShell launch behavior:

```text
powershell.exe -ExecutionPolicy Bypass -File "<script-path>" <arguments>
```

EXE launch behavior:

```text
"<exe-path>" <arguments>
```

## Window UI

The first version window is a basic management interface.

Top area:

- Display current administrator autostart status.
- Button to enable or disable administrator autostart.

Main area:

- Task list with name, type, run mode, enabled state, running state, and recent result.
- Add task.
- Edit selected task.
- Delete selected task.
- Enable or disable selected task.
- Run selected task now.
- Stop selected running task.
- View recent result or simple log location.

Task editor fields:

- Name.
- Type.
- Path picker.
- Arguments.
- Working directory.
- Run mode.
- Enabled checkbox.

Window lifecycle:

- Closing the window hides it to the tray by default.
- Exiting from the tray menu shuts the app down.

## Tray Behavior

The tray icon keeps the app accessible while the window is hidden.

Tray menu:

- Open window.
- Run all enabled tasks.
- Stop all running tasks.
- Enable or disable administrator autostart.
- Exit.

Exit behavior:

- If long-running tasks are active, the app attempts to stop them before exiting.
- The app records stop attempts and any failures in the log.

## Error Handling

Missing script or EXE path:

- Do not start the task.
- Set status to failed to start.
- Store the error in recent result.
- Write the error to the log.

Scheduled task creation failure:

- Show the error reason.
- If elevation is missing, explain that administrator permission is needed to create the elevated startup task.
- Write details to the log.

PowerShell or EXE startup failure:

- Set status to failed to start.
- Store the exception summary.
- Write details to the log.

Damaged configuration file:

- Rename the damaged file to a timestamped backup.
- Create a new default configuration.
- Show a clear message in the UI.
- Write details to the log.

Stopping a running task:

- Try a normal close request when possible.
- If the process does not exit within a short timeout, terminate it.
- Record the final outcome.

## Testing And Verification

Automated tests should cover:

- JSON configuration save and load with multiple tasks.
- Damaged configuration backup behavior.
- PowerShell command construction.
- EXE command construction.
- Runtime status updates when a process exits.
- Scheduled task command/settings generation where feasible without mutating the machine.

Manual verification should cover:

- Add, edit, delete, enable, and disable tasks.
- Run a PowerShell script once and record its exit code.
- Run a long-running PowerShell script and stop it.
- Run an EXE once.
- Hide the window to tray and reopen it.
- Exit from the tray menu while a long-running task is active.
- Enable administrator autostart, log out and back in, and confirm the app starts elevated without a manual UAC prompt.

## Acceptance Criteria

- The app can manage multiple PowerShell and EXE tasks.
- The app has both a tray icon and a window UI.
- Enabled tasks start automatically when the app starts.
- Run-once tasks record their exit results.
- Long-running tasks can be stopped manually and are not auto-restarted.
- Administrator autostart is implemented through Windows Task Scheduler.
- After autostart is enabled once, login startup does not require manual UAC approval.
- Configuration and logs are stored in user-local app data locations.
