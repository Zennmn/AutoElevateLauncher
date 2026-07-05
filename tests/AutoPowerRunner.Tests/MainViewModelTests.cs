using System.Diagnostics;
using System.Windows.Input;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;
using AutoPowerRunner.ViewModels;

namespace AutoPowerRunner.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task MainViewModel_AddOrUpdateTaskAsync_AddsAndSaves()
    {
        var config = new FakeTaskConfigService();
        var viewModel = CreateViewModel(config);
        var task = new ManagedTask { Name = "Task" };

        await viewModel.AddOrUpdateTaskAsync(task);

        Assert.Same(task, Assert.Single(viewModel.Tasks));
        Assert.Equal(1, config.SaveCount);
        Assert.Same(task, Assert.Single(config.LastSavedTasks));
    }

    [Fact]
    public void MainViewModel_RunAllEnabled_RunsOnlyEnabledTasks()
    {
        var processRunner = new FakeProcessRunner();
        var viewModel = CreateViewModel(processRunner: processRunner);
        var enabled = new ManagedTask { Name = "Enabled", IsEnabled = true };
        var disabled = new ManagedTask { Name = "Disabled", IsEnabled = false };
        viewModel.Tasks.Add(enabled);
        viewModel.Tasks.Add(disabled);

        viewModel.RunAllEnabled();

        Assert.Equal([enabled], processRunner.StartedTasks);
    }

    [Fact]
    public async Task MainViewModel_DeleteSelected_WhenSaveFails_RollsBackAndLogs()
    {
        var config = new FakeTaskConfigService { SaveException = new IOException("save failed") };
        var processRunner = new FakeProcessRunner();
        var log = new FakeLogService();
        var viewModel = CreateViewModel(config, processRunner, logService: log);
        var first = new ManagedTask { Name = "First" };
        var selected = new ManagedTask { Name = "Selected" };
        var last = new ManagedTask { Name = "Last" };
        viewModel.Tasks.Add(first);
        viewModel.Tasks.Add(selected);
        viewModel.Tasks.Add(last);
        viewModel.SelectedTask = selected;

        viewModel.DeleteSelectedCommand.Execute(null);
        await AwaitCommandAsync(viewModel.DeleteSelectedCommand);

        Assert.Equal([first, selected, last], viewModel.Tasks);
        Assert.Same(selected, viewModel.SelectedTask);
        Assert.Contains(selected.Id, processRunner.StoppedTaskIds);
        Assert.Contains(log.Errors, entry => entry.Message.Contains("Could not delete task 'Selected'."));
    }

    [Fact]
    public async Task MainViewModel_ToggleSelectedEnabled_WhenSaveFails_RollsBackAndLogs()
    {
        var config = new FakeTaskConfigService { SaveException = new IOException("save failed") };
        var log = new FakeLogService();
        var task = new ManagedTask { Name = "Toggle", IsEnabled = true };
        var viewModel = CreateViewModel(config, logService: log);
        viewModel.Tasks.Add(task);
        viewModel.SelectedTask = task;

        viewModel.ToggleSelectedEnabledCommand.Execute(null);
        await AwaitCommandAsync(viewModel.ToggleSelectedEnabledCommand);

        Assert.True(task.IsEnabled);
        Assert.Contains(log.Errors, entry => entry.Message.Contains("Could not update task 'Toggle'."));
    }

    [Fact]
    public void MainViewModel_SelectedTask_RaisesCanExecuteChanged()
    {
        var viewModel = CreateViewModel();
        var raisedCount = 0;
        viewModel.RunSelectedCommand.CanExecuteChanged += (_, _) => raisedCount++;

        viewModel.SelectedTask = new ManagedTask();

        Assert.True(raisedCount > 0);
        Assert.True(viewModel.RunSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void MainViewModel_ProcessCallback_UsesSynchronizationContextWhenProvided()
    {
        var processRunner = new FakeProcessRunner();
        var updateContext = new CapturingSynchronizationContext();
        var viewModel = CreateViewModel(processRunner: processRunner, updateContext: updateContext);
        var task = new ManagedTask { IsEnabled = true };
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        viewModel.Tasks.Add(task);

        viewModel.RunAllEnabled();
        processRunner.LastUpdateCallback?.Invoke(task);

        Assert.Empty(changedProperties);
        var postedCallback = Assert.Single(updateContext.PostedCallbacks);

        postedCallback();

        Assert.Contains(nameof(MainViewModel.Tasks), changedProperties);
    }

    [Fact]
    public async Task AsyncRelayCommand_WhenExecuteFails_LogsAndDoesNotThrow()
    {
        var log = new FakeLogService();
        var command = new AsyncRelayCommand(
            _ => throw new InvalidOperationException("boom"),
            log,
            "Async command failed.");

        var exception = Record.Exception(() => command.Execute(null));
        await command.ExecutionTask!;

        Assert.Null(exception);
        Assert.Contains(log.Errors, entry =>
            entry.Message == "Async command failed." &&
            entry.Exception is InvalidOperationException);
    }

    private static MainViewModel CreateViewModel(
        ITaskConfigService? configService = null,
        IProcessRunner? processRunner = null,
        IStartupTaskService? startupTaskService = null,
        ILogService? logService = null,
        SynchronizationContext? updateContext = null)
    {
        return new MainViewModel(
            configService ?? new FakeTaskConfigService(),
            processRunner ?? new FakeProcessRunner(),
            startupTaskService ?? new FakeStartupTaskService(),
            logService ?? new FakeLogService(),
            updateContext);
    }

    private static async Task AwaitCommandAsync(ICommand command)
    {
        var asyncCommand = Assert.IsType<AsyncRelayCommand>(command);
        await asyncCommand.ExecutionTask!;
    }

    private sealed class FakeTaskConfigService : ITaskConfigService
    {
        public List<ManagedTask> TasksToLoad { get; } = [];
        public IReadOnlyCollection<ManagedTask> LastSavedTasks { get; private set; } = [];
        public int SaveCount { get; private set; }
        public Exception? SaveException { get; set; }

        public Task<List<ManagedTask>> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TasksToLoad.ToList());
        }

        public Task SaveAsync(IReadOnlyCollection<ManagedTask> tasks, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (SaveException is not null)
            {
                throw SaveException;
            }

            LastSavedTasks = tasks.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public IReadOnlyCollection<Guid> RunningTaskIds => [];
        public List<ManagedTask> StartedTasks { get; } = [];
        public List<Guid> StoppedTaskIds { get; } = [];
        public Action<ManagedTask>? LastUpdateCallback { get; private set; }

        public Process Start(ManagedTask task, Action<ManagedTask>? onUpdated = null)
        {
            StartedTasks.Add(task);
            LastUpdateCallback = onUpdated;
            return new Process();
        }

        public void Stop(Guid taskId)
        {
            StoppedTaskIds.Add(taskId);
        }

        public void StopAll()
        {
        }
    }

    private sealed class FakeStartupTaskService : IStartupTaskService
    {
        public bool Enabled { get; set; }

        public bool IsEnabled()
        {
            return Enabled;
        }

        public void Enable()
        {
            Enabled = true;
        }

        public void Disable()
        {
            Enabled = false;
        }
    }

    private sealed class FakeLogService : ILogService
    {
        public string LogFile => "test.log";
        public List<string> InfoMessages { get; } = [];
        public List<(string Message, Exception? Exception)> Errors { get; } = [];

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Error(string message, Exception? exception = null)
        {
            Errors.Add((message, exception));
        }
    }

    private sealed class CapturingSynchronizationContext : SynchronizationContext
    {
        public List<Action> PostedCallbacks { get; } = [];

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostedCallbacks.Add(() => d(state));
        }
    }
}
