# Admin Tray Launcher Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert Auto Elevate Launcher into a Chinese administrator tray launcher that starts itself elevated at login and runs enabled startup items directly from that elevated process.

**Architecture:** Replace per-item scheduled task behavior with one manager self-start scheduled task. Add a direct item launcher and startup orchestrator, then update tray/UI code to use those services. Keep the existing WinForms/.NET shape, but make the manager UI Chinese and data-grid based.

**Tech Stack:** C# 12, .NET 8 Windows, WinForms, xUnit, Windows Task Scheduler via `schtasks.exe`, PowerShell 7 shell for verification commands.

## Global Constraints

- User-facing application copy must be Chinese.
- Use one scheduled task named `AutoElevateLauncher-Manager` for administrator self-start.
- New startup items are configuration records, not scheduled tasks.
- Config path remains `%AppData%\AutoElevateLauncher\config.json`.
- Logs path remains `%AppData%\AutoElevateLauncher\logs\`.
- Existing `TaskName` and `TaskSyncStatus` properties remain for config compatibility but are ignored by UI and launch logic.
- No installer, Windows service, plugin system, remote control, or per-item scheduled task creation.
- Keep changes minimal and focused; do not add third-party packages.
- Every implementation task must run `dotnet test "AutoElevateLauncher.sln" --nologo` before commit.

---

## File Structure

- Modify `src/AutoElevateLauncher/StartupConfig.cs`: default `StartManagerAtLogin` to `false`.
- Modify `src/AutoElevateLauncher/StartupItemValidator.cs`: return Chinese validation messages.
- Modify `src/AutoElevateLauncher/TaskXmlBuilder.cs`: make manager self-start XML use `HighestAvailable`; keep per-item builder only while tests still reference it, then stop using it from UI/service.
- Modify `src/AutoElevateLauncher/ConfigStore.cs`: add atomic save through temp file and replacement.
- Modify `src/AutoElevateLauncher/ProcessRunner.cs`: add elevated process execution support for helper commands.
- Modify `src/AutoElevateLauncher/ProcessCommand.cs`: no planned changes; keep the existing `Succeeded` behavior.
- Modify `src/AutoElevateLauncher/ScheduledTaskService.cs`: remove public per-item scheduled task usage from active code; add manager-only create/delete methods and helper command support.
- Modify `src/AutoElevateLauncher/ItemRunner.cs`: preserve argument builders; add direct launch against a supplied `StartupConfig` and `StartupItem`.
- Create `src/AutoElevateLauncher/IStartupItemLauncher.cs`: test seam for launching startup items.
- Create `src/AutoElevateLauncher/StartupOrchestrator.cs`: runs all enabled items once per process and isolates failures.
- Modify `src/AutoElevateLauncher/ManagerContext.cs`: Chinese tray menu, admin status, self-start toggle, run-all action, startup auto-run trigger.
- Modify `src/AutoElevateLauncher/MainForm.cs`: Chinese two-pane `DataGridView` UI and direct item operations.
- Modify `src/AutoElevateLauncher/Program.cs`: add internal elevated helper commands for enabling/disabling manager self-start.
- Modify `README.md`: rewrite user docs in Chinese.
- Add/modify tests under `tests/AutoElevateLauncher.Tests/` for defaults, validator copy, task XML, config save, launcher/orchestrator behavior, and command construction.

---

### Task 1: Defaults, Chinese Validation, Manager Task XML, Atomic Config Save

**Files:**
- Modify: `src/AutoElevateLauncher/StartupConfig.cs`
- Modify: `src/AutoElevateLauncher/StartupItemValidator.cs`
- Modify: `src/AutoElevateLauncher/TaskXmlBuilder.cs`
- Modify: `src/AutoElevateLauncher/ConfigStore.cs`
- Modify: `tests/AutoElevateLauncher.Tests/StartupItemValidatorTests.cs`
- Modify: `tests/AutoElevateLauncher.Tests/TaskXmlBuilderTests.cs`
- Create: `tests/AutoElevateLauncher.Tests/StartupConfigTests.cs`
- Create: `tests/AutoElevateLauncher.Tests/ConfigStoreTests.cs`

**Interfaces:**
- Consumes: existing `StartupConfig`, `StartupItemValidator.Validate(StartupItem)`, `TaskXmlBuilder.BuildManagerSelfStartTaskXml(string appExePath, string userId)`, `ConfigStore.Save(StartupConfig)`.
- Produces: `StartupConfig.StartManagerAtLogin == false` by default; Chinese validation messages; manager XML with `<RunLevel>HighestAvailable</RunLevel>`; atomic config write behavior.

- [ ] **Step 1: Add failing default-config test**

Create `tests/AutoElevateLauncher.Tests/StartupConfigTests.cs`:

```csharp
using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class StartupConfigTests
{
    [Fact]
    public void NewConfig_DisablesAdministratorSelfStartByDefault()
    {
        var config = new StartupConfig();

        Assert.False(config.StartManagerAtLogin);
    }
}
```

- [ ] **Step 2: Update validator tests to expect Chinese messages**

Replace `tests/AutoElevateLauncher.Tests/StartupItemValidatorTests.cs` with:

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

        try
        {
            var item = new StartupItem
            {
                Name = "脚本",
                Type = StartupItemType.PowerShellScript,
                Path = script,
                WorkingDirectory = Path.GetDirectoryName(script)!
            };

            var result = StartupItemValidator.Validate(item);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void Validate_RejectsMissingNameWithChineseMessage()
    {
        var item = new StartupItem
        {
            Name = "",
            Type = StartupItemType.PowerShellScript,
            Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1")
        };

        var result = StartupItemValidator.Validate(item);

        Assert.False(result.IsValid);
        Assert.Contains("名称不能为空。", result.Errors);
    }

    [Fact]
    public void Validate_RejectsWrongExtensionForExecutableWithChineseMessage()
    {
        var script = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        File.WriteAllText(script, "Write-Output 'ok'");

        try
        {
            var item = new StartupItem
            {
                Name = "类型错误",
                Type = StartupItemType.Executable,
                Path = script,
                WorkingDirectory = Path.GetDirectoryName(script)!
            };

            var result = StartupItemValidator.Validate(item);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("可执行程序项目必须使用 .exe 文件", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void Validate_RejectsMissingPathWithChineseMessage()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        var item = new StartupItem
        {
            Name = "缺失文件",
            Type = StartupItemType.PowerShellScript,
            Path = missingPath
        };

        var result = StartupItemValidator.Validate(item);

        Assert.False(result.IsValid);
        Assert.Contains($"文件不存在：{missingPath}", result.Errors);
    }
}
```

