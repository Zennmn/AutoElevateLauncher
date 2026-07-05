using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly TaskConfigService _configService;
    private readonly ProcessRunner _processRunner;
    private readonly StartupTaskService _startupTaskService;
    private readonly LogService _logService;
    private ManagedTask? _selectedTask;
    private bool _isAdministratorAutostartEnabled;

    public MainViewModel(
        TaskConfigService configService,
        ProcessRunner processRunner,
        StartupTaskService startupTaskService,
        LogService logService)
    {
        _configService = configService;
        _processRunner = processRunner;
        _startupTaskService = startupTaskService;
        _logService = logService;

        RunSelectedCommand = new RelayCommand(_ => RunSelected(), _ => SelectedTask is not null);
        StopSelectedCommand = new RelayCommand(_ => StopSelected(), _ => SelectedTask is not null);
        DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedTask is not null);
        ToggleSelectedEnabledCommand = new RelayCommand(_ => ToggleSelectedEnabled(), _ => SelectedTask is not null);
        RunAllEnabledCommand = new RelayCommand(_ => RunAllEnabled());
        StopAllCommand = new RelayCommand(_ => StopAll());
        ToggleAutostartCommand = new RelayCommand(_ => ToggleAutostart());
    }

    public ObservableCollection<ManagedTask> Tasks { get; } = [];

    public ManagedTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            _selectedTask = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public bool IsAdministratorAutostartEnabled
    {
        get => _isAdministratorAutostartEnabled;
        private set
        {
            _isAdministratorAutostartEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutostartStatusText));
            OnPropertyChanged(nameof(ToggleAutostartText));
        }
    }

    public string AutostartStatusText => IsAdministratorAutostartEnabled
        ? "Administrator autostart is enabled"
        : "Administrator autostart is disabled";

    public string ToggleAutostartText => IsAdministratorAutostartEnabled
        ? "Disable administrator autostart"
        : "Enable administrator autostart";

    public string LogFile => _logService.LogFile;

    public ICommand RunSelectedCommand { get; }
    public ICommand StopSelectedCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand ToggleSelectedEnabledCommand { get; }
    public ICommand RunAllEnabledCommand { get; }
    public ICommand StopAllCommand { get; }
    public ICommand ToggleAutostartCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadAsync()
    {
        Tasks.Clear();
        foreach (var task in await _configService.LoadAsync())
        {
            Tasks.Add(task);
        }

        IsAdministratorAutostartEnabled = _startupTaskService.IsEnabled();
    }

    public async Task SaveAsync()
    {
        await _configService.SaveAsync(Tasks);
    }

    public async Task AddOrUpdateTaskAsync(ManagedTask task)
    {
        var existing = Tasks.FirstOrDefault(item => item.Id == task.Id);
        if (existing is null)
        {
            Tasks.Add(task);
        }
        else
        {
            var index = Tasks.IndexOf(existing);
            Tasks[index] = task;
        }

        await SaveAsync();
    }

    public void RunAllEnabled()
    {
        foreach (var task in Tasks.Where(task => task.IsEnabled))
        {
            RunTask(task);
        }
    }

    public void StopAll()
    {
        _processRunner.StopAll();
    }

    private void RunSelected()
    {
        if (SelectedTask is not null)
        {
            RunTask(SelectedTask);
        }
    }

    private void StopSelected()
    {
        if (SelectedTask is not null)
        {
            _processRunner.Stop(SelectedTask.Id);
        }
    }

    private async void DeleteSelected()
    {
        if (SelectedTask is null)
        {
            return;
        }

        _processRunner.Stop(SelectedTask.Id);
        Tasks.Remove(SelectedTask);
        SelectedTask = null;
        await SaveAsync();
    }

    private async void ToggleSelectedEnabled()
    {
        if (SelectedTask is null)
        {
            return;
        }

        SelectedTask.IsEnabled = !SelectedTask.IsEnabled;
        await SaveAsync();
        OnPropertyChanged(nameof(Tasks));
    }

    private void ToggleAutostart()
    {
        if (IsAdministratorAutostartEnabled)
        {
            _startupTaskService.Disable();
        }
        else
        {
            _startupTaskService.Enable();
        }

        IsAdministratorAutostartEnabled = _startupTaskService.IsEnabled();
    }

    private void RunTask(ManagedTask task)
    {
        try
        {
            _processRunner.Start(task, _ => OnPropertyChanged(nameof(Tasks)));
        }
        catch (Exception ex)
        {
            _logService.Error($"Could not run task '{task.Name}'.", ex);
        }
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new[] { RunSelectedCommand, StopSelectedCommand, DeleteSelectedCommand, ToggleSelectedEnabledCommand })
        {
            if (command is RelayCommand relayCommand)
            {
                relayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
