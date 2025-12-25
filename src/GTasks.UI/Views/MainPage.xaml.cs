using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using GTasks.Core.Models;
using GTasks.Core.Services;
using GTasks.UI.ViewModels;

namespace GTasks.UI.Views;

public sealed partial class MainPage : Page
{
    private MainViewModel? _viewModel;
    private IAuthService? _authService;
    private ISyncService? _syncService;

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    public void SetViewModel(MainViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void SetServices(IAuthService authService, ISyncService syncService)
    {
        _authService = authService;
        _syncService = syncService;

        _authService.AuthenticationChanged += OnAuthenticationChanged;
        _syncService.SyncStatusChanged += OnSyncStatusChanged;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_authService == null) return;

        // Try to restore previous session
        LoginProgress.IsActive = true;
        StatusText.Text = "Checking authentication...";

        var restored = await _authService.TryRestoreAsync();

        LoginProgress.IsActive = false;
        StatusText.Text = "";

        if (restored)
        {
            ShowMainView();
            await LoadDataAsync();
        }
        else
        {
            ShowLoginView();
        }
    }

    private void OnAuthenticationChanged(object? sender, bool isAuthenticated)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isAuthenticated)
            {
                ShowMainView();
            }
            else
            {
                ShowLoginView();
            }
        });
    }

    private void OnSyncStatusChanged(object? sender, SyncStatusEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SyncProgress.IsActive = e.IsSyncing;
            SyncStatusText.Text = e.Message ?? "Ready";
        });
    }

    private void ShowLoginView()
    {
        LoginView.Visibility = Visibility.Visible;
        MainAppView.Visibility = Visibility.Collapsed;
    }

    private void ShowMainView()
    {
        LoginView.Visibility = Visibility.Collapsed;
        MainAppView.Visibility = Visibility.Visible;

        if (_authService?.UserEmail != null)
        {
            UserEmailText.Text = _authService.UserEmail;
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_authService == null) return;

        LoginButton.IsEnabled = false;
        LoginProgress.IsActive = true;
        StatusText.Text = "Opening browser for Google sign-in...";

        try
        {
            var success = await _authService.LoginAsync();

            if (success)
            {
                StatusText.Text = "Success! Loading your tasks...";
                ShowMainView();
                await LoadDataAsync();
            }
            else
            {
                StatusText.Text = "Login was cancelled or failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginProgress.IsActive = false;
        }
    }

    private async Task LoadDataAsync()
    {
        if (_viewModel == null || _syncService == null) return;

        SyncProgress.IsActive = true;
        SyncStatusText.Text = "Syncing...";

        try
        {
            await _syncService.SyncAsync();
            await _viewModel.InitializeAsync();

            TaskListsView.ItemsSource = _viewModel.TaskLists;
            TasksView.ItemsSource = _viewModel.FilteredTasks;

            if (_viewModel.TaskLists.Count > 0)
            {
                TaskListsView.SelectedIndex = 0;
            }

            UpdateEmptyState();
        }
        catch (Exception ex)
        {
            SyncStatusText.Text = $"Sync failed: {ex.Message}";
        }
        finally
        {
            SyncProgress.IsActive = false;
        }
    }

    private void UpdateEmptyState()
    {
        var hasItems = _viewModel?.FilteredTasks.Count > 0;
        EmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        TasksView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        if (_syncService == null) return;

        SyncProgress.IsActive = true;
        await _syncService.SyncAsync();
        await LoadDataAsync();
        SyncProgress.IsActive = false;
    }

    private async void SwitchAccount_Click(object sender, RoutedEventArgs e)
    {
        if (_authService == null) return;

        // Log out first, then immediately start a new login
        await _authService.LogoutAsync();
        _viewModel?.TaskLists.Clear();
        _viewModel?.FilteredTasks.Clear();

        // Immediately start the login process
        LoginProgress.IsActive = true;
        StatusText.Text = "Opening browser for Google sign-in...";
        ShowLoginView();

        try
        {
            var success = await _authService.LoginAsync();
            if (success)
            {
                StatusText.Text = "Success! Loading your tasks...";
                ShowMainView();
                await LoadDataAsync();
            }
            else
            {
                StatusText.Text = "Login was cancelled or failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            LoginProgress.IsActive = false;
        }
    }

    private async void LogoutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_authService == null) return;

        await _authService.LogoutAsync();
        _viewModel?.TaskLists.Clear();
        _viewModel?.FilteredTasks.Clear();
        ShowLoginView();
    }

    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_authService == null) return;

        var dialog = new ContentDialog
        {
            Title = "Sign Out",
            Content = "Are you sure you want to sign out?",
            PrimaryButtonText = "Sign Out",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _authService.LogoutAsync();
            _viewModel?.TaskLists.Clear();
            _viewModel?.FilteredTasks.Clear();
            ShowLoginView();
        }
    }

    private void TaskListsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null) return;

        if (TaskListsView.SelectedItem is TaskListViewModel selectedList)
        {
            _viewModel.SelectedTaskList = selectedList;
            TasksView.ItemsSource = _viewModel.FilteredTasks;
            UpdateEmptyState();
        }
    }

    private void TasksView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null) return;

        if (TasksView.SelectedItem is TaskItem selectedTask)
        {
            _viewModel.SelectedTask = selectedTask;
        }
    }

    private async void QuickAddBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await AddTaskFromQuickAdd();
        }
    }

    private async void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        await AddTaskFromQuickAdd();
    }

    private async Task AddTaskFromQuickAdd()
    {
        if (_viewModel == null) return;

        var title = QuickAddBox.Text.Trim();
        if (!string.IsNullOrEmpty(title))
        {
            await _viewModel.CreateTaskCommand.ExecuteAsync(title);
            QuickAddBox.Text = "";
            TasksView.ItemsSource = _viewModel.FilteredTasks;
            UpdateEmptyState();
        }
    }

    private async void TaskCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        if (sender is CheckBox checkBox && checkBox.DataContext is TaskItem task)
        {
            await _viewModel.ToggleTaskCompletionCommand.ExecuteAsync(task);
        }
    }

    private async void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is TaskItem task)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Task",
                Content = $"Are you sure you want to delete \"{task.Title}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await _viewModel.DeleteTaskCommand.ExecuteAsync(task);
                UpdateEmptyState();
            }
        }
    }

    private async void TaskDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_viewModel == null) return;

        // Find the task from the DataContext
        if (sender.DataContext is TaskItem task && args.NewDate != args.OldDate)
        {
            DateTimeOffset? newDate = args.NewDate?.Date;
            await _viewModel.UpdateTaskDueDateCommand.ExecuteAsync((task, newDate));
        }
    }

    private async void SetDateToday_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        if (sender is Button button && GetTaskFromButton(button) is TaskItem task)
        {
            var today = DateTimeOffset.Now.Date;
            await _viewModel.UpdateTaskDueDateCommand.ExecuteAsync((task, (DateTimeOffset?)today));
            CloseFlyoutFromButton(button);
        }
    }

    private async void SetDateTomorrow_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        if (sender is Button button && GetTaskFromButton(button) is TaskItem task)
        {
            var tomorrow = DateTimeOffset.Now.Date.AddDays(1);
            await _viewModel.UpdateTaskDueDateCommand.ExecuteAsync((task, (DateTimeOffset?)tomorrow));
            CloseFlyoutFromButton(button);
        }
    }

    private async void ClearDate_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        if (sender is Button button && GetTaskFromButton(button) is TaskItem task)
        {
            await _viewModel.UpdateTaskDueDateCommand.ExecuteAsync((task, (DateTimeOffset?)null));
            CloseFlyoutFromButton(button);
        }
    }

    private static TaskItem? GetTaskFromButton(Button button)
    {
        // Navigate up the visual tree to find the DataContext
        var parent = button.Parent;
        while (parent != null)
        {
            if (parent is FrameworkElement fe && fe.DataContext is TaskItem task)
            {
                return task;
            }
            parent = (parent as FrameworkElement)?.Parent;
        }
        return null;
    }

    private static void CloseFlyoutFromButton(Button button)
    {
        // Navigate up to find and close the Flyout
        var parent = button.Parent;
        while (parent != null)
        {
            if (parent is Flyout flyout)
            {
                flyout.Hide();
                return;
            }
            if (parent is FrameworkElement fe)
            {
                // Check if this element is inside a Flyout
                var flyoutParent = fe.Parent;
                if (flyoutParent is FlyoutPresenter presenter)
                {
                    // Find the flyout and hide it
                    break;
                }
                parent = fe.Parent;
            }
            else
            {
                break;
            }
        }
    }

    private async void NewListButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var textBox = new TextBox { PlaceholderText = "List name" };
        var dialog = new ContentDialog
        {
            Title = "New List",
            Content = textBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var title = textBox.Text.Trim();
            if (!string.IsNullOrEmpty(title))
            {
                await _viewModel.CreateTaskListCommand.ExecuteAsync(title);
                TaskListsView.ItemsSource = _viewModel.TaskLists;
            }
        }
    }

    private async void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        if (sender is Button button && button.Tag is string filterName)
        {
            // Clear list selection when using filters
            TaskListsView.SelectedIndex = -1;

            await _viewModel.ApplyFilterCommand.ExecuteAsync(filterName);
            TasksView.ItemsSource = _viewModel.FilteredTasks;
            UpdateEmptyState();
        }
    }

    private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        // Select the first task list to clear filters
        if (_viewModel.TaskLists.Count > 0)
        {
            TaskListsView.SelectedIndex = 0;
        }
    }
}
