<img src="logo.png" alt="Theme Songs" width="96" align="right" />

# Theme Songs

**By Bitstorm Labs** · [Latest release](https://github.com/bitstorm-labs/jellyfin-theme-songs/releases/latest)

A Jellyfin plugin that downloads TV series theme songs and saves them as
`theme.mp3` in each series folder, so Jellyfin's built-in "Play Theme Songs"
feature has something to play.

## What it does

- Runs a **nightly scheduled task** that sweeps your library for series
  without a `theme.mp3` and attempts to download one.
- Optionally fetches a theme **as soon as a new series is added** to the
  library (see settings below).
- Downloads themes from [tvthemes.plexapp.com](https://tvthemes.plexapp.com),
  a free third-party service. **It has no SLA, only covers TV series (not
  movies or music), and has coverage gaps for very recent shows** that
  haven't been added to its catalog yet.
- **Never overwrites** an existing `theme.mp3` — if one is already there
  (however it got there), the plugin leaves it alone.
- Validates every download before writing it (checks it's actually a
  playable MP3, not an error page or truncated file) and writes atomically,
  so a failed or interrupted download can never leave behind a corrupt or
  partial `theme.mp3`.
- Skips series that have no TVDB ID silently, by design. This includes
  things like YouTube-channel "series" some libraries contain — there's
  nothing to look up a theme by, so the plugin doesn't log noise for them.
- Remembers failed attempts and backs off (see `RetryAfterDays` below)
  instead of hammering the upstream service every night for a show that
  simply doesn't have a theme available.

On the reference library this plugin was built against, a full sweep found
themes for **180 of roughly 305 series** — a useful baseline for what
"coverage" looks like in practice, not a guarantee for your library.

## How it works

Both triggers — the nightly sweep and the on-add hook — run the same steps for
each series:

1. **Already has a `theme.mp3`?** Skip. A file you placed yourself always wins.
2. **Find its TVDB ID.** The source is keyed by TVDB, so a series without one is
   skipped silently rather than logged as a failure.
3. **Check recent history.** A series that was looked up recently and had no
   theme is left alone until `RetryAfterDays` has passed.
4. **Download**, throttled to ~1 request/sec to stay polite to a free service.
5. **Validate before writing.** A missing theme returns a small HTML error page,
   not a 404. Unchecked, that page would land on disk named `theme.mp3` and every
   client would try to play it.
6. **Write atomically** — temp file, then rename — so a download interrupted by a
   restart can't leave a half-written file that looks valid.
7. **Refresh the item.** Jellyfin doesn't notice a new `theme.mp3` on its own;
   without this the file exists but nothing plays it.

A genuine "no theme exists" is remembered and retried on the interval. A
*temporary* failure — server error, timeout, a download that fails validation —
is deliberately **not** remembered, so a bad hour upstream can't mark your whole
library as themeless until the interval expires.

## Requirements

- **Jellyfin 10.11 or later.**
- The Jellyfin server process needs **write access** to your TV library
  folders — that's where `theme.mp3` files get created.

## Settings

Configure from Jellyfin's dashboard under Plugins → Theme Songs:

| Setting | Default | Description |
|---|---|---|
| `RetryAfterDays` | 7 | Days to wait before retrying a series whose theme wasn't found last time. |
| `EnableItemAddedHook` | on | Fetch a theme as soon as a new series is added, instead of waiting for the next nightly sweep. |

## Installation

Add this repository to Jellyfin (Dashboard → Plugins → Repositories → Add
Repository) using the manifest URL:

```
https://raw.githubusercontent.com/bitstorm-labs/jellyfin-theme-songs/main/manifest.json
```

Then install "Theme Songs" from Dashboard → Plugins → Catalog and restart
Jellyfin.

> **If Jellyfin says it can't read the repository, wait a minute and retry.**
> `raw.githubusercontent.com` caches for a few minutes, so adding the
> repository within that window of a fresh release can return a stale or
> missing manifest. It is not a configuration problem.

### Installing or upgrading manually

Build from source (see below), then place the DLL in
`<jellyfin-config>/plugins/Theme Songs_<version>/` — for example
`plugins/Theme Songs_1.0.0.0/`.

> **Stop Jellyfin before replacing the DLL.** Overwriting the file while the
> server is running corrupts the memory-mapped assembly, and Jellyfin logs a
> `System.BadImageFormatException: Bad IL range` naming this plugin during its
> *next shutdown*. The plugin itself is fine — the fix is the ordering:
>
> ```bash
> docker stop jellyfin
> cp Jellyfin.Plugin.ThemeSongs.dll "…/plugins/Theme Songs_1.0.0.0/"
> docker start jellyfin
> ```

## Verifying it works

1. **Run it on demand** — Dashboard → Scheduled Tasks → "Download theme
   songs" → Run. It processes roughly one series per second, so a first sweep
   of a large library takes a few minutes.
2. **Check the files** — `find /path/to/media -maxdepth 4 -name theme.mp3 | wc -l`
3. **Check Jellyfin sees them** — a theme only becomes visible after a
   metadata refresh, which the plugin triggers automatically:

   ```
   GET /Items/<seriesId>/ThemeMedia?api_key=<key>
   ```

   `ThemeSongsResult.TotalRecordCount` of 1 means that series has a playable
   theme. It can lag the download by a minute or two.
4. **Hear it** — theme playback is a *client* setting. In Jellyfin web it's
   Settings → Playback → "Play theme songs". Open a series page and it should
   play.

## Building from source

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

## Versioning

Released as `vMAJOR.MINOR.PATCH` tags. Each tag builds the plugin, publishes a
zip to [Releases](https://github.com/bitstorm-labs/jellyfin-theme-songs/releases),
and appends the version to `manifest.json` so the repository URL above always
lists every release. Per-version changelogs are on the release pages.

## License

MIT — see [LICENSE](LICENSE).