- [ ] **Step 3: Add manager XML highest privilege assertion**

Append this test to `tests/AutoElevateLauncher.Tests/TaskXmlBuilderTests.cs`:

```csharp
[Fact]
public void BuildManagerSelfStartTaskXml_UsesHighestAvailablePrivileges()
{
    var xml = TaskXmlBuilder.BuildManagerSelfStartTaskXml("C:\\Tools\\AutoElevateLauncher.exe", "TEST-PC\\me");

    Assert.Contains("<LogonTrigger>", xml);
    Assert.Contains("<RunLevel>HighestAvailable</RunLevel>", xml);
    Assert.Contains("<Command>C:\\Tools\\AutoElevateLauncher.exe</Command>", xml);
    Assert.DoesNotContain("<Arguments>", xml);
}
```

- [ ] **Step 4: Add config-store atomic save test**

Create `tests/AutoElevateLauncher.Tests/ConfigStoreTests.cs`:

```csharp
using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class ConfigStoreTests
{
    [Fact]
    public void Save_WritesConfigAndLeavesNoTempFileAfterSuccessfulSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AutoElevateLauncherTests", Guid.NewGuid().ToString("N"));
        var store = new ConfigStore(directory);
        var config = new StartupConfig
        {
            Items = [new StartupItem { Name = "测试", Path = "C:\\Tools\\demo.exe", Type = StartupItemType.Executable }]
        };

        try
        {
            store.Save(config);

            Assert.True(File.Exists(Path.Combine(directory, "config.json")));
            Assert.Empty(Directory.GetFiles(directory, "config.*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
```

- [ ] **Step 5: Run tests and verify failures**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: FAIL because `ConfigStore(string appDataDirectory)` does not exist, `StartManagerAtLogin` is still `true`, validator messages are English, and manager XML uses `LeastAvailable`.

- [ ] **Step 6: Implement default, Chinese validator, and manager XML changes**

Change `src/AutoElevateLauncher/StartupConfig.cs` to:

```csharp
namespace AutoElevateLauncher;

public sealed class StartupConfig
{
    public List<StartupItem> Items { get; set; } = [];
    public bool StartManagerAtLogin { get; set; } = false;
}
```

Change the message-producing parts of `src/AutoElevateLauncher/StartupItemValidator.cs` to:

```csharp
if (string.IsNullOrWhiteSpace(item.Name))
{
    errors.Add("名称不能为空。");
}

if (string.IsNullOrWhiteSpace(item.Path))
{
    errors.Add("路径不能为空。");
}
else if (!File.Exists(item.Path))
{
    errors.Add($"文件不存在：{item.Path}");
}
else
{
    var extension = System.IO.Path.GetExtension(item.Path);
    if (item.Type == StartupItemType.PowerShellScript && !extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
    {
        errors.Add("PowerShell 脚本项目必须使用 .ps1 文件。");
    }

    if (item.Type == StartupItemType.Executable && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
    {
        errors.Add("可执行程序项目必须使用 .exe 文件。");
    }
}

if (!string.IsNullOrWhiteSpace(item.WorkingDirectory) && !Directory.Exists(item.WorkingDirectory))
{
    errors.Add($"工作目录不存在：{item.WorkingDirectory}");
}
```

