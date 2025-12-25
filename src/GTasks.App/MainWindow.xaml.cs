using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using GTasks.Core.Services;
using GTasks.UI.ViewModels;
using GTasks.UI.Views;
using WinRT.Interop;

namespace GTasks.App;

public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        // Get AppWindow for customization
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Configure window
        ConfigureWindow();
        ConfigureTitleBar();
        ApplySystemBackdrop();

        // Navigate to main page and set up ViewModel and services
        RootFrame.Navigate(typeof(MainPage));
        if (RootFrame.Content is MainPage mainPage)
        {
            var viewModel = App.Services.GetRequiredService<MainViewModel>();
            var authService = App.Services.GetRequiredService<IAuthService>();
            var syncService = App.Services.GetRequiredService<ISyncService>();

            mainPage.SetViewModel(viewModel);
            mainPage.SetServices(authService, syncService);
        }
    }

    private void ConfigureWindow()
    {
        // Set minimum size
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

        // Center on screen
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var centerX = (displayArea.WorkArea.Width - 1200) / 2;
        var centerY = (displayArea.WorkArea.Height - 800) / 2;
        _appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
    }

    private void ConfigureTitleBar()
    {
        // Use custom title bar with Mica
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
        else
        {
            ExtendsContentIntoTitleBar = true;
        }
    }

    private void ApplySystemBackdrop()
    {
        // Apply Mica backdrop for Windows 11 look
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
        else if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
    }
}
