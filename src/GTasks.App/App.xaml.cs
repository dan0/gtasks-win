using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using GTasks.Core.Services;
using GTasks.Data;
using GTasks.UI.ViewModels;

namespace GTasks.App;

public partial class App : Application
{
    private readonly IHost _host;
    private Window? _mainWindow;

    public static IServiceProvider Services => ((App)Current)._host.Services;
    public static Window MainWindow => ((App)Current)._mainWindow!;

    public App()
    {
        InitializeComponent();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Core services
                services.AddSingleton<IAuthService, AuthService>();
                services.AddSingleton<IGoogleTasksService, GoogleTasksService>();
                services.AddSingleton<ITaskRepository, TaskRepository>();
                services.AddSingleton<ISyncService, SyncService>();

                // Data
                services.AddDbContext<AppDbContext>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();

        // Ensure database is created
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}
