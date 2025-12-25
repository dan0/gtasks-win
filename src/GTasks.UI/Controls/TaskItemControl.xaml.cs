using Microsoft.UI.Xaml.Controls;
using GTasks.Core.Models;

namespace GTasks.UI.Controls;

public sealed partial class TaskItemControl : UserControl
{
    public TaskItem? Task { get; set; }

    public TaskItemControl()
    {
        InitializeComponent();
    }

    public void SetTask(TaskItem task)
    {
        Task = task;
        TitleText.Text = task.Title;
        CompleteCheckBox.IsChecked = task.IsCompleted;
    }
}
