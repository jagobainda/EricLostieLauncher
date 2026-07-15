using LostieLauncher.Models;
using LostieLauncher.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LostieLauncher.Tests.Helpers;

/// <summary>
/// Builds a minimal <see cref="IServiceProvider"/> for tests, mirroring the shape of
/// <c>LostieLauncher.Core.DependencyInjection</c> but with every external dependency
/// replaced by a test double:
/// <list type="bullet">
///   <item><description><see cref="IHttpClientFactory"/> -> <see cref="HttpClientFactoryStub"/></description></item>
///   <item><description><see cref="ISettingsService"/>, <see cref="IWindowsStartupService"/>, etc. -> NSubstitute mocks</description></item>
/// </list>
/// Tests can replace any registration via <see cref="With{TService}(TService)"/> before
/// calling <see cref="Build"/>.
/// </summary>
public sealed class TestServiceProviderBuilder
{
    private readonly ServiceCollection _services = [];
    private readonly HttpClientFactoryStub _httpClientFactory = new();

    public HttpClientFactoryStub HttpClientFactory => _httpClientFactory;

    public TestServiceProviderBuilder()
    {
        // Default option records (mirror production). Tests may override via With<T>().
        _services.AddSingleton(new ContentOptions(
            ContentEndpoint: "https://content.test/list.json",
            NotificationsEndpoint: "https://content.test/notifications.json",
            FlagEndpoint: "https://content.test/flag.txt"));
        _services.AddSingleton(new DownloadOptions(BaseUrl: "https://download.test/"));

        // Stubbed infrastructure.
        _services.AddSingleton<IHttpClientFactory>(_httpClientFactory);
        _services.AddSingleton(Substitute.For<ISettingsService>());
        _services.AddSingleton(Substitute.For<IWindowsStartupService>());
        _services.AddSingleton(Substitute.For<IContentService>());
        _services.AddSingleton(Substitute.For<IDownloadService>());

        // ViewModels under test are registered on demand via With<T>(); we don't register
        // them by default to avoid pulling their constructors (which often kick off
        // background tasks) into every fixture.
    }

    /// <summary>Replace or add a singleton instance for <typeparamref name="TService"/>.</summary>
    public TestServiceProviderBuilder With<TService>(TService instance) where TService : class
    {
        // Remove any prior registration of the same service type to ensure deterministic resolution.
        for (var i = _services.Count - 1; i >= 0; i--)
        {
            if (_services[i].ServiceType == typeof(TService)) _services.RemoveAt(i);
        }
        _services.AddSingleton(instance);
        return this;
    }

    /// <summary>Register a concrete type so it can be resolved by the container.</summary>
    public TestServiceProviderBuilder AddSingleton<TService>() where TService : class
    {
        _services.AddSingleton<TService>();
        return this;
    }

    public ServiceProvider Build() => _services.BuildServiceProvider(validateScopes: true);
}
