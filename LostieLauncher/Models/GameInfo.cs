using CommunityToolkit.Mvvm.ComponentModel;
using LostieLauncher.Core;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LostieLauncher.Models;

public partial class GameInfo : ObservableObject
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; init; } = string.Empty;

    [JsonPropertyName("pesoGB")]
    public double PesoGB { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("rutaRelativa")]
    public string RutaRelativa { get; init; } = string.Empty;

    [JsonPropertyName("logo")]
    public string Logo { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonIgnore]
    public string? LogoUrl => string.IsNullOrEmpty(Logo) ? null : $"{Endpoints.CdnBaseUrl}{Logo}";

    [JsonIgnore]
    public string GameId => SlugRegex().Replace((Nombre ?? string.Empty).ToLowerInvariant(), "-").Trim('-');

    [JsonIgnore]
    public string PesoFormateado => PesoGB >= 1 ? $"{PesoGB.ToString("0.#", CultureInfo.InvariantCulture)} GB" : $"{(PesoGB * 1024).ToString("0", CultureInfo.InvariantCulture)} MB";

    [ObservableProperty]
    [JsonIgnore]
    public partial GameDownloadStatus DownloadStatus { get; set; } = GameDownloadStatus.Available;

    [ObservableProperty]
    [JsonIgnore]
    public partial double DownloadProgressValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadSpeedText))]
    [JsonIgnore]
    public partial double DownloadSpeedBytesPerSec { get; set; }

    [ObservableProperty]
    [JsonIgnore]
    public partial string DownloadRemainingText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaytimeText))]
    [JsonIgnore]
    public partial int PlaytimeMinutes { get; set; }

    [JsonIgnore]
    public string PlaytimeText => PlaytimeFormatter.Format(PlaytimeMinutes);

    [JsonIgnore]
    public string DownloadSpeedText => DownloadSpeedBytesPerSec switch
    {
        >= 1_048_576 => $"{DownloadSpeedBytesPerSec / 1_048_576.0:0.0} MB/s",
        > 0 => $"{DownloadSpeedBytesPerSec / 1024.0:0.0} KB/s",
        _ => "0 KB/s"
    };

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
