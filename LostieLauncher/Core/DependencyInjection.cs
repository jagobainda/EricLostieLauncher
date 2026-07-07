using LostieLauncher.Models;
using LostieLauncher.Services;
using LostieLauncher.ViewModels;
using LostieLauncher.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace LostieLauncher.Core;

public static class DependencyInjection
{
    private const string TelemetryEndpoint = $"{Endpoints.CdnBaseUrl}/";
    private const string ContentEndpoint = $"{Endpoints.CdnBaseUrl}/games/listado.json";
    private const string NotificationsEndpoint = "https://cdn.jagoba.dev/ericlostie-launcher/homepage-notifications.json";
    private const string FlagEndpoint = "https://cdn.jagoba.dev/ericlostie-launcher/flag.txt";
    private const string DownloadBaseUrl = $"{Endpoints.CdnBaseUrl}/games";
    private const string UpdateFeedUrl = $"{Endpoints.CdnBaseUrl}/public/installer/";

    public static IServiceProvider Configure()
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(new TelemetryOptions(ApiKey: "4V7p0XSJ9C6FgCE7ae3c", Endpoint: TelemetryEndpoint));
        services.AddSingleton(new ContentOptions(ContentEndpoint: ContentEndpoint, NotificationsEndpoint: NotificationsEndpoint, FlagEndpoint: FlagEndpoint));
        services.AddSingleton(new DownloadOptions(BaseUrl: DownloadBaseUrl));
        services.AddSingleton(new UpdateOptions(FeedUrl: UpdateFeedUrl));

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IWindowsStartupService, WindowsStartupService>();
        services.AddSingleton<IContentService, ContentService>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IUpdateGateway, VelopackUpdateGateway>();
        services.AddSingleton<IUpdateNotifier, WpfUpdateNotifier>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddHttpClient("Telemetry", client => { client.Timeout = TimeSpan.FromSeconds(5); });
        services.AddHttpClient("Content", client => { client.Timeout = TimeSpan.FromSeconds(10); });
        services.AddHttpClient("SecurityFlag", client => { client.Timeout = TimeSpan.FromSeconds(3); });
        services.AddHttpClient("Download", client => { client.Timeout = Timeout.InfiniteTimeSpan; })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(20) });

        // ViewModels
        services.AddSingleton<GlobalViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<GamesViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<FaqsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>();

        var provider = services.BuildServiceProvider();
        Logs.InfoLogManager("Dependency injection container built successfully.");
        return provider;
    }
}
