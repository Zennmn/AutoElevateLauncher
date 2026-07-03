# Auto Elevate Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a lightweight Windows WinForms tray app that manages elevated logon startup for PowerShell scripts and executable programs through Windows scheduled tasks.

**Architecture:** The app has a normal manager mode for tray/UI and a hidden runner mode invoked by scheduled tasks. Startup item config lives in `%AppData%\AutoElevateLauncher\config.json`; each item maps to one elevated scheduled task that starts the same executable with `--run-item <id>`. Task creation uses generated Task Scheduler XML plus `schtasks.exe` to avoid external dependencies.

**Tech Stack:** C# 12, .NET 8 Windows Desktop, WinForms, `System.Text.Json`, xUnit, Windows Task Scheduler via `schtasks.exe`.

## Global Constraints

- Platform: Windows only.
- SDK prerequisite: .NET 8 SDK must be installed; current machine has .NET 8 runtime but no SDK.
- First delivery: portable executable, not installer.
- Managed startup items are intended to run elevated.
- Startup item types: PowerShell scripts (`.ps1`) and executable programs (`.exe`).
- Trigger: current user logon.
- Privilege: scheduled tasks run with highest privileges.
- Config path: `%AppData%\AutoElevateLauncher\config.json`.
- Log root: `%AppData%\AutoElevateLauncher\logs\`.
- Do not commit changes unless the user explicitly asks for a commit.

---

## File Structure

- Create `AutoElevateLauncher.sln`: solution containing app and tests.
- Create `src/AutoElevateLauncher/AutoElevateLauncher.csproj`: WinForms application project.
- Create `src/AutoElevateLauncher/Program.cs`: app entry point; dispatches manager mode vs runner mode.
- Create `src/AutoElevateLauncher/AppPaths.cs`: central paths under `%AppData%`.
- Create `src/AutoElevateLauncher/StartupItem.cs`: startup item model and enums.
- Create `src/AutoElevateLauncher/StartupConfig.cs`: config root model.
- Create `src/AutoElevateLauncher/ConfigStore.cs`: JSON load/save with safe directory creation.
- Create `src/AutoElevateLauncher/StartupItemValidator.cs`: validates item path/type/working directory.
- Create `src/AutoElevateLauncher/TaskXmlBuilder.cs`: produces Task Scheduler XML.
- Create `src/AutoElevateLauncher/ProcessCommand.cs`: command result model for `schtasks.exe` and launched targets.
- Create `src/AutoElevateLauncher/ProcessRunner.cs`: starts external commands and captures output when requested.
- Create `src/AutoElevateLauncher/ScheduledTaskService.cs`: create/update/delete/enable/disable/run/stop scheduled tasks.
- Create `src/AutoElevateLauncher/ItemRunner.cs`: launches configured `.ps1` and `.exe` items and writes logs/status.
- Create `src/AutoElevateLauncher/ManagerContext.cs`: tray application context and tray menu.
- Create `src/AutoElevateLauncher/MainForm.cs`: split manager UI.
- Create `tests/AutoElevateLauncher.Tests/AutoElevateLauncher.Tests.csproj`: xUnit test project.
- Create `tests/AutoElevateLauncher.Tests/StartupItemValidatorTests.cs`: validation tests.
- Create `tests/AutoElevateLauncher.Tests/TaskXmlBuilderTests.cs`: task XML tests.
- Create `tests/AutoElevateLauncher.Tests/ItemRunnerCommandTests.cs`: PowerShell/executable command tests.
- Modify `docs/superpowers/specs/2026-07-04-auto-elevate-launcher-design.md` only if implementation discovers a spec contradiction.

---

### Task 1: Project Scaffold And Domain Models

**Files:**
- Create: `AutoElevateLauncher.sln`
- Create: `src/AutoElevateLauncher/AutoElevateLauncher.csproj`
- Create: `src/AutoElevateLauncher/Program.cs`
- Create: `src/AutoElevateLauncher/AppPaths.cs`
- Create: `src/AutoElevateLauncher/StartupItem.cs`
- Create: `src/AutoElevateLauncher/StartupConfig.cs`
- Create: `src/AutoElevateLauncher/ConfigStore.cs`
- Create: `tests/AutoElevateLauncher.Tests/AutoElevateLauncher.Tests.csproj`

**Interfaces:**
- Produces: `StartupItem`, `StartupItemType`, `StartupItemStatus`, `TaskSyncStatus`, `StartupConfig`, `ConfigStore.Load()`, `ConfigStore.Save(StartupConfig config)`, `AppPaths.ConfigFile`, `AppPaths.LogsDirectory`.
- Consumes: no project code from earlier tasks.

- [ ] **Step 1: Install .NET 8 SDK if missing**

Run:

```powershell
dotnet --list-sdks
```

Expected if SDK is missing on this machine: no output.

Install command:

```powershell
winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
```

Verify:

```powershell
dotnet --list-sdks
```

Expected: a line starting with `8.0.`.

- [ ] **Step 2: Create solution and projects**

Run:

```powershell
dotnet new sln -n AutoElevateLauncher
dotnet new winforms -n AutoElevateLauncher -o src\AutoElevateLauncher --framework net8.0-windows
dotnet new xunit -n AutoElevateLauncher.Tests -o tests\AutoElevateLauncher.Tests --framework net8.0
dotnet sln AutoElevateLauncher.sln add src\AutoElevateLauncher\AutoElevateLauncher.csproj
dotnet sln AutoElevateLauncher.sln add tests\AutoElevateLauncher.Tests\AutoElevateLauncher.Tests.csproj
dotnet add tests\AutoElevateLauncher.Tests\AutoElevateLauncher.Tests.csproj reference src\AutoElevateLauncher\AutoElevateLauncher.csproj
```

Expected: solution and both projects are created and referenced.

- [ ] **Step 3: Configure app project for tests and WinForms**

Replace `src/AutoElevateLauncher/AutoElevateLauncher.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>AutoElevateLauncher</AssemblyName>
    <RootNamespace>AutoElevateLauncher</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Add path helper**

