# Theme Songs

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

> **Note for the maintainer:** this URL only serves a real manifest once the
> repository has been pushed to GitHub and at least one release has been
> tagged (`git tag -a vX.Y.Z && git push origin vX.Y.Z`) — the release job in
> `.github/workflows/build.yml` is what populates `manifest.json` with a
> version entry and commits it back to `main`. Until that first tag exists,
> `manifest.json` in this repo is a valid but empty skeleton (no installable
> versions listed), and the raw GitHub URL above will 404 until the repo
> itself exists on GitHub. See `.superpowers/sdd/2026-08-17-theme-songs-plugin-plan/task-8-report.md`
> for what has and hasn't been done yet.

Alternatively, build from source (see below) and drop the resulting DLL into
your Jellyfin `plugins/Theme Songs/` folder manually.

## Building from source

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
```
