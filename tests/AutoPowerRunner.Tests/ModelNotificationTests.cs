using System.ComponentModel;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Tests;

public sealed class ModelNotificationTests
{
    [Fact]
    public void ManagedTask_IsEnabled_RaisesPropertyChanged()
    {
        var task = new ManagedTask();
        var changedProperties = new List<string?>();
        var notifyingTask = Assert.IsAssignableFrom<INotifyPropertyChanged>(task);
        notifyingTask.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        task.IsEnabled = !task.IsEnabled;

        Assert.Contains(nameof(ManagedTask.IsEnabled), changedProperties);
    }

    [Fact]
    public void TaskRuntimeResult_Status_RaisesStatusAndSummaryPropertyChanged()
    {
        var result = new TaskRuntimeResult();
        var changedProperties = new List<string?>();
        var notifyingResult = Assert.IsAssignableFrom<INotifyPropertyChanged>(result);
        notifyingResult.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        result.Status = TaskRuntimeStatus.Running;

        Assert.Contains(nameof(TaskRuntimeResult.Status), changedProperties);
        Assert.Contains(nameof(TaskRuntimeResult.Summary), changedProperties);
    }
}
