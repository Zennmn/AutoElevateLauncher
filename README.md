# Auto Elevate Launcher

Lightweight Windows tray app for automatically starting PowerShell scripts and executable programs with administrator privileges after user login.

## Requirements

- Windows
- .NET 8 Desktop Runtime for framework-dependent publish builds

## How It Works

Each startup item maps to a Windows scheduled task configured to run with highest privileges at current user logon. Creating or updating an elevated task can show a UAC prompt once. Later logon-triggered runs should not show UAC again.

## Data Locations

- Config: `%AppData%\AutoElevateLauncher\config.json`
- Logs: `%AppData%\AutoElevateLauncher\logs\`

## Usage

1. Start `AutoElevateLauncher.exe`.
2. Double-click the tray icon to open the manager.
3. Add a PowerShell script or executable program.
4. Fill in startup arguments and working directory if needed.
5. Save the item to create or update its scheduled task.
6. Use `Run now` and logs to verify behavior.