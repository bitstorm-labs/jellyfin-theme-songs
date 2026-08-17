using System.Text.Json;

namespace Jellyfin.Plugin.ThemeSongs;

/// <summary>Remembers which TVDB ids were tried and failed, so a nightly sweep
/// does not re-request a theme that does not exist. Only failures are stored —
/// a success leaves theme.mp3 on disk, which is its own record.</summary>
public class AttemptStore(string path)
{
    private Dictionary<string, DateTimeOffset> _failures = new();

    public bool ShouldTry(string tvdbId, int retryAfterDays, DateTimeOffset now)
        => !_failures.TryGetValue(tvdbId, out var last)
           || now - last >= TimeSpan.FromDays(retryAfterDays);

    public void RecordFailure(string tvdbId, DateTimeOffset now) => _failures[tvdbId] = now;

    public void Clear() => _failures.Clear();

    public async Task LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(path)) return;
        await using var stream = File.OpenRead(path);
        _failures = await JsonSerializer
            .DeserializeAsync<Dictionary<string, DateTimeOffset>>(stream, cancellationToken: ct)
            .ConfigureAwait(false) ?? new();
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, _failures, cancellationToken: ct).ConfigureAwait(false);
    }
}