Create `src/AutoElevateLauncher/AppPaths.cs`:

```csharp
namespace AutoElevateLauncher;

public static class AppPaths
{
    public static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoElevateLauncher");

    public static string ConfigFile => Path.Combine(AppDataDirectory, "config.json");

    public static string LogsDirectory => Path.Combine(AppDataDirectory, "logs");

    public static string GetItemLogDirectory(string itemId) => Path.Combine(LogsDirectory, itemId);
}
```

- [ ] **Step 5: Add startup item models**

Create `src/AutoElevateLauncher/StartupItem.cs`:

```csharp
namespace AutoElevateLauncher;

public enum StartupItemType
{
    PowerShellScript,
    Executable
}

public enum StartupItemStatus
{
    NeverRun,
    Running,
    Succeeded,
    Failed,
    Unknown
}

public enum TaskSyncStatus
{
    NotCreated,
    Synchronized,
    Failed
}

public sealed class StartupItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public StartupItemType Type { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string TaskName { get; set; } = string.Empty;
    public TaskSyncStatus TaskSyncStatus { get; set; } = TaskSyncStatus.NotCreated;
    public string LastTaskError { get; set; } = string.Empty;
    public DateTimeOffset? LastRunStartedAt { get; set; }
    public DateTimeOffset? LastRunFinishedAt { get; set; }
    public int? LastExitCode { get; set; }
    public StartupItemStatus LastStatus { get; set; } = StartupItemStatus.NeverRun;

    public void EnsureTaskName()
    {
        if (string.IsNullOrWhiteSpace(TaskName))
        {
            TaskName = $"AutoElevateLauncher-{Id}";
        }
    }
}
```

Create `src/AutoElevateLauncher/StartupConfig.cs`:

```csharp
namespace AutoElevateLauncher;

public sealed class StartupConfig
{
    public List<StartupItem> Items { get; set; } = [];
    public bool StartManagerAtLogin { get; set; } = true;
}
```

- [ ] **Step 6: Add config store**

Create `src/AutoElevateLauncher/ConfigStore.cs`:

```csharp
using System.Text.Json;

namespace AutoElevateLauncher;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public StartupConfig Load()
    {
        if (!File.Exists(AppPaths.ConfigFile))
        {
            return new StartupConfig();
        }

        var json = File.ReadAllText(AppPaths.ConfigFile);
        return JsonSerializer.Deserialize<StartupConfig>(json, JsonOptions) ?? new StartupConfig();
    }

    public void Save(StartupConfig config)
    {
        Directory.CreateDirectory(AppPaths.AppDataDirectory);
        Directory.CreateDirectory(AppPaths.LogsDirectory);

        foreach (var item in config.Items)
        {
            item.EnsureTaskName();
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(AppPaths.ConfigFile, json);
    }
}
```

- [ ] **Step 7: Add minimal program entry point**

Replace `src/AutoElevateLauncher/Program.cs` with:

```csharp
namespace AutoElevateLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length == 2 && args[0] == "--run-item")
        {
            MessageBox.Show($"Runner mode will execute item {args[1]} after Task 4 is complete.", "Auto Elevate Launcher");
            return 0;
        }

        Application.Run(new Form { Text = "Auto Elevate Launcher", Width = 900, Height = 600 });
        return 0;
    }
}
```

- [ ] **Step 8: Build and test scaffold**

Run:

```powershell
dotnet build AutoElevateLauncher.sln
dotnet test AutoElevateLauncher.sln
```

