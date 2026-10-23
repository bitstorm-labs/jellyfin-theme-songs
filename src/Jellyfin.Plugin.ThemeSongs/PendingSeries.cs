using System.Collections.Concurrent;

namespace Jellyfin.Plugin.ThemeSongs;

/// <summary>Series the item-added hook saw before they had a TVDB id. Jellyfin raises
/// <c>ItemAdded</c> when a scan first creates the series, before the metadata providers run, so
/// a brand-new series nearly always arrives without one and the hook used to skip it until the
/// nightly sweep. The id is remembered here, and the first <c>ItemUpdated</c> that carries a
/// TVDB id (the identify pass finishing) fetches the theme instead.</summary>
internal sealed class PendingSeries(TimeProvider? clock = null)
{
    /// <summary>A series that has not been identified within this long is left to the nightly sweep.</summary>
    internal static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>Bounds memory if a huge library is added and never identified.</summary>
    internal const int MaxWaiting = 5000;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _waiting = new();

    internal int Count => _waiting.Count;

    /// <summary>True when the theme should be fetched now; otherwise the series is remembered.</summary>
    public bool OnAdded(Guid id, bool hasTvdbId)
    {
        if (hasTvdbId) return true;
        var now = _clock.GetUtcNow();
        if (_waiting.Count >= MaxWaiting) DropExpired(now);
        if (_waiting.Count < MaxWaiting) _waiting[id] = now;
        return false;
    }

    /// <summary>True the first time a remembered series shows up with a TVDB id. Every other
    /// update (playback, edits, series that were never pending) is ignored, so this stays cheap
    /// on a busy server.</summary>
    public bool OnUpdated(Guid id, bool hasTvdbId)
    {
        if (!hasTvdbId || !_waiting.TryRemove(id, out var since)) return false;
        return _clock.GetUtcNow() - since <= Window;
    }

    private void DropExpired(DateTimeOffset now)
    {
        foreach (var (id, since) in _waiting)
        {
            if (now - since > Window) _waiting.TryRemove(id, out _);
        }
    }
}