In `src/AutoElevateLauncher/TaskXmlBuilder.cs`, change the manager task run level:

```xml
<RunLevel>HighestAvailable</RunLevel>
```

- [ ] **Step 7: Make ConfigStore path-testable and implement atomic save**

Update the top of `src/AutoElevateLauncher/ConfigStore.cs` to add instance paths:

```csharp
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _appDataDirectory;
    private readonly string _configFile;
    private readonly string _logsDirectory;

    public ConfigStore() : this(AppPaths.AppDataDirectory)
    {
    }

    internal ConfigStore(string appDataDirectory)
    {
        _appDataDirectory = appDataDirectory;
        _configFile = Path.Combine(appDataDirectory, "config.json");
        _logsDirectory = Path.Combine(appDataDirectory, "logs");
    }
```

Replace all uses of `AppPaths.ConfigFile`, `AppPaths.AppDataDirectory`, and `AppPaths.LogsDirectory` inside `ConfigStore` with `_configFile`, `_appDataDirectory`, and `_logsDirectory`.

Replace `ConfigStore.Save` with:

```csharp
public void Save(StartupConfig config)
{
    Directory.CreateDirectory(_appDataDirectory);
    Directory.CreateDirectory(_logsDirectory);

    foreach (var item in config.Items)
    {
        item.EnsureTaskName();
    }

    var json = JsonSerializer.Serialize(config, JsonOptions);
    var tempPath = Path.Combine(_appDataDirectory, $"config.{Guid.NewGuid():N}.tmp");
    File.WriteAllText(tempPath, json);

    try
    {
        if (File.Exists(_configFile))
        {
            File.Replace(tempPath, _configFile, null);
        }
        else
        {
            File.Move(tempPath, _configFile);
        }
    }
    finally
    {
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
    }
}
```

- [ ] **Step 8: Run verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS, including the new startup config and manager XML tests.

- [ ] **Step 9: Commit**

Run:

```bash
git add src/AutoElevateLauncher/StartupConfig.cs src/AutoElevateLauncher/StartupItemValidator.cs src/AutoElevateLauncher/TaskXmlBuilder.cs src/AutoElevateLauncher/ConfigStore.cs tests/AutoElevateLauncher.Tests/StartupConfigTests.cs tests/AutoElevateLauncher.Tests/StartupItemValidatorTests.cs tests/AutoElevateLauncher.Tests/TaskXmlBuilderTests.cs tests/AutoElevateLauncher.Tests/ConfigStoreTests.cs
git commit -m "fix: localize validation and manager task defaults"
```

---

### Task 2: Direct Item Launcher Interface

**Files:**
- Create: `src/AutoElevateLauncher/IStartupItemLauncher.cs`
- Modify: `src/AutoElevateLauncher/ItemRunner.cs`
- Modify: `tests/AutoElevateLauncher.Tests/ItemRunnerCommandTests.cs`

**Interfaces:**
- Produces: `public interface IStartupItemLauncher { Task<int> RunAsync(StartupConfig config, StartupItem item, CancellationToken cancellationToken = default); }`
- Produces: `ItemRunner.RunAsync(StartupConfig config, StartupItem item, CancellationToken cancellationToken = default)` for direct elevated in-process launching.
- Consumes: existing `ItemRunner.BuildPowerShellArguments(StartupItem)` and `ItemRunner.BuildExecutableStartInfo(StartupItem)`.

- [ ] **Step 1: Add launcher interface**

Create `src/AutoElevateLauncher/IStartupItemLauncher.cs`:

