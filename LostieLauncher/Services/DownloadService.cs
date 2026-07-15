using LostieLauncher.Models;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LostieLauncher.Services;

public interface IDownloadService
{
    public Task<SpecialVersionConfigResult> FetchSpecialVersionConfigAsync(string key, CancellationToken ct = default);

    public Task<DownloadResult> DownloadAsync(string url, string destinationPath, IProgress<DownloadProgressInfo>? progress = null, CancellationToken ct = default);
}

public class DownloadService : IDownloadService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DownloadOptions _downloadOptions;
    private readonly TimeSpan _inactivityTimeout;

    private const int BufferSize = 64 * 1024;
    private const int MaxRetries = 2;
    private static readonly TimeSpan DefaultInactivityTimeout = TimeSpan.FromSeconds(60);

    public DownloadService(IHttpClientFactory httpClientFactory, DownloadOptions downloadOptions)
        : this(httpClientFactory, downloadOptions, DefaultInactivityTimeout)
    {
    }

    internal DownloadService(IHttpClientFactory httpClientFactory, DownloadOptions downloadOptions, TimeSpan inactivityTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(inactivityTimeout, TimeSpan.Zero);
        _httpClientFactory = httpClientFactory;
        _downloadOptions = downloadOptions;
        _inactivityTimeout = inactivityTimeout;
    }

    public async Task<SpecialVersionConfigResult> FetchSpecialVersionConfigAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Content");
            var configUrl = $"{_downloadOptions.BaseUrl}/{key}/game.config";
            using var response = await client.GetAsync(configUrl, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Logs.InfoLogManager($"Special version config not found for key: {key}");
                return SpecialVersionConfigResult.NotFound();
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var config = SpecialVersionConfig.Parse(content);

            if (config is null)
            {
                Logs.InfoLogManager($"Failed to parse special version config for key: {key}");
                return SpecialVersionConfigResult.InvalidResponse();
            }

            Logs.InfoLogManager($"Special version config loaded: {config.Tipo} v{config.Version}");
            return SpecialVersionConfigResult.Success(config);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Logs.InfoLogManager($"Special version config fetch cancelled for key: {key}");
            return SpecialVersionConfigResult.Cancelled();
        }
        catch (OperationCanceledException ex)
        {
            Logs.ErrorLogManager(ex);
            return SpecialVersionConfigResult.NetworkError();
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            return SpecialVersionConfigResult.NetworkError();
        }
    }

    public async Task<DownloadResult> DownloadAsync(string url, string destinationPath, IProgress<DownloadProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var partPath = Utils.DownloadPathUtils.GetPartFilePath(destinationPath);

        try
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            Exception? lastError = null;

            for (var attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await DownloadCoreAsync(url, partPath, destinationPath, progress, ct).ConfigureAwait(false);
                    return DownloadResult.Succeeded();
                }
                catch (OperationCanceledException)
                {
                    Logs.InfoLogManager("Download paused by user.");
                    return DownloadResult.Cancelled();
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException && !ct.IsCancellationRequested)
                {
                    lastError = ex;

                    if (attempt < MaxRetries)
                    {
                        Logs.InfoLogManager($"Download attempt {attempt + 1}/{MaxRetries + 1} failed ({ex.Message}), retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(attempt + 1), CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }

            Logs.ErrorLogManager($"Download failed after {MaxRetries + 1} attempts, giving up. Last error: {lastError?.Message}");
            return DownloadResult.Failed("Download failed after maximum retries.");
        }
        catch (UnauthorizedAccessException ex)
        {
            Logs.ErrorLogManager(ex);
            return DownloadResult.PermissionDenied();
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            return DownloadResult.Failed(ex.Message);
        }
    }

    private async Task DownloadCoreAsync(string url, string partPath, string finalPath, IProgress<DownloadProgressInfo>? progress, CancellationToken ct)
    {
        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await DownloadCoreWatchedAsync(url, partPath, finalPath, progress, ct, watchdog).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new IOException($"Download stalled: no data received within {_inactivityTimeout.TotalSeconds:0}s.", ex);
        }
    }

    private async Task DownloadCoreWatchedAsync(string url, string partPath, string finalPath, IProgress<DownloadProgressInfo>? progress, CancellationToken ct, CancellationTokenSource watchdog)
    {
        var client = _httpClientFactory.CreateClient("Download");
        var metaPath = Utils.DownloadPathUtils.GetMetaFilePath(partPath);
        long existingBytes = 0;
        DownloadResumeMetadata? meta = null;

        if (File.Exists(partPath))
        {
            existingBytes = new FileInfo(partPath).Length;
            meta = ReadResumeMetadata(metaPath);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (existingBytes > 0)
        {
            if (TrySetIfRange(request, meta))
            {
                request.Headers.Range = new RangeHeaderValue(existingBytes, null);
                Logs.DebugLogManager($"Resuming download from byte {existingBytes}.");
            }
            else
            {
                Logs.InfoLogManager("No usable If-Range validator for the partial file — restarting download from scratch to avoid corruption.");
                File.Delete(partPath);
                DeleteResumeMetadata(metaPath);
                existingBytes = 0;
                meta = null;
            }
        }
        else
        {
            Logs.DebugLogManager("Starting fresh download.");
        }

        watchdog.CancelAfter(_inactivityTimeout);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, watchdog.Token).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            if (meta?.TotalBytes is long expectedTotal && existingBytes == expectedTotal)
            {
                Logs.InfoLogManager("Server returned 416 — partial file matches expected size, treating as complete.");
                File.Move(partPath, finalPath, overwrite: true);
                DeleteResumeMetadata(metaPath);
                progress?.Report(new DownloadProgressInfo(100, 0));
                return;
            }

            Logs.InfoLogManager("Server returned 416 but the partial file size is unexpected — discarding and restarting.");
            File.Delete(partPath);
            DeleteResumeMetadata(metaPath);
            throw new IOException("Partial download is invalid (416 with size mismatch); restarting.");
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            if (existingBytes > 0)
            {
                Logs.InfoLogManager("Server returned 200 instead of 206 — remote resource changed, restarting download from scratch.");
                File.Delete(partPath);
                existingBytes = 0;
            }

            meta = BuildResumeMetadata(response);
            WriteResumeMetadata(metaPath, meta);
        }
        else if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            Logs.DebugLogManager("Server returned 206 Partial Content — resuming.");
        }
        else
        {
            response.EnsureSuccessStatusCode();
        }

        var contentLength = response.Content.Headers.ContentLength;
        var totalBytes = contentLength.HasValue ? contentLength.Value + existingBytes : -1;

        await using var contentStream = await response.Content.ReadAsStreamAsync(watchdog.Token).ConfigureAwait(false);
        var fileMode = existingBytes > 0 ? FileMode.Append : FileMode.Create;

        await using (var fileStream = new FileStream(partPath, fileMode, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
        {
            var buffer = new byte[BufferSize];
            var totalRead = existingBytes;
            int bytesRead;
            var speedwatch = Stopwatch.StartNew();
            var lastSpeedBytes = existingBytes;
            double currentSpeed = 0;

            while (true)
            {
                watchdog.CancelAfter(_inactivityTimeout);
                bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, BufferSize), watchdog.Token).ConfigureAwait(false);
                if (bytesRead <= 0) break;

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                totalRead += bytesRead;

                var elapsedMs = speedwatch.ElapsedMilliseconds;
                if (elapsedMs >= 500)
                {
                    var deltaBytes = totalRead - lastSpeedBytes;
                    currentSpeed = deltaBytes / (elapsedMs / 1000.0);
                    lastSpeedBytes = totalRead;
                    speedwatch.Restart();
                }

                if (totalBytes > 0)
                {
                    progress?.Report(new DownloadProgressInfo((double)totalRead / totalBytes * 100.0, currentSpeed, totalBytes, totalRead));
                }
            }

            await fileStream.FlushAsync(CancellationToken.None).ConfigureAwait(false);

            Logs.DebugLogManager($"Download data received: {totalRead} bytes total.");
        }
        File.Move(partPath, finalPath, overwrite: true);
        DeleteResumeMetadata(metaPath);
        progress?.Report(new DownloadProgressInfo(100, 0));
        Logs.InfoLogManager("Download completed and file finalized.");
    }

    private sealed record DownloadResumeMetadata(string? ETag, DateTimeOffset? LastModified, long? TotalBytes);

    private static DownloadResumeMetadata BuildResumeMetadata(HttpResponseMessage response)
    {
        var etag = response.Headers.ETag;

        var strongETag = etag is not null && !etag.IsWeak ? etag.ToString() : null;
        return new DownloadResumeMetadata(strongETag, response.Content.Headers.LastModified, response.Content.Headers.ContentLength);
    }

    private static bool TrySetIfRange(HttpRequestMessage request, DownloadResumeMetadata? meta)
    {
        if (meta is null) return false;

        if (!string.IsNullOrEmpty(meta.ETag))
        {
            try
            {
                var tag = EntityTagHeaderValue.Parse(meta.ETag);
                if (!tag.IsWeak)
                {
                    request.Headers.IfRange = new RangeConditionHeaderValue(tag);
                    return true;
                }
            }
            catch (FormatException ex)
            {
                Logs.ErrorLogManager(ex);
            }
        }

        if (meta.LastModified.HasValue)
        {
            request.Headers.IfRange = new RangeConditionHeaderValue(meta.LastModified.Value);
            return true;
        }

        return false;
    }

    private static DownloadResumeMetadata? ReadResumeMetadata(string metaPath)
    {
        try
        {
            if (!File.Exists(metaPath)) return null;
            return JsonSerializer.Deserialize<DownloadResumeMetadata>(File.ReadAllText(metaPath));
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            return null;
        }
    }

    private static void WriteResumeMetadata(string metaPath, DownloadResumeMetadata meta)
    {
        try
        {
            File.WriteAllText(metaPath, JsonSerializer.Serialize(meta));
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
        }
    }

    private static void DeleteResumeMetadata(string metaPath)
    {
        try { if (File.Exists(metaPath)) File.Delete(metaPath); }
        catch (Exception ex) { Logs.ErrorLogManager(ex); }
    }
}
