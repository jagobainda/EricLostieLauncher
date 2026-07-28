using LostieLauncher.Models;
using LostieLauncher.Services;
using LostieLauncher.ViewModels;
using LostieLauncher.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace LostieLauncher.Core;

public static class DependencyInjection
{
    private const string ContentEndpoint = $"{Endpoints.CdnBaseUrl}/games/listado.json";
    private const string NotificationsEndpoint = "https://cdn.jagoba.dev/ericlostie-launcher/homepage-notifications.json";
    private const string FlagEndpoint = "https://cdn.jagoba.dev/ericlostie-launcher/flag.txt";
    private const string DownloadBaseUrl = $"{Endpoints.CdnBaseUrl}/games";
    private const string UpdateFeedUrl = $"{Endpoints.CdnBaseUrl}/public/installer/";

    internal static readonly string UserAgent = $"LostieLauncher/{ResolveVersion()}";

    private static string ResolveVersion()
    {
        var version = typeof(DependencyInjection).Assembly.GetName().Version;
        if (version is null) return "unknown";

        var availableFields = version.Revision >= 0 ? 4 : version.Build >= 0 ? 3 : 2;
        return version.ToString(Math.Min(3, availableFields));
    }

    public static IServiceProvider Configure()
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(new ContentOptions(ContentEndpoint: ContentEndpoint, NotificationsEndpoint: NotificationsEndpoint, FlagEndpoint: FlagEndpoint));
        services.AddSingleton(new DownloadOptions(BaseUrl: DownloadBaseUrl));
        services.AddSingleton(new UpdateOptions(FeedUrl: UpdateFeedUrl));

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IWindowsStartupService, WindowsStartupService>();
        services.AddSingleton<IContentService, ContentService>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IUpdateGateway, VelopackUpdateGateway>();
        services.AddSingleton<IUpdateNotifier, WpfUpdateNotifier>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddHttpClient("Content", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        services.AddHttpClient("SecurityFlag", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(3);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        services.AddHttpClient("Download", client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        })
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