```csharp
namespace AutoElevateLauncher;

public interface IStartupItemLauncher
{
    Task<int> RunAsync(StartupConfig config, StartupItem item, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Add failing direct-run signature test by compiling against interface**

Append this compile-level test to `tests/AutoElevateLauncher.Tests/ItemRunnerCommandTests.cs`:

```csharp
[Fact]
public void ItemRunner_ImplementsStartupItemLauncher()
{
    IStartupItemLauncher launcher = new ItemRunner(new ConfigStore());

    Assert.IsType<ItemRunner>(launcher);
}
```

- [ ] **Step 3: Run targeted tests and verify failure**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: FAIL to compile because `ItemRunner` does not implement `IStartupItemLauncher`.

- [ ] **Step 4: Implement direct-run overload**

Change the class declaration and add the overload in `src/AutoElevateLauncher/ItemRunner.cs`:

```csharp
public sealed class ItemRunner : IStartupItemLauncher
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

        return await RunAsync(config, item, cancellationToken);
    }

    public async Task<int> RunAsync(StartupConfig config, StartupItem item, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.GetItemLogDirectory(item.Id));
        var logPath = Path.Combine(AppPaths.GetItemLogDirectory(item.Id), DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fffffff") + ".log");

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
```

Keep `BuildPowerShellArguments`, `BuildExecutableStartInfo`, `RunPowerShellAsync`, `RunExecutableAsync`, and `BuildHeader` unchanged below this block.

- [ ] **Step 5: Run verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

- [ ] **Step 6: Commit**

Run:

```bash
git add src/AutoElevateLauncher/IStartupItemLauncher.cs src/AutoElevateLauncher/ItemRunner.cs tests/AutoElevateLauncher.Tests/ItemRunnerCommandTests.cs
git commit -m "feat: add direct startup item launcher"
```

---

### Task 3: Startup Orchestrator

**Files:**
- Create: `src/AutoElevateLauncher/StartupOrchestrator.cs`
- Create: `tests/AutoElevateLauncher.Tests/StartupOrchestratorTests.cs`

**Interfaces:**
- Consumes: `IStartupItemLauncher.RunAsync(StartupConfig, StartupItem, CancellationToken)` from Task 2.
- Produces: `public sealed class StartupOrchestrator` with `Task RunEnabledItemsOnceAsync(StartupConfig config, CancellationToken cancellationToken = default)` and `Task RunEnabledItemsAsync(StartupConfig config, CancellationToken cancellationToken = default)`.

- [ ] **Step 1: Add orchestrator tests**

Create `tests/AutoElevateLauncher.Tests/StartupOrchestratorTests.cs`:

```csharp
using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class StartupOrchestratorTests
{
    [Fact]
    public async Task RunEnabledItemsAsync_RunsOnlyEnabledItems()
    {
        var launcher = new RecordingLauncher();
        var orchestrator = new StartupOrchestrator(launcher);
        var enabled = new StartupItem { Id = "enabled", Name = "启用", Enabled = true };
        var disabled = new StartupItem { Id = "disabled", Name = "禁用", Enabled = false };
        var config = new StartupConfig { Items = [enabled, disabled] };

        await orchestrator.RunEnabledItemsAsync(config);

        Assert.Equal(["enabled"], launcher.StartedItemIds);
    }

    [Fact]
    public async Task RunEnabledItemsAsync_ContinuesAfterFailure()
    {
        var launcher = new RecordingLauncher { FailItemId = "first" };
        var orchestrator = new StartupOrchestrator(launcher);
        var config = new StartupConfig
        {
            Items =
            [
                new StartupItem { Id = "first", Name = "第一个", Enabled = true },
                new StartupItem { Id = "second", Name = "第二个", Enabled = true }
            ]
        };

        await orchestrator.RunEnabledItemsAsync(config);

        Assert.Equal(["first", "second"], launcher.StartedItemIds);
    }

    [Fact]
    public async Task RunEnabledItemsOnceAsync_RunsOnlyOncePerOrchestratorInstance()
    {
        var launcher = new RecordingLauncher();
        var orchestrator = new StartupOrchestrator(launcher);
        var config = new StartupConfig
        {
            Items = [new StartupItem { Id = "item", Name = "项目", Enabled = true }]
        };

        await orchestrator.RunEnabledItemsOnceAsync(config);
        await orchestrator.RunEnabledItemsOnceAsync(config);

        Assert.Equal(["item"], launcher.StartedItemIds);
    }

    private sealed class RecordingLauncher : IStartupItemLauncher
    {
        public List<string> StartedItemIds { get; } = [];
        public string? FailItemId { get; set; }

        public Task<int> RunAsync(StartupConfig config, StartupItem item, CancellationToken cancellationToken = default)
        {
            StartedItemIds.Add(item.Id);
            if (item.Id == FailItemId)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(0);
        }
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: FAIL to compile because `StartupOrchestrator` does not exist.

- [ ] **Step 3: Implement orchestrator**

Create `src/AutoElevateLauncher/StartupOrchestrator.cs`:

```csharp
namespace AutoElevateLauncher;

public sealed class StartupOrchestrator
{
    private readonly IStartupItemLauncher _launcher;
    private bool _hasRunAutomaticStartup;

    public StartupOrchestrator(IStartupItemLauncher launcher)
    {
        _launcher = launcher;
    }

    public async Task RunEnabledItemsOnceAsync(StartupConfig config, CancellationToken cancellationToken = default)
    {
        if (_hasRunAutomaticStartup)
        {
            return;
        }

        _hasRunAutomaticStartup = true;
        await RunEnabledItemsAsync(config, cancellationToken);
    }

    public async Task RunEnabledItemsAsync(StartupConfig config, CancellationToken cancellationToken = default)
    {
        foreach (var item in config.Items.Where(item => item.Enabled).ToList())
        {
            try
            {
                await _launcher.RunAsync(config, item, cancellationToken);
            }
            catch
            {
                item.LastRunFinishedAt = DateTimeOffset.Now;
                item.LastExitCode = -1;
                item.LastStatus = StartupItemStatus.Failed;
            }
        }
    }
}
```

- [ ] **Step 4: Run verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```bash
git add src/AutoElevateLauncher/StartupOrchestrator.cs tests/AutoElevateLauncher.Tests/StartupOrchestratorTests.cs
git commit -m "feat: run enabled startup items once"
```

---

### Task 4: Manager-Only Scheduled Task Service and Elevated Helper Commands

**Files:**
- Modify: `src/AutoElevateLauncher/ProcessRunner.cs`
- Modify: `src/AutoElevateLauncher/Program.cs`
- Modify: `src/AutoElevateLauncher/ScheduledTaskService.cs`
- Modify: `tests/AutoElevateLauncher.Tests/TaskXmlBuilderTests.cs`

**Interfaces:**
- Produces: `IProcessRunner.RunElevatedAsync(string fileName, string arguments, CancellationToken cancellationToken = default)`.
- Produces: helper CLI commands `--enable-manager-startup` and `--disable-manager-startup`.
- Produces: manager-only `ScheduledTaskService` active API: `CreateOrUpdateManagerSelfStartTaskAsync`, `DeleteManagerSelfStartTaskAsync`, `EnableManagerSelfStartElevatedAsync`, `DisableManagerSelfStartElevatedAsync`.

- [ ] **Step 1: Extend process runner interface**

Modify `src/AutoElevateLauncher/ProcessRunner.cs` interface to include:

```csharp
Task<ProcessCommandResult> RunElevatedAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Implement elevated process execution**

Add this method to `ProcessRunner`:

```csharp
public async Task<ProcessCommandResult> RunElevatedAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        UseShellExecute = true,
        Verb = "runas"
    };

    try
    {
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ProcessCommandResult(-1, string.Empty, "无法启动管理员权限操作。");
        }

        await process.WaitForExitAsync(cancellationToken);
        return new ProcessCommandResult(process.ExitCode, string.Empty, string.Empty);
    }
    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
    {
        return new ProcessCommandResult(1223, string.Empty, "用户取消了管理员权限请求。");
    }
}
```

- [ ] **Step 3: Refactor scheduled task service to manager-only active API**

In `src/AutoElevateLauncher/ScheduledTaskService.cs`, keep `ManagerTaskName`, `CreateOrUpdateManagerSelfStartTaskAsync`, and `DeleteManagerSelfStartTaskAsync`. Add elevated wrapper methods:

```csharp
public Task<ProcessCommandResult> EnableManagerSelfStartElevatedAsync(string appExePath, CancellationToken cancellationToken = default)
{
    return _processRunner.RunElevatedAsync(appExePath, "--enable-manager-startup", cancellationToken);
}