Expected: build succeeds and default xUnit test passes.

- [ ] **Step 9: Record changed files**

Run:

```powershell
git status --short
```

Expected: new solution, app project, test project, and source files are listed. Do not commit unless the user explicitly asks.

---

### Task 2: Validation And Task XML Generation

**Files:**
- Create: `src/AutoElevateLauncher/StartupItemValidator.cs`
- Create: `src/AutoElevateLauncher/TaskXmlBuilder.cs`
- Create: `tests/AutoElevateLauncher.Tests/StartupItemValidatorTests.cs`
- Create: `tests/AutoElevateLauncher.Tests/TaskXmlBuilderTests.cs`

**Interfaces:**
- Consumes: `StartupItem`, `StartupItemType`, `AppPaths`.
- Produces: `StartupItemValidator.Validate(StartupItem item)`, `ValidationResult`, `TaskXmlBuilder.BuildStartupItemTaskXml(StartupItem item, string appExePath, string userId)`.

- [ ] **Step 1: Write failing validator tests**

Create `tests/AutoElevateLauncher.Tests/StartupItemValidatorTests.cs`:

```csharp
using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class StartupItemValidatorTests
{
    [Fact]
    public void Validate_AcceptsExistingPowerShellScript()
    {
        var script = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        File.WriteAllText(script, "Write-Output 'ok'");

        var item = new StartupItem
        {
            Name = "Script",
            Type = StartupItemType.PowerShellScript,
            Path = script,
            WorkingDirectory = Path.GetDirectoryName(script)!
        };

        var result = StartupItemValidator.Validate(item);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_RejectsWrongExtensionForExecutable()
    {
        var script = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        File.WriteAllText(script, "Write-Output 'ok'");

        var item = new StartupItem
        {
            Name = "Wrong Type",
            Type = StartupItemType.Executable,
            Path = script,
            WorkingDirectory = Path.GetDirectoryName(script)!
        };

        var result = StartupItemValidator.Validate(item);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains(".exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsMissingPath()
    {
        var item = new StartupItem
        {
            Name = "Missing",
            Type = StartupItemType.PowerShellScript,
            Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1")
        };

        var result = StartupItemValidator.Validate(item);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 2: Run validator tests and verify failure**

Run:

```powershell
dotnet test tests\AutoElevateLauncher.Tests\AutoElevateLauncher.Tests.csproj --filter StartupItemValidatorTests
```

Expected: fails because `StartupItemValidator` does not exist.

- [ ] **Step 3: Implement validator**

Create `src/AutoElevateLauncher/StartupItemValidator.cs`:

```csharp
namespace AutoElevateLauncher;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success { get; } = new(true, []);
}

public static class StartupItemValidator
{
    public static ValidationResult Validate(StartupItem item)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            errors.Add("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            errors.Add("Path is required.");
        }
        else if (!File.Exists(item.Path))
        {
            errors.Add($"Path does not exist: {item.Path}");
        }
        else
        {
            var extension = System.IO.Path.GetExtension(item.Path);
            if (item.Type == StartupItemType.PowerShellScript && !extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("PowerShell script items must use a .ps1 file.");
            }

            if (item.Type == StartupItemType.Executable && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Executable items must use a .exe file.");
            }
        }

        if (!string.IsNullOrWhiteSpace(item.WorkingDirectory) && !Directory.Exists(item.WorkingDirectory))
        {
            errors.Add($"Working directory does not exist: {item.WorkingDirectory}");
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(false, errors);
    }
}
```

- [ ] **Step 4: Write failing task XML tests**

Create `tests/AutoElevateLauncher.Tests/TaskXmlBuilderTests.cs`:

```csharp
using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class TaskXmlBuilderTests
{
    [Fact]
    public void BuildStartupItemTaskXml_UsesHighestAvailablePrivilegesAndLogonTrigger()
    {
        var item = new StartupItem { Id = "abc", Name = "Demo", Enabled = true };
        item.EnsureTaskName();

        var xml = TaskXmlBuilder.BuildStartupItemTaskXml(item, "C:\\Tools\\AutoElevateLauncher.exe", "TEST-PC\\me");

        Assert.Contains("<LogonTrigger>", xml);
        Assert.Contains("<RunLevel>HighestAvailable</RunLevel>", xml);
        Assert.Contains("<Command>C:\\Tools\\AutoElevateLauncher.exe</Command>", xml);
        Assert.Contains("<Arguments>--run-item abc</Arguments>", xml);
        Assert.Contains("<UserId>TEST-PC\\me</UserId>", xml);
    }

    [Fact]
    public void BuildStartupItemTaskXml_DisablesTaskWhenItemDisabled()
    {
        var item = new StartupItem { Id = "abc", Name = "Demo", Enabled = false };
        item.EnsureTaskName();

        var xml = TaskXmlBuilder.BuildStartupItemTaskXml(item, "C:\\Tools\\AutoElevateLauncher.exe", "TEST-PC\\me");

        Assert.Contains("<Enabled>false</Enabled>", xml);
    }
}
```

- [ ] **Step 5: Run task XML tests and verify failure**

Run:

```powershell
dotnet test tests\AutoElevateLauncher.Tests\AutoElevateLauncher.Tests.csproj --filter TaskXmlBuilderTests
```

Expected: fails because `TaskXmlBuilder` does not exist.

- [ ] **Step 6: Implement task XML builder**

Create `src/AutoElevateLauncher/TaskXmlBuilder.cs`:

```csharp
using System.Security;
using System.Text;

