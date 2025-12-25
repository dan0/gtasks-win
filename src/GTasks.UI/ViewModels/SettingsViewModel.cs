using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTasks.Core.Services;
using Microsoft.UI.Xaml;

namespace GTasks.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ISyncService _syncService;

    [ObservableProperty]
    private ElementTheme _currentTheme = ElementTheme.Default;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private int _syncIntervalMinutes = 5;

    [ObservableProperty]
    private bool _showCompletedTasks = true;

    [ObservableProperty]
    private DateTimeOffset? _lastSyncTime;

    [ObservableProperty]
    private int _pendingChangesCount;

    public SettingsViewModel(IAuthService authService, ISyncService syncService)
    {
        _authService = authService;
        _syncService = syncService;

        IsAuthenticated = _authService.IsAuthenticated;
        UserEmail = _authService.UserEmail ?? string.Empty;
        LastSyncTime = _syncService.LastSyncTime;
        PendingChangesCount = _syncService.PendingChangesCount;
    }

    [RelayCommand]
    private void SetTheme(ElementTheme theme)
    {
        CurrentTheme = theme;
        // Theme will be applied through binding in the view
    }

    [RelayCommand]
    private void SetSyncInterval(int minutes)
    {
        SyncIntervalMinutes = minutes;
        _syncService.StopBackgroundSync();
        _syncService.StartBackgroundSync(TimeSpan.FromMinutes(minutes));
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        IsAuthenticated = false;
        UserEmail = string.Empty;
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        await _syncService.SyncAsync();
        LastSyncTime = _syncService.LastSyncTime;
        PendingChangesCount = _syncService.PendingChangesCount;
    }
}