public Task<ProcessCommandResult> DisableManagerSelfStartElevatedAsync(string appExePath, CancellationToken cancellationToken = default)
{
    return _processRunner.RunElevatedAsync(appExePath, "--disable-manager-startup", cancellationToken);
}
```

Keep these existing per-item methods unchanged in this task because `MainForm` still calls them before Task 6: `CreateOrUpdateStartupItemTaskAsync`, `DeleteTaskAsync`, `RunTaskAsync`, and `StopTaskAsync`. Task 6 removes those calls and deletes the methods.

- [ ] **Step 4: Add helper commands to Program.Main**

Modify `src/AutoElevateLauncher/Program.cs` to handle helper commands before WinForms startup:

```csharp
if (args.Length == 1 && args[0] == "--enable-manager-startup")
{
    var result = new ScheduledTaskService(new ProcessRunner()).CreateOrUpdateManagerSelfStartTaskAsync(Application.ExecutablePath).GetAwaiter().GetResult();
    return result.ExitCode;
}

if (args.Length == 1 && args[0] == "--disable-manager-startup")
{
    var result = new ScheduledTaskService(new ProcessRunner()).DeleteManagerSelfStartTaskAsync().GetAwaiter().GetResult();
    return result.ExitCode;
}
```

Keep existing `--run-item` until Task 6 removes UI dependencies; do not add new per-item task creation calls.

- [ ] **Step 5: Run verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

- [ ] **Step 6: Commit**

Run:

```bash
git add src/AutoElevateLauncher/ProcessRunner.cs src/AutoElevateLauncher/Program.cs src/AutoElevateLauncher/ScheduledTaskService.cs tests/AutoElevateLauncher.Tests/TaskXmlBuilderTests.cs
git commit -m "feat: add elevated manager self-start helpers"
```

---

### Task 5: Tray Lifecycle, Admin Detection, and Auto-Run Trigger

**Files:**
- Modify: `src/AutoElevateLauncher/ManagerContext.cs`
- Create: `src/AutoElevateLauncher/WindowsPrivilege.cs`

**Interfaces:**
- Consumes: `StartupOrchestrator.RunEnabledItemsOnceAsync(StartupConfig, CancellationToken)` from Task 3.
- Consumes: `ScheduledTaskService.EnableManagerSelfStartElevatedAsync` and `DisableManagerSelfStartElevatedAsync` from Task 4.
- Produces: Chinese tray menu and automatic startup trigger.
- Produces: `WindowsPrivilege.IsCurrentProcessAdministrator()`.

- [ ] **Step 1: Add privilege helper**

Create `src/AutoElevateLauncher/WindowsPrivilege.cs`:

```csharp
using System.Security.Principal;