namespace AutoElevateLauncher;

public static class TaskXmlBuilder
{
    public static string BuildStartupItemTaskXml(StartupItem item, string appExePath, string userId)
    {
        item.EnsureTaskName();
        var enabled = item.Enabled ? "true" : "false";

        return $$"""
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Description>Auto Elevate Launcher startup item: {{Escape(item.Name)}}</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{{Escape(userId)}}</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>{{Escape(userId)}}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>{{enabled}}</Enabled>
    <Hidden>false</Hidden>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>{{Escape(appExePath)}}</Command>
      <Arguments>--run-item {{Escape(item.Id)}}</Arguments>
    </Exec>
  </Actions>
</Task>
""";
    }

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }
}
```

- [ ] **Step 7: Run validation and XML tests**

Run:

```powershell
dotnet test tests\AutoElevateLauncher.Tests\AutoElevateLauncher.Tests.csproj --filter "StartupItemValidatorTests|TaskXmlBuilderTests"
```

Expected: all tests pass.

- [ ] **Step 8: Record changed files**

Run:

```powershell
git status --short
```

Expected: validator, XML builder, and tests are listed. Do not commit unless the user explicitly asks.

---

### Task 3: Scheduled Task Service

**Files:**
- Create: `src/AutoElevateLauncher/ProcessCommand.cs`
- Create: `src/AutoElevateLauncher/ProcessRunner.cs`
- Create: `src/AutoElevateLauncher/ScheduledTaskService.cs`

**Interfaces:**
- Consumes: `StartupItem`, `TaskXmlBuilder`.
- Produces: `ProcessCommandResult`, `IProcessRunner.RunAsync(...)`, `ScheduledTaskService.CreateOrUpdateStartupItemTaskAsync(...)`, `DeleteTaskAsync(...)`, `RunTaskAsync(...)`, `StopTaskAsync(...)`.

- [ ] **Step 1: Add process command model**

Create `src/AutoElevateLauncher/ProcessCommand.cs`:

```csharp
namespace AutoElevateLauncher;

public sealed record ProcessCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
```

- [ ] **Step 2: Add process runner**

Create `src/AutoElevateLauncher/ProcessRunner.cs`:

```csharp
using System.Diagnostics;

namespace AutoElevateLauncher;

public interface IProcessRunner
{
    Task<ProcessCommandResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken cancellationToken = default);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessCommandResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessCommandResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
```

- [ ] **Step 3: Add scheduled task service**

Create `src/AutoElevateLauncher/ScheduledTaskService.cs`:

```csharp
using System.Security.Principal;
using System.Text;

namespace AutoElevateLauncher;

public sealed class ScheduledTaskService
{
    private readonly IProcessRunner _processRunner;

    public ScheduledTaskService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<ProcessCommandResult> CreateOrUpdateStartupItemTaskAsync(StartupItem item, string appExePath, CancellationToken cancellationToken = default)
    {
        item.EnsureTaskName();
        var userId = WindowsIdentity.GetCurrent().Name;
        var xml = TaskXmlBuilder.BuildStartupItemTaskXml(item, appExePath, userId);
        var xmlPath = Path.Combine(Path.GetTempPath(), item.TaskName + ".xml");
        await File.WriteAllTextAsync(xmlPath, xml, Encoding.Unicode, cancellationToken);

        try
        {
            return await _processRunner.RunAsync("schtasks.exe", $"/Create /TN \"{item.TaskName}\" /XML \"{xmlPath}\" /F", null, cancellationToken);
        }
        finally
        {
            TryDelete(xmlPath);
        }
    }

