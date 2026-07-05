using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AutoPowerRunner.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace AutoPowerRunner;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public async Task InitializeAsync()
    {
        await _viewModel.LoadAsync();
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private async void AddTask_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TaskEditorWindow { Owner = this };
        if (editor.ShowDialog() == true)
        {
            try
            {
                await _viewModel.AddOrUpdateTaskAsync(editor.Result);
            }
            catch (Exception ex)
            {
                ShowError("Could not save the task.", ex);
            }
        }
    }

    private async void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTask is null)
        {
            return;
        }

        var editor = new TaskEditorWindow(_viewModel.SelectedTask) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            try
            {
                await _viewModel.AddOrUpdateTaskAsync(editor.Result);
            }
            catch (Exception ex)
            {
                ShowError("Could not save the task.", ex);
            }
        }
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_viewModel.LogFile))
        {
            MessageBox.Show(this, "The log file does not exist yet.", "Open Log", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_viewModel.LogFile)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError("Could not open the log file.", ex);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void ShowError(string message, Exception exception)
    {
        MessageBox.Show(this, $"{message}{Environment.NewLine}{exception.Message}", "Auto Power Runner", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