namespace AutoElevateLauncher;

public static class WindowsPrivilege
{
    public static bool IsCurrentProcessAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
```

- [ ] **Step 2: Update ManagerContext fields and constructor services**

Change `ManagerContext` fields to include:

```csharp
private readonly StartupOrchestrator _startupOrchestrator;
private readonly IStartupItemLauncher _itemLauncher;
```

In the constructor, initialize:

```csharp
_itemLauncher = new ItemRunner(_configStore);
_startupOrchestrator = new StartupOrchestrator(_itemLauncher);
```

- [ ] **Step 3: Replace tray menu copy with Chinese actions**

Use these menu texts in `BuildMenu`:

```csharp
menu.Items.Add("打开管理器", null, (_, _) => ShowManager());
menu.Items.Add(_startAtLoginMenuItem);
menu.Items.Add("立即运行所有启用项", null, async (_, _) => await RunEnabledItemsNowAsync());
menu.Items.Add("退出", null, (_, _) => ExitThread());
```

Set `_startAtLoginMenuItem` text to `"启用管理员开机自启"` and `_notifyIcon.Text` to `"管理员自启动器"`.

- [ ] **Step 4: Add automatic run trigger after tray initialization**

At the end of `ManagerContext` constructor, after event handlers are assigned, start automatic launch:

```csharp
_ = RunEnabledItemsAtStartupAsync();
```

Add methods:

```csharp
private async Task RunEnabledItemsAtStartupAsync()
{
    await _startupOrchestrator.RunEnabledItemsOnceAsync(_config);
}

private async Task RunEnabledItemsNowAsync()
{
    await _startupOrchestrator.RunEnabledItemsAsync(_config);
}
```

- [ ] **Step 5: Update self-start toggle logic**

In `ToggleStartAtLoginAsync`, when enabling:

```csharp
var result = WindowsPrivilege.IsCurrentProcessAdministrator()
    ? await _taskService.CreateOrUpdateManagerSelfStartTaskAsync(Application.ExecutablePath)
    : await _taskService.EnableManagerSelfStartElevatedAsync(Application.ExecutablePath);
```

When disabling:

```csharp
var result = WindowsPrivilege.IsCurrentProcessAdministrator()
    ? await _taskService.DeleteManagerSelfStartTaskAsync()
    : await _taskService.DisableManagerSelfStartElevatedAsync(Application.ExecutablePath);
```

Use Chinese failure dialogs:

```csharp
MessageBox.Show(result.StandardError + Environment.NewLine + result.StandardOutput, "启用管理员开机自启失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
MessageBox.Show(result.StandardError + Environment.NewLine + result.StandardOutput, "关闭管理员开机自启失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
```

- [ ] **Step 6: Keep existing MainForm construction for this task**

Keep `ShowManager` construction unchanged in Task 5:

```csharp
_mainForm = new MainForm(_config, _configStore, _taskService);
```

Task 6 changes both `MainForm` and this constructor call in the same commit.

- [ ] **Step 7: Run verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

- [ ] **Step 8: Commit if build passes**

Run:

```bash
git add src/AutoElevateLauncher/ManagerContext.cs src/AutoElevateLauncher/WindowsPrivilege.cs
git commit -m "feat: run enabled items from tray startup"
```

---

### Task 6: Chinese Manager UI and Direct Item Operations

**Files:**
- Replace: `src/AutoElevateLauncher/MainForm.cs`
- Modify: `src/AutoElevateLauncher/ScheduledTaskService.cs` if per-item methods are now unused.

**Interfaces:**
- Consumes: `IStartupItemLauncher.RunAsync(StartupConfig, StartupItem, CancellationToken)`.
- Consumes: `StartupOrchestrator.RunEnabledItemsAsync(StartupConfig, CancellationToken)`.
- Produces: `MainForm(StartupConfig config, ConfigStore configStore, ScheduledTaskService taskService, IStartupItemLauncher itemLauncher, StartupOrchestrator startupOrchestrator)`.

- [ ] **Step 1: Replace MainForm constructor signature**

Use this constructor signature:

```csharp
public MainForm(StartupConfig config, ConfigStore configStore, ScheduledTaskService taskService, IStartupItemLauncher itemLauncher, StartupOrchestrator startupOrchestrator)
```

Store `itemLauncher` and `startupOrchestrator` in private readonly fields.

Update `src/AutoElevateLauncher/ManagerContext.cs` `ShowManager` construction in the same task:

```csharp
_mainForm = new MainForm(_config, _configStore, _taskService, _itemLauncher, _startupOrchestrator);
```

- [ ] **Step 2: Replace ListBox with DataGridView**

Use a `DataGridView` configured as:

```csharp
private readonly DataGridView _items = new()
{
    Dock = DockStyle.Fill,
    ReadOnly = true,
    AllowUserToAddRows = false,
    AllowUserToDeleteRows = false,
    AutoGenerateColumns = false,
    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    MultiSelect = false,
    RowHeadersVisible = false
};
```

Add columns in `BuildLayout`:

```csharp
_items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "名称", DataPropertyName = nameof(StartupItem.Name), Width = 150 });
_items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "类型", DataPropertyName = nameof(StartupItem.Type), Width = 90 });
_items.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "启用", DataPropertyName = nameof(StartupItem.Enabled), Width = 60 });
_items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "最近状态", DataPropertyName = nameof(StartupItem.LastStatus), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
```

- [ ] **Step 3: Use Chinese labels and buttons**

Set form and controls to Chinese text:

```csharp
Text = "管理员自启动器";
var addScript = new Button { Text = "新增脚本", Width = 100 };
var addProgram = new Button { Text = "新增程序", Width = 100 };
var delete = new Button { Text = "删除", Width = 80 };
var save = new Button { Text = "保存", Width = 80 };
var run = new Button { Text = "立即运行", Width = 90 };
var stop = new Button { Text = "停止", Width = 80 };
var logs = new Button { Text = "打开日志", Width = 90 };
_enabled.Text = "启用此项目";
```

Use these labels: `名称`, `路径`, `参数`, `工作目录`.

- [ ] **Step 4: Add status area**

Add labels:

```csharp
private readonly Label _permissionStatus = new() { Dock = DockStyle.Top, AutoSize = true };
private readonly Label _selfStartStatus = new() { Dock = DockStyle.Top, AutoSize = true };
```

Set text in `RefreshStatusLabels`:

```csharp
_permissionStatus.Text = WindowsPrivilege.IsCurrentProcessAdministrator() ? "当前权限：管理员" : "当前权限：普通用户";
_selfStartStatus.Text = _config.StartManagerAtLogin ? "管理员开机自启：已启用" : "管理员开机自启：未启用";
```

- [ ] **Step 5: Make save only validate and save config**

Remove the call to `CreateOrUpdateStartupItemTaskAsync`. `SaveSelectedAsync` should:

```csharp
var validation = StartupItemValidator.Validate(item);
if (!validation.IsValid)
{
    MessageBox.Show(this, string.Join(Environment.NewLine, validation.Errors), "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    return;
}

_configStore.Save(_config);
RefreshList();
RestoreSelection(selectedId);
LoadSelectedIntoDetails();
```

- [ ] **Step 6: Make delete only remove config item**

Replace `DeleteSelectedAsync` body with:

```csharp
var item = SelectedItem;
if (item is null) return;
var confirmed = MessageBox.Show(this, $"确定删除“{item.Name}”吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
if (confirmed != DialogResult.Yes) return;
_config.Items.Remove(item);
_configStore.Save(_config);
RefreshList();
```

- [ ] **Step 7: Make run now direct-launch selected item**

Replace `RunSelectedAsync` body with:

```csharp
var item = SelectedItem;
if (item is null) return;
await _itemLauncher.RunAsync(_config, item);
RefreshList();
LoadSelectedIntoDetails();
```

For stop, show a Chinese limitation message because direct spawned executables/scripts are not tracked for later stop:

```csharp
MessageBox.Show(this, "当前版本不跟踪已启动进程，无法可靠停止。请在任务管理器或目标程序中结束。", "无法停止", MessageBoxButtons.OK, MessageBoxIcon.Information);
```

- [ ] **Step 8: Update add dialogs and filters to Chinese**

Use filters:

```csharp
Filter = type == StartupItemType.PowerShellScript ? "PowerShell 脚本 (*.ps1)|*.ps1" : "可执行程序 (*.exe)|*.exe"
```

- [ ] **Step 9: Remove per-item scheduled task service methods**

After `MainForm` no longer calls these methods, remove these exact public methods from `ScheduledTaskService`:

```csharp
CreateOrUpdateStartupItemTaskAsync
DeleteTaskAsync
RunTaskAsync
StopTaskAsync
```

Keep `TaskXmlBuilder.BuildStartupItemTaskXml` and its existing tests in this refactor so legacy config/test coverage remains stable, but do not call it from runtime code.

- [ ] **Step 10: Run verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

Run: `dotnet build "AutoElevateLauncher.sln" -c Release`

Expected: Build succeeded with 0 errors.

- [ ] **Step 11: Commit**

Run:

```bash
git add src/AutoElevateLauncher/MainForm.cs src/AutoElevateLauncher/ScheduledTaskService.cs src/AutoElevateLauncher/ManagerContext.cs
git commit -m "refactor: replace per-item tasks with Chinese manager UI"
```

---

### Task 7: Chinese README and Final Verification

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: final behavior from Tasks 1-6.
- Produces: Chinese user documentation matching the refactored product.

- [ ] **Step 1: Rewrite README in Chinese**

Replace `README.md` with:

```markdown
# 管理员自启动器

一个轻量 Windows 托盘工具，用于在用户登录后以管理员权限启动自身，然后自动运行已启用的 PowerShell 脚本和可执行程序。

## 运行要求

- Windows
- .NET 8 Desktop Runtime（框架依赖发布版本需要）

## 工作方式

软件只创建一个 Windows 计划任务：`AutoElevateLauncher-Manager`。

启用“管理员开机自启”后，Windows 会在当前用户登录时以最高权限启动本软件。软件启动后读取配置，并自动运行所有已启用的启动项目。脚本和程序由已提权的软件进程启动，因此默认继承管理员权限。

## 数据位置

- 配置：`%AppData%\AutoElevateLauncher\config.json`
- 日志：`%AppData%\AutoElevateLauncher\logs\`

## 使用方法

1. 启动 `AutoElevateLauncher.exe`。
2. 在托盘菜单中打开管理器。
3. 添加 PowerShell 脚本或可执行程序。
4. 填写参数和工作目录。
5. 勾选“启用此项目”。
6. 点击“保存”。
7. 启用“管理员开机自启”。首次启用可能需要确认 UAC。

下次登录时，软件会以管理员权限自动启动，并运行所有已启用项目。

## 已知限制

- 可执行程序启动成功后会记录进程 ID，并将状态视为成功；当前版本不等待长期运行程序退出，也不记录最终退出码。
- 当前版本不可靠跟踪已启动进程，因此“停止”只提供限制说明。
- 日志不会自动清理，会持续累积在日志目录下。
- 旧版本创建的单项目计划任务不会自动删除；如曾使用旧版本，请在 Windows 任务计划程序中手动清理不需要的旧任务。
```

- [ ] **Step 2: Run full verification**

Run: `dotnet test "AutoElevateLauncher.sln" --nologo`

Expected: PASS.

Run: `dotnet build "AutoElevateLauncher.sln" -c Release`

Expected: Build succeeded with 0 errors and 0 warnings.

- [ ] **Step 3: Inspect final diff**

Run: `git diff --stat`

Expected: only source, test, and README files related to this refactor are changed. Do not include unrelated deleted old docs or `.superpowers/brainstorm` files.

- [ ] **Step 4: Commit**

Run:

```bash
git add README.md
git commit -m "docs: update Chinese launcher documentation"
```

---

## Manual Verification Checklist

Run after all tasks are complete:

- [ ] `dotnet test "AutoElevateLauncher.sln" --nologo` passes.
- [ ] `dotnet build "AutoElevateLauncher.sln" -c Release` succeeds with 0 errors.
- [ ] Launch app normally and verify UI is Chinese.
- [ ] Verify permission label shows `当前权限：普通用户` when not elevated.
- [ ] Enable administrator self-start and confirm UAC appears when needed.
- [ ] Run the created `AutoElevateLauncher-Manager` task and verify the app starts elevated.
- [ ] Add one enabled script and one disabled script; verify only the enabled item auto-runs once.
- [ ] Verify logs are written under `%AppData%\AutoElevateLauncher\logs\<item-id>\`.
- [ ] Disable administrator self-start and verify the manager task is removed or a Chinese error is shown.

## Implementation Notes

- The existing worktree currently has unrelated deletions under `docs/superpowers/` and an untracked `.superpowers/` directory from the failed visual companion session. Do not stage those unless the user explicitly approves.
- If a task cannot be committed independently because `ManagerContext` and `MainForm` constructor changes are coupled, implement Tasks 5 and 6 together, run verification, and commit them together.
- Do not use `git reset --hard`, `git checkout --`, or any destructive command to clean unrelated changes.
