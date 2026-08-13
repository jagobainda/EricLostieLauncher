using LostieLauncher.Models;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LostieLauncher.Services;

public interface IContentService
{
    public Task<List<GameInfo>> GetGamesAsync();
    public Task<List<LocalGameInfo>> GetLocalGamesAsync();
    public Task<HomeContent> GetHomeContentAsync(bool forceRefresh = false);
    public Task<bool> IsServerActionBlockedAsync(bool forceRefresh = false, CancellationToken ct = default);
    public string GetGameDirectory(string gameName);
    public Task RegisterGameAsync(Guid gameId, string gameName, string version, string? tipo = null);
    public Task RemoveGameRegistryAsync(string gameName);
    public Task AddPlaytimeAsync(Guid gameId, int minutes);
    public Task<Dictionary<Guid, int>> GetAllPlaytimesAsync();
}

public class ContentService(IHttpClientFactory httpClientFactory, ContentOptions contentOptions, ISettingsService settingsService) : IContentService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ContentOptions _contentOptions = contentOptions;
    private readonly ISettingsService _settingsService = settingsService;
    private const string LocalGamesFileName = "local_games.json";
    private const string PlaytimeFileName = "playtime.json";
    private readonly SemaphoreSlim _homeContentGate = new(1, 1);
    private HomeContentDto? _homeContentCache;
    private volatile ServerActionFlagCache? _serverActionBlockedCache;
    private static readonly TimeSpan ServerActionBlockedCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions RemoteJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        RespectNullableAnnotations = true
    };

    private static readonly TimeZoneInfo CetZone = GetCetZone();

    private static readonly SemaphoreSlim LocalGamesFileLock = new(1, 1);
    private static readonly SemaphoreSlim PlaytimeFileLock = new(1, 1);

    private static TimeZoneInfo GetCetZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time"); }
        catch (TimeZoneNotFoundException)
        {
            Logs.ErrorLogManager("CET timezone not found on this system. Falling back to UTC for expiration comparisons.");
            return TimeZoneInfo.Utc;
        }
    }

    internal static DateTime NormalizeToCet(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(dt, CetZone),
        DateTimeKind.Local => TimeZoneInfo.ConvertTime(dt, CetZone),
        _ => dt
    };

    private sealed record ServerActionFlagCache(bool Blocked, DateTime ExpiresAtUtc);

    public async Task<List<GameInfo>> GetGamesAsync()
    {
        try
        {
            if (await IsServerActionBlockedAsync().ConfigureAwait(false))
            {
                Logs.InfoLogManager("Games list request skipped because server actions are blocked by the maintenance flag.");
                return [];
            }

            Logs.DebugLogManager("Fetching games list from remote.");
            var client = _httpClientFactory.CreateClient("Content");
            using var response = await client.GetAsync(_contentOptions.ContentEndpoint).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var games = JsonSerializer.Deserialize<List<GameInfo>>(json, RemoteJsonOptions) ?? [];
            games = [.. games.Where(g => g is not null)];
            Logs.InfoLogManager($"Fetched {games.Count} games from remote.");
            return games;
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            return [];
        }
    }

    public async Task<HomeContent> GetHomeContentAsync(bool forceRefresh = false)
    {
        try
        {
            HomeContentDto cache;
            await _homeContentGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (forceRefresh) _homeContentCache = null;

                if (_homeContentCache is null)
                {
                    Logs.DebugLogManager("Fetching home content from remote.");
                    var client = _httpClientFactory.CreateClient("Content");
                    using var response = await client.GetAsync(_contentOptions.NotificationsEndpoint).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _homeContentCache = JsonSerializer.Deserialize<HomeContentDto>(json, RemoteJsonOptions) ?? new HomeContentDto([], []);
                    Logs.DebugLogManager($"Home content fetched: {_homeContentCache.News.Count} raw news, {_homeContentCache.Notifications.Count} raw notifications.");
                }
                else
                {
                    Logs.DebugLogManager("Using cached home content.");
                }

                cache = _homeContentCache;
            }
            finally
            {
                _homeContentGate.Release();
            }

            var settings = _settingsService.Load();
            var langCode = GetLanguageCode(settings.Language);

            var nowCet = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CetZone);

            var news = cache.News
                    .Where(n => n is not null && (n.ExpiresAt is null || NormalizeToCet(n.ExpiresAt.Value) > nowCet))
                    .Select(n => new NewsItem
                    {
                        Id = n.Id,
                        Title = Resolve(n.Title, langCode),
                        Description = Resolve(n.Description, langCode),
                        Tag = n.Tag,
                        Date = n.Date,
                        ExpiresAt = n.ExpiresAt
                    }).ToList();
            var notifications = cache.Notifications
                    .Where(n => n is not null && (n.ExpiresAt is null || NormalizeToCet(n.ExpiresAt.Value) > nowCet))
                    .Select(n => new NotificationItem
                    {
                        Id = n.Id,
                        Title = Resolve(n.Title, langCode),
                        Message = Resolve(n.Message, langCode),
                        Type = n.Type,
                        Date = n.Date,
                        ExpiresAt = n.ExpiresAt
                    }).ToList();
            Logs.DebugLogManager($"Home content resolved for lang '{langCode}': {news.Count} news, {notifications.Count} notifications.");

            return new HomeContent
            {
                News = news,
                Notifications = notifications
            };
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            return new HomeContent();
        }
    }

    public async Task<bool> IsServerActionBlockedAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        var cache = _serverActionBlockedCache;
        if (!forceRefresh && cache is not null && DateTime.UtcNow < cache.ExpiresAtUtc)
            return cache.Blocked;

        try
        {
            var blocked = await CheckServerActionFlagAsync(ct).ConfigureAwait(false);
            _serverActionBlockedCache = new ServerActionFlagCache(blocked, DateTime.UtcNow.Add(ServerActionBlockedCacheDuration));
            return blocked;
        }
        catch (OperationCanceledException)
        {
            Logs.InfoLogManager("Maintenance flag check timed out or was cancelled; assuming not blocked.");
            _serverActionBlockedCache = new ServerActionFlagCache(false, DateTime.UtcNow.Add(ServerActionBlockedCacheDuration));
            return false;
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            _serverActionBlockedCache = new ServerActionFlagCache(false, DateTime.UtcNow.Add(ServerActionBlockedCacheDuration));
            return false;
        }
    }

    private async Task<bool> CheckServerActionFlagAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("SecurityFlag");
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, _contentOptions.FlagEndpoint);
        using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (headResponse.StatusCode == HttpStatusCode.MethodNotAllowed)
        {
            using var getRequest = new HttpRequestMessage(HttpMethod.Get, _contentOptions.FlagEndpoint);
            using var getResponse = await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return IsBlockingFlagStatus(getResponse.StatusCode);
        }

        return IsBlockingFlagStatus(headResponse.StatusCode);
    }

    private static bool IsBlockingFlagStatus(HttpStatusCode statusCode)
    {
        if ((int)statusCode is >= 200 and <= 299)
        {
            Logs.InfoLogManager("Maintenance flag detected. Server-backed actions are temporarily blocked.");
            return true;
        }

        Logs.DebugLogManager($"Maintenance flag check: status {(int)statusCode} — not blocking.");
        return false;
    }

    private static string GetLanguageCode(AppLanguage language) => language switch
    {
        AppLanguage.Esp => "es",
        AppLanguage.Eng => "en",
        AppLanguage.Cat => "ca",
        AppLanguage.Eus => "eu",
        AppLanguage.Gal => "gl",
        AppLanguage.Por => "pt",
        AppLanguage.Val => "val",
        AppLanguage.Fra => "fr",
        _ => "es"
    };

    internal static string Resolve(Dictionary<string, string>? localized, string languageCode)
    {
        if (localized is null) return string.Empty;
        if (localized.TryGetValue(languageCode, out var value) && value is not null) return value;
        if (localized.TryGetValue("es", out var spanish) && spanish is not null) return spanish;
        if (localized.TryGetValue("en", out var english) && english is not null) return english;

        return localized
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Value)
            .FirstOrDefault(v => v is not null) ?? string.Empty;
    }

    private record HomeContentDto(List<NewsItemDto> News, List<NotificationItemDto> Notifications);

    private record NewsItemDto(
        Guid Id,
        Dictionary<string, string> Title,
        Dictionary<string, string> Description,
        string Tag,
        DateTime Date,
        [property: JsonPropertyName("expires_at")] DateTime? ExpiresAt);

    private record NotificationItemDto(
        Guid Id,
        Dictionary<string, string> Title,
        Dictionary<string, string> Message,
        NotificationType Type,
        DateTime Date,
        [property: JsonPropertyName("expires_at")] DateTime? ExpiresAt);

    public async Task<List<LocalGameInfo>> GetLocalGamesAsync()
    {
        await LocalGamesFileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Logs.DebugLogManager("Reading local games registry.");
            var gamesRoot = _settingsService.GetGamesRootDirectory();
            var path = Path.Combine(gamesRoot, LocalGamesFileName);
            if (!File.Exists(path)) return [];

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var games = JsonSerializer.Deserialize<List<LocalGameInfo>>(json, JsonOptions) ?? [];
            var deduplicated = DeduplicateLocalGames(games);
            Logs.DebugLogManager($"Local games registry loaded: {deduplicated.Count} games.");
            return deduplicated;
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            return [];
        }
        finally { LocalGamesFileLock.Release(); }
    }

    private static List<LocalGameInfo> DeduplicateLocalGames(List<LocalGameInfo> games)
    {
        var seenIds = new HashSet<Guid>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduplicated = new List<LocalGameInfo>(games.Count);

        foreach (var game in games)
        {
            if (game is null) continue;

            var isDuplicate = game.Id != Guid.Empty ? !seenIds.Add(game.Id) : !seenNames.Add(game.Nombre ?? string.Empty);
            if (isDuplicate)
            {
                Logs.InfoLogManager($"Skipping duplicate local game entry: '{game.Nombre}' (id: {game.Id}).");
                continue;
            }

            deduplicated.Add(game);
        }

        return deduplicated;
    }

    public string GetGameDirectory(string gameName)
    {
        var gamesRoot = _settingsService.GetGamesRootDirectory();
        var resolved = Path.GetFullPath(Path.Combine(gamesRoot, gameName));
        var canonicalRoot = Path.GetFullPath(gamesRoot) + Path.DirectorySeparatorChar;

        if (!resolved.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Invalid game directory: '{gameName}' escapes the games root.");

        return resolved;
    }

    public async Task RegisterGameAsync(Guid gameId, string gameName, string version, string? tipo = null)
    {
        await LocalGamesFileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Logs.DebugLogManager($"Registering game in local registry: {gameName} {VersionUtils.FormatDisplayVersion(version)}{(tipo is not null ? $" ({tipo})" : "")}.");
            var gamesRoot = _settingsService.GetGamesRootDirectory();
            Directory.CreateDirectory(gamesRoot);
            var path = Path.Combine(gamesRoot, LocalGamesFileName);

            List<LocalGameInfo> games = [];
            if (File.Exists(path))
            {
                var existingJson = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                games = JsonSerializer.Deserialize<List<LocalGameInfo>>(existingJson, JsonOptions) ?? [];
            }

            games.RemoveAll(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
            games.Add(new LocalGameInfo { Id = gameId, Nombre = gameName, Version = version, Tipo = tipo });

            await WriteAllTextAtomicAsync(path, JsonSerializer.Serialize(games, JsonOptions)).ConfigureAwait(false);
            Logs.InfoLogManager($"Game registered in local registry: {gameName} {VersionUtils.FormatDisplayVersion(version)}{(tipo is not null ? $" ({tipo})" : "")}.");
        }
        catch (Exception ex) { Logs.ErrorLogManager(ex); }
        finally { LocalGamesFileLock.Release(); }
    }

    public async Task RemoveGameRegistryAsync(string gameName)
    {
        await LocalGamesFileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var gamesRoot = _settingsService.GetGamesRootDirectory();
            var path = Path.Combine(gamesRoot, LocalGamesFileName);
            if (!File.Exists(path)) return;

            Logs.DebugLogManager($"Removing game from registry: {gameName}.");
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var games = JsonSerializer.Deserialize<List<LocalGameInfo>>(json, JsonOptions) ?? [];
            List<LocalGameInfo> updated = [.. games.Where(g => !string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase))];
            await WriteAllTextAtomicAsync(path, JsonSerializer.Serialize(updated, JsonOptions)).ConfigureAwait(false);
            Logs.InfoLogManager($"Game removed from registry: {gameName}.");
        }
        catch (Exception ex) { Logs.ErrorLogManager(ex); }
        finally { LocalGamesFileLock.Release(); }
    }

    public async Task AddPlaytimeAsync(Guid gameId, int minutes)
    {
        if (gameId == Guid.Empty) return;

        await PlaytimeFileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var gamesRoot = _settingsService.GetGamesRootDirectory();
            var path = Path.Combine(gamesRoot, PlaytimeFileName);

            Logs.DebugLogManager($"Adding {minutes} playtime minutes for game id: {gameId}.");
            List<PlaytimeRecord> records = [];
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                records = JsonSerializer.Deserialize<List<PlaytimeRecord>>(json, JsonOptions) ?? [];
            }

            var existing = records.FirstOrDefault(r => r.Id == gameId);
            if (existing is not null)
                records = [.. records.Select(r => r.Id == gameId
                    ? new PlaytimeRecord { Id = r.Id, PlaytimeMinutes = r.PlaytimeMinutes + minutes }
                    : r)];
            else
                records.Add(new PlaytimeRecord { Id = gameId, PlaytimeMinutes = minutes });

            Directory.CreateDirectory(gamesRoot);
            await WriteAllTextAtomicAsync(path, JsonSerializer.Serialize(records, JsonOptions)).ConfigureAwait(false);
            Logs.DebugLogManager($"Playtime recorded: {minutes} min for game id {gameId}.");
        }
        catch (Exception ex) { Logs.ErrorLogManager(ex); }
        finally { PlaytimeFileLock.Release(); }
    }

    private static async Task WriteAllTextAtomicAsync(string path, string content)
    {
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, content).ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }

    public async Task<Dictionary<Guid, int>> GetAllPlaytimesAsync()
    {
        await PlaytimeFileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var gamesRoot = _settingsService.GetGamesRootDirectory();
            var path = Path.Combine(gamesRoot, PlaytimeFileName);
            if (!File.Exists(path)) return [];

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var records = JsonSerializer.Deserialize<List<PlaytimeRecord>>(json, JsonOptions) ?? [];
            Logs.DebugLogManager($"Playtimes loaded: {records.Count} records.");
            return records
                .Where(r => r.Id != Guid.Empty)
                .ToDictionary(r => r.Id, r => r.PlaytimeMinutes);
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            return [];
        }
        finally { PlaytimeFileLock.Release(); }
    }
}