    public Task<ProcessCommandResult> DeleteTaskAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        item.EnsureTaskName();
        return _processRunner.RunAsync("schtasks.exe", $"/Delete /TN \"{item.TaskName}\" /F", null, cancellationToken);
    }

    public Task<ProcessCommandResult> RunTaskAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        item.EnsureTaskName();
        return _processRunner.RunAsync("schtasks.exe", $"/Run /TN \"{item.TaskName}\"", null, cancellationToken);
    }

    public Task<ProcessCommandResult> StopTaskAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        item.EnsureTaskName();
        return _processRunner.RunAsync("schtasks.exe", $"/End /TN \"{item.TaskName}\"", null, cancellationToken);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary task XML cleanup failure should not hide the schtasks result.
        }
    }
}
```

- [ ] **Step 4: Build project**

Run:

```powershell
dotnet build AutoElevateLauncher.sln
```

Expected: build succeeds.

- [ ] **Step 5: Record changed files**

Run:

```powershell
git status --short
```

Expected: scheduled task service and process runner files are listed. Do not commit unless the user explicitly asks.

---

### Task 4: Runner Mode And Launch Logging

**Files:**
- Create: `src/AutoElevateLauncher/ItemRunner.cs`
- Create: `tests/AutoElevateLauncher.Tests/ItemRunnerCommandTests.cs`
- Modify: `src/AutoElevateLauncher/Program.cs`

**Interfaces:**
- Consumes: `ConfigStore`, `StartupItem`, `StartupItemType`, `ProcessRunner`.
- Produces: `ItemRunner.RunAsync(string itemId, CancellationToken cancellationToken)`, `ItemRunner.BuildPowerShellArguments(StartupItem item)`, `ItemRunner.BuildExecutableStartInfo(StartupItem item)`.

- [ ] **Step 1: Write failing command construction tests**

Create `tests/AutoElevateLauncher.Tests/ItemRunnerCommandTests.cs`:

```csharp
using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class ItemRunnerCommandTests
{
    [Fact]
    public void BuildPowerShellArguments_PreservesScriptPathAndUserArguments()
    {
        var item = new StartupItem
        {
            Type = StartupItemType.PowerShellScript,
            Path = "D:\\Scripts\\hello world.ps1",
            Arguments = "-Mode Auto -Config \"D:\\cfg\\a b.json\""
        };

        var arguments = ItemRunner.BuildPowerShellArguments(item);

        Assert.Contains("-ExecutionPolicy Bypass", arguments);
        Assert.Contains("-File \"D:\\Scripts\\hello world.ps1\"", arguments);
        Assert.Contains("-Mode Auto -Config \"D:\\cfg\\a b.json\"", arguments);
    }

    [Fact]
    public void BuildExecutableStartInfo_UsesExecutablePathArgumentsAndWorkingDirectory()
    {
        var item = new StartupItem
        {
            Type = StartupItemType.Executable,
            Path = "C:\\Tools\\app.exe",
            Arguments = "--silent --profile default",
            WorkingDirectory = "C:\\Tools"
        };

        var startInfo = ItemRunner.BuildExecutableStartInfo(item);

        Assert.Equal("C:\\Tools\\app.exe", startInfo.FileName);
        Assert.Equal("--silent --profile default", startInfo.Arguments);
        Assert.Equal("C:\\Tools", startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
    }
}
```

- [ ] **Step 2: Run command tests and verify failure**

Run:

```powershell
dotnet test tests\AutoElevateLauncher.Tests\AutoElevateLauncher.Tests.csproj --filter ItemRunnerCommandTests
```

Expected: fails because `ItemRunner` does not exist.

- [ ] **Step 3: Implement item runner**

Create `src/AutoElevateLauncher/ItemRunner.cs`:

```csharp
using System.Diagnostics;

namespace AutoElevateLauncher;

public sealed class ItemRunner
{
    private readonly ConfigStore _configStore;

    public ItemRunner(ConfigStore configStore)
    {
        _configStore = configStore;
    }

    public async Task<int> RunAsync(string itemId, CancellationToken cancellationToken = default)
    {
        var config = _configStore.Load();
        var item = config.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            return 2;
        }

        Directory.CreateDirectory(AppPaths.GetItemLogDirectory(item.Id));
        var logPath = Path.Combine(AppPaths.GetItemLogDirectory(item.Id), DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + ".log");

        item.LastRunStartedAt = DateTimeOffset.Now;
        item.LastRunFinishedAt = null;
        item.LastExitCode = null;
        item.LastStatus = StartupItemStatus.Running;
        _configStore.Save(config);

        try
        {
            var exitCode = item.Type == StartupItemType.PowerShellScript
                ? await RunPowerShellAsync(item, logPath, cancellationToken)
                : await RunExecutableAsync(item, logPath, cancellationToken);

            item.LastRunFinishedAt = DateTimeOffset.Now;
            item.LastExitCode = exitCode;
            item.LastStatus = exitCode == 0 ? StartupItemStatus.Succeeded : StartupItemStatus.Failed;
            _configStore.Save(config);
            return exitCode;
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync(logPath, Environment.NewLine + ex, cancellationToken);
            item.LastRunFinishedAt = DateTimeOffset.Now;
            item.LastExitCode = -1;
            item.LastStatus = StartupItemStatus.Failed;
            _configStore.Save(config);
            return -1;
        }
    }

    public static string BuildPowerShellArguments(StartupItem item)
    {
        var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{item.Path}\"";
        return string.IsNullOrWhiteSpace(item.Arguments) ? args : args + " " + item.Arguments;
    }

    public static ProcessStartInfo BuildExecutableStartInfo(StartupItem item)
    {
        return new ProcessStartInfo
        {
            FileName = item.Path,
            Arguments = item.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(item.WorkingDirectory) ? Path.GetDirectoryName(item.Path) ?? Environment.CurrentDirectory : item.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true
        };
    }

    private static async Task<int> RunPowerShellAsync(StartupItem item, string logPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = BuildPowerShellArguments(item),
            WorkingDirectory = string.IsNullOrWhiteSpace(item.WorkingDirectory) ? Path.GetDirectoryName(item.Path) ?? Environment.CurrentDirectory : item.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        await File.WriteAllTextAsync(logPath, BuildHeader(item, startInfo), cancellationToken);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start powershell.exe.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        await File.AppendAllTextAsync(logPath,
            $"{Environment.NewLine}--- stdout ---{Environment.NewLine}{await stdoutTask}{Environment.NewLine}--- stderr ---{Environment.NewLine}{await stderrTask}{Environment.NewLine}ExitCode: {process.ExitCode}{Environment.NewLine}",
            cancellationToken);

        return process.ExitCode;
    }

    private static async Task<int> RunExecutableAsync(StartupItem item, string logPath, CancellationToken cancellationToken)
    {
        var startInfo = BuildExecutableStartInfo(item);
        await File.WriteAllTextAsync(logPath, BuildHeader(item, startInfo), cancellationToken);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {item.Path}.");
        await File.AppendAllTextAsync(logPath, $"ProcessId: {process.Id}{Environment.NewLine}", cancellationToken);
        return 0;
    }

    private static string BuildHeader(StartupItem item, ProcessStartInfo startInfo)
    {
        return $"StartTime: {DateTimeOffset.Now:O}{Environment.NewLine}Type: {item.Type}{Environment.NewLine}Target: {startInfo.FileName}{Environment.NewLine}WorkingDirectory: {startInfo.WorkingDirectory}{Environment.NewLine}Arguments: {startInfo.Arguments}{Environment.NewLine}";
    }
}
```

- [ ] **Step 4: Wire runner mode into Program.cs**

Replace `src/AutoElevateLauncher/Program.cs` with:

```csharp
namespace AutoElevateLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--run-item")
        {
            return new ItemRunner(new ConfigStore()).RunAsync(args[1]).GetAwaiter().GetResult();
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new Form { Text = "Auto Elevate Launcher", Width = 900, Height = 600 });
        return 0;
    }
}
```

- [ ] **Step 5: Run command tests**

Run:

```powershell
dotnet test tests\AutoElevateLauncher.Tests\AutoElevateLauncher.Tests.csproj --filter ItemRunnerCommandTests
```

Expected: tests pass.

- [ ] **Step 6: Build solution**

Run:

```powershell
dotnet build AutoElevateLauncher.sln
```

Expected: build succeeds.

- [ ] **Step 7: Record changed files**

Run:

```powershell
git status --short
```

Expected: runner and Program.cs changes are listed. Do not commit unless the user explicitly asks.

---

### Task 5: Manager Tray And Split UI

**Files:**
- Create: `src/AutoElevateLauncher/ManagerContext.cs`
- Create: `src/AutoElevateLauncher/MainForm.cs`
- Modify: `src/AutoElevateLauncher/Program.cs`

**Interfaces:**
- Consumes: `ConfigStore`, `ScheduledTaskService`, `StartupItemValidator`, `StartupItem`.
- Produces: tray icon, split manager window, add script, add program, save, delete, run now, stop, open logs.

- [ ] **Step 1: Create tray application context**

Create `src/AutoElevateLauncher/ManagerContext.cs`:

```csharp
namespace AutoElevateLauncher;

