<div align="center">

# Deskboard Native

**The cross-platform rewrite of Deskboard — a native Tumblr client for Windows, macOS and Linux.**

</div>

> **Status: early scaffold.** This repository is the *start* of the cross-platform
> rewrite. The working Linux proof of concept lives in
> [`Wrecht-Dev/deskboard`](https://github.com/Wrecht-Dev/deskboard) and documents
> Tumblr's API surface in depth; this repository will grow into a client that runs
> on all three desktop platforms.

## Why a rewrite

The proof of concept was Python + GTK, which is Linux-only by construction. This
rewrite targets **one codebase for Windows, macOS and Linux**, keeping the things
that made the PoC good (native performance, no webview, Tumblr-faithful UI) while
dropping the platform lock-in.

## Stack: C# / .NET 10 + Avalonia

Chosen after comparing the realistic cross-platform desktop stacks:

| Stack | Verdict |
|---|---|
| **C# + Avalonia** ✅ | Real native window chrome on all three OSes, mature virtualised lists for infinite feeds, async/await networking, sane media/text story, single language for core + UI, excellent tooling. |
| Flutter (Dart) | Strong UI velocity and media ecosystem, but custom-rendered UI (less platform-native), heavier toolchain; good runner-up. |
| Rust + Slint/Iced | Best raw performance and binary size, weakest ecosystem for video/audio/rich text — too much cost for this app. |
| Kotlin/Compose Multiplatform | Desktop story is JVM-heavy; weaker Windows/macOS packaging. |

Full decision record: [`docs/REWRITE.md`](docs/REWRITE.md).

## What works right now

- **Live read-only feed** against Tumblr's API: Home (For you), Following, Likes, Explore.
- Tumblr-faithful shell: navy left rail, centred card feed, right tag rail.
- Post rendering: header, reblog provenance, text blocks, media, tag pills, action row.
- Session reuse: reads the cookies the Linux PoC already imported, or `DESKBOARD_COOKIES`.
- Public web token discovered and cached locally — **no credentials in this repo**.

## Screenshot

![Early Avalonia scaffold rendering a live Tumblr feed](docs/screenshots/scaffold-dark.png)

## Build & run

Requires the **.NET 10 SDK**.

```bash
dotnet build
dotnet run --project src/Deskboard.App
```

Session cookies, in order of precedence:

1. `DESKBOARD_COOKIES` — a JSON dictionary of cookie name → value.
2. `~/.local/share/deskboard/cookies.json` — the Linux PoC's store (reused as-is).
3. No session → public/Explore-only content.

## Layout

```
src/Deskboard.Core/   API client, models, cookie loading   (no UI dependency)
src/Deskboard.App/    Avalonia UI: views, view models, theme
docs/API.md           the mapped Tumblr API surface
docs/REWRITE.md       stack decision + design reference
docs/reference/       real Tumblr UI screenshots used as design reference
```

## Roadmap

1. **Read everything** — notifications, messages/inbox, blog pages, tags (Latest/Top),
   search, notes. (PoC proves every endpoint; port in progress.)
2. **Write actions** — like, reblog (text/post-only/scheduled), follow, reply, ask, DM.
3. **Composer** — NPF text/quote/link/photo, uploads, publish/queue/draft/schedule.
4. **Platform polish** — native menus, tray, Windows/macOS packaging, auto-update.

## Security

No credentials are committed. Session cookies live only in your local store, the
public web token is fetched at first launch and cached `0600`, and the app talks
only to `www.tumblr.com` and Tumblr's CDNs. See the PoC's `SECURITY.md`.

## License

MIT.
