using Microsoft.Extensions.Logging;

namespace Overmind.Infrastructure.Embeddings;

public sealed class ModelDownloader
{
    private const int BufferSize = 64 * 1024;

    private readonly HttpClient _http;
    private readonly ILogger<ModelDownloader>? _logger;

    public ModelDownloader(HttpClient http, ILogger<ModelDownloader>? logger = null)
    {
        _http = http;
        _logger = logger;
    }

    public async Task EnsureAsync(
        string directory,
        IReadOnlyList<ModelFileSpec> files,
        Action<string, long, long>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(directory);

        foreach (var file in files)
        {
            var targetPath = Path.Combine(directory, file.FileName);
            if (File.Exists(targetPath))
            {
                continue;
            }

            await DownloadAsync(file, targetPath, progress, ct);
        }
    }

    private async Task DownloadAsync(ModelFileSpec file, string targetPath, Action<string, long, long>? progress, CancellationToken ct)
    {
        var tempPath = targetPath + ".tmp";
        try
        {
            _logger?.LogInformation("Downloading {FileName} from {Url}", file.FileName, file.Url);

            using var response = await _http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            long readBytes = 0;
            await using (var source = await response.Content.ReadAsStreamAsync(ct))
            await using (var target = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[BufferSize];
                int count;
                while ((count = await source.ReadAsync(buffer.AsMemory(0, BufferSize), ct)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, count), ct);
                    readBytes += count;
                    progress?.Invoke(file.FileName, readBytes, totalBytes);
                }
            }

            File.Move(tempPath, targetPath);
            _logger?.LogInformation("Downloaded {FileName} ({Bytes} bytes)", file.FileName, readBytes);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }
}