public sealed class ManagerContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private MainForm? _mainForm;

    public ManagerContext()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Auto Elevate Launcher",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => ShowManager();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open manager", null, (_, _) => ShowManager());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private void ShowManager()
    {
        if (_mainForm is null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(new ConfigStore(), new ScheduledTaskService(new ProcessRunner()));
        }

        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.Activate();
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.ExitThreadCore();
    }
}
```

- [ ] **Step 2: Create split manager form**

Create `src/AutoElevateLauncher/MainForm.cs`:

```csharp
using System.Diagnostics;

namespace AutoElevateLauncher;

public sealed class MainForm : Form
{
    private readonly ConfigStore _configStore;
    private readonly ScheduledTaskService _taskService;
    private StartupConfig _config;
    private readonly ListBox _items = new() { Dock = DockStyle.Fill, DisplayMember = nameof(StartupItem.Name) };
    private readonly TextBox _name = new() { Dock = DockStyle.Top };
    private readonly TextBox _path = new() { Dock = DockStyle.Top };
    private readonly TextBox _arguments = new() { Dock = DockStyle.Top };
    private readonly TextBox _workingDirectory = new() { Dock = DockStyle.Top };
    private readonly CheckBox _enabled = new() { Text = "Enabled", Dock = DockStyle.Top };
    private readonly Label _status = new() { Dock = DockStyle.Top, AutoSize = true };

    public MainForm(ConfigStore configStore, ScheduledTaskService taskService)
    {
        _configStore = configStore;
        _taskService = taskService;
        _config = _configStore.Load();

        Text = "Auto Elevate Launcher";
        Width = 1000;
        Height = 650;
        BuildLayout();
        RefreshList();
    }

    private void BuildLayout()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 360 };
        Controls.Add(split);

        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72 };
        var addScript = new Button { Text = "Add script", Width = 100 };
        var addProgram = new Button { Text = "Add program", Width = 110 };
        var delete = new Button { Text = "Delete", Width = 90 };
        addScript.Click += (_, _) => AddItem(StartupItemType.PowerShellScript);
        addProgram.Click += (_, _) => AddItem(StartupItemType.Executable);
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        leftButtons.Controls.AddRange([addScript, addProgram, delete]);

        split.Panel1.Controls.Add(_items);
        split.Panel1.Controls.Add(leftButtons);
        _items.SelectedIndexChanged += (_, _) => LoadSelectedIntoDetails();

        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 14, Padding = new Padding(12) };
        split.Panel2.Controls.Add(details);
        AddLabeled(details, "Name", _name);
        AddLabeled(details, "Path", _path);
        AddLabeled(details, "Arguments", _arguments);
        AddLabeled(details, "Working directory", _workingDirectory);
        details.Controls.Add(_enabled);
        details.Controls.Add(_status);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        var save = new Button { Text = "Save", Width = 90 };
        var run = new Button { Text = "Run now", Width = 90 };
        var stop = new Button { Text = "Stop", Width = 90 };
        var logs = new Button { Text = "Open logs", Width = 100 };
        save.Click += async (_, _) => await SaveSelectedAsync();
        run.Click += async (_, _) => await RunSelectedAsync();
        stop.Click += async (_, _) => await StopSelectedAsync();
        logs.Click += (_, _) => OpenSelectedLogs();
        actions.Controls.AddRange([save, run, stop, logs]);
        details.Controls.Add(actions);
    }

    private static void AddLabeled(Control parent, string label, Control control)
    {
        parent.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, AutoSize = true });
        parent.Controls.Add(control);
    }

    private void AddItem(StartupItemType type)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = type == StartupItemType.PowerShellScript ? "PowerShell scripts (*.ps1)|*.ps1" : "Programs (*.exe)|*.exe"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var item = new StartupItem
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            Type = type,
            Path = dialog.FileName,
            WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty,
            Enabled = true
        };
        item.EnsureTaskName();
        _config.Items.Add(item);
        _configStore.Save(_config);
        RefreshList();
        _items.SelectedItem = item;
    }

    private StartupItem? SelectedItem => _items.SelectedItem as StartupItem;

    private void RefreshList()
    {
        _items.DataSource = null;
        _items.DataSource = _config.Items;
    }

    private void LoadSelectedIntoDetails()
    {
        var item = SelectedItem;
        if (item is null) return;
        _name.Text = item.Name;
        _path.Text = item.Path;
        _arguments.Text = item.Arguments;
        _workingDirectory.Text = item.WorkingDirectory;
        _enabled.Checked = item.Enabled;
        _status.Text = $"Status: {item.LastStatus}; Task: {item.TaskSyncStatus}; Exit: {item.LastExitCode?.ToString() ?? ""}";
    }

    private async Task SaveSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        item.Name = _name.Text.Trim();
        item.Path = _path.Text.Trim();
        item.Arguments = _arguments.Text;
        item.WorkingDirectory = _workingDirectory.Text.Trim();
        item.Enabled = _enabled.Checked;

        var validation = StartupItemValidator.Validate(item);
        if (!validation.IsValid)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, validation.Errors), "Validation failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = await _taskService.CreateOrUpdateStartupItemTaskAsync(item, Application.ExecutablePath);
        item.TaskSyncStatus = result.Succeeded ? TaskSyncStatus.Synchronized : TaskSyncStatus.Failed;
        item.LastTaskError = result.Succeeded ? string.Empty : result.StandardError + result.StandardOutput;
        _configStore.Save(_config);
        RefreshList();
        LoadSelectedIntoDetails();
    }

    private async Task DeleteSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        await _taskService.DeleteTaskAsync(item);
        _config.Items.Remove(item);
        _configStore.Save(_config);
        RefreshList();
    }

    private async Task RunSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        await _taskService.RunTaskAsync(item);
    }

    private async Task StopSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        await _taskService.StopTaskAsync(item);
    }

    private void OpenSelectedLogs()
    {
        var item = SelectedItem;
        if (item is null) return;
        var directory = AppPaths.GetItemLogDirectory(item.Id);
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }
}
```

- [ ] **Step 3: Wire manager context into Program.cs**

Replace `src/AutoElevateLauncher/Program.cs` with:

```csharp
namespace AutoElevateLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--run-item")
        {
            return new ItemRunner(new ConfigStore()).RunAsync(args[1]).GetAwaiter().GetResult();
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new ManagerContext());
        return 0;
    }
}
```

- [ ] **Step 4: Build solution**

Run:

```powershell
dotnet build AutoElevateLauncher.sln
```

Expected: build succeeds.

- [ ] **Step 5: Run app manually**

Run:

```powershell
dotnet run --project src\AutoElevateLauncher\AutoElevateLauncher.csproj
```

Expected: tray icon appears. Double-clicking opens the manager window. Adding a `.ps1` or `.exe` creates an item. Saving an item may show one UAC prompt for scheduled task creation.

- [ ] **Step 6: Record changed files**

Run:

```powershell
git status --short
```

Expected: tray and UI files are listed. Do not commit unless the user explicitly asks.

---

### Task 6: Verification And Portable Publish

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: finished app behavior from Tasks 1-5.
- Produces: verified portable Windows executable and usage notes.

- [ ] **Step 1: Run full automated tests**

Run:

```powershell
dotnet test AutoElevateLauncher.sln
```

Expected: all tests pass.

- [ ] **Step 2: Run full build**

Run:

```powershell
dotnet build AutoElevateLauncher.sln -c Release
```

Expected: release build succeeds.

- [ ] **Step 3: Publish portable executable**

Run:

```powershell
dotnet publish src\AutoElevateLauncher\AutoElevateLauncher.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish\win-x64
```

Expected: `publish\win-x64\AutoElevateLauncher.exe` exists.

- [ ] **Step 4: Create smoke test PowerShell script**

Create a temporary script outside the repo at `%TEMP%\auto-elevate-smoke.ps1`:

```powershell
'started ' + (Get-Date).ToString('O') | Out-File -FilePath "$env:TEMP\auto-elevate-smoke-output.txt" -Append
Write-Output "stdout smoke"
Write-Error "stderr smoke"
exit 3
```

- [ ] **Step 5: Manual smoke test elevated script task**

Run the published app:

```powershell
publish\win-x64\AutoElevateLauncher.exe
```

Expected:

- Tray icon appears.
- Manager window opens on tray double-click.
- Add `%TEMP%\auto-elevate-smoke.ps1` as a script item.
- Save creates a scheduled task after a UAC confirmation.
- `Run now` starts the scheduled task.
- A log appears under `%AppData%\AutoElevateLauncher\logs\<item-id>\`.
- The log contains `stdout smoke`, `stderr smoke`, and `ExitCode: 3`.

- [ ] **Step 6: Manual smoke test executable task**

Add `C:\Windows\System32\notepad.exe` as a program item with empty arguments.

Expected:

- Save creates a scheduled task after a UAC confirmation if Windows requests one.
- `Run now` starts Notepad elevated through the scheduled task.
- `Stop` ends the scheduled task instance if it is still associated with the task.
- The log records the process ID or a launch exception.

- [ ] **Step 7: Add README usage notes**

Create `README.md`:

```markdown
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
```

- [ ] **Step 8: Final verification**

Run:

```powershell
dotnet test AutoElevateLauncher.sln
dotnet publish src\AutoElevateLauncher\AutoElevateLauncher.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish\win-x64
```

Expected: tests pass and publish succeeds.

- [ ] **Step 9: Record final changed files**

Run:

```powershell
git status --short
```

Expected: source, tests, docs, README, and publish output are visible. Do not commit unless the user explicitly asks.

---

## Self-Review Notes

- Spec coverage: tasks cover WinForms tray app, split manager UI, config in `%AppData%`, multiple items, `.ps1` and `.exe`, per-item arguments and working directory, elevated scheduled tasks, runner mode, logs, and portable publish.
- Type consistency: `StartupItem`, `ConfigStore`, `ScheduledTaskService`, and `ItemRunner` signatures are defined before use by later tasks.
- Scope: this is one cohesive first version; installer packaging, PowerShell 7 selection, log cleanup, startup delay, and import/export remain outside first implementation.
