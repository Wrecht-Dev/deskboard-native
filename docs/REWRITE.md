# Rewrite decision record and design reference

## 1. Language / framework decision

Requirements: one codebase for **Windows, macOS, Linux**; native-feeling desktop
app (no Electron/webview); smooth infinite feeds with images and video; rich text
(NPF blocks); large API surface (73 client methods in the PoC); maintainable by one
developer.

| Candidate | Strengths | Weaknesses | Score |
|---|---|---|---|
| **C# / .NET + Avalonia** | Native chrome on all 3 OSes; `ItemsControl` + virtualising panels; async HTTP; mature text/layout; single language; strong IDE/tooling; easy Windows/macOS packaging | Smaller UI community than Flutter; media needs a package | **9/10** |
| Flutter | Fast UI iteration; excellent lists, images, video (`media_kit`); one codebase | Custom-rendered (not platform-native fields/dialogs), large toolchain, Dart | 8/10 |
| Rust + Slint | Best performance/size; declarative UI | Media/audio/video and rich text immature; slow iteration | 6/10 |
| Rust + Iced | Pure Rust, GPU | Ecosystem small; all widgets custom | 5/10 |
| Kotlin + Compose Multiplatform | Shared UI with Android | Desktop = JVM, heavier startup/packaging; macOS story weaker | 6/10 |
| Tauri / Electron | Fast to build | Webview — contradicts the app's premise | 2/10 |

**Decision: C# / .NET 10 + Avalonia 12.** It is the best match for a
desktop-only, media-heavy, feed-heavy client that must feel native on Windows and
macOS, and it keeps the API core in a testable, UI-free assembly.

## 2. Design reference (captured from the real Tumblr desktop UI)

The design reference was captured from tumblr.com at 1440×1000 and reduced to the
tokens below (the captures themselves are not committed, since they were taken in
a signed-in session and could include personal content).

### Tokens sampled from the live site

| Token | Value |
|---|---|
| Rails / chrome | `#001935` (Tumblr navy) |
| Page background (dark) | `#0A0A0A` – `#0D0D0D` |
| Card background (dark) | `#161616` (hover `#1D1D1D`) |
| Accent (buttons, links) | `#00B8FF` |
| Tag text | `#7CC4FF` |
| Primary text | `#FFFFFF` |
| Secondary text | `#8C9AA8` |
| Typeface | `Favorit` (fallback Helvetica Neue / Inter) |
| Base size | 16 px, post body ~15 px, line-height ~1.45 |
| Card radius | 8–10 px |
| Rail width | ~232 px; feed column ~640 px; right rail ~324 px |

### Layout observed

- **Left rail:** wordmark, Home, Explore, Communities, Activity, Messages, Inbox,
  Account, Settings, TumblrMart; bottom "Go Premium" + bright **Create** button.
- **Centre:** single centred column of post cards, generous spacing, no card
  borders on dark (surface elevation only).
- **Post anatomy:** avatar + blog name + handle, reblog provenance line, body
  text, media, blue tag list, action row (reply · reblog · like · share) with
  counts, `⋯` menu.
- **Right rail:** tag header card (name, description, follower/recent counts,
  Follow + New post buttons), related tag pills, sponsored slot.

The scaffold in `src/Deskboard.App` already reproduces the three-column shell,
navy rail, card surfaces, tag pills and the action row; post media, right-rail
context and interactions are next.

## 3. Architecture

```
Deskboard.Core        no UI dependency, testable
  Api/TumblrClient    token discovery, cookies, CSRF, endpoints, pagination
  Api/Parsers         camelCase JSON → models (NPF blocks, trail, media)
  Models/Post         Post, Block, Media, TrailItem, Blog, Paged<T>
Deskboard.App         Avalonia UI (MVVM, CommunityToolkit)
  ViewModels/         MainViewModel + PostItemViewModel (feed, lazy images)
  Views/MainWindow    rail, feed, right rail
  Styles/Tumblr.axaml palette + control styles
```

Design rules carried over from the PoC:

- Reads use the public web token + session cookies; writes additionally require
  the per-session **CSRF token** from the dashboard bootstrap.
- Pagination always follows the API's own `links.next.href` cursor.
- Feed state keeps a look-ahead buffer; UI never blocks on the network thread.
- Media loads lazily per visible row.

## 4. Porting map (PoC → rewrite)

| PoC (Python) | Rewrite (C#) | Status |
|---|---|---|
| `deskboard/models.py` | `Core/Models/Post.cs` + `Api/Parsers.cs` | ✅ |
| `deskboard/api/client.py` (reads) | `Core/Api/TumblrClient.cs` | ✅ home/following/likes/explore/blog/tags |
| `deskboard/api/auth.py` | `Core/Api/CookieStore.cs` | ✅ JSON store + cookies.txt |
| `deskboard/ui/feed.py`, `postcard.py` | `App/ViewModels`, `MainWindow.axaml` | 🚧 scaffold |
| write endpoints (like/reblog/follow/reply/ask/DM) | `TumblrClient` | ⏳ |
| composer, messaging, notifications | — | ⏳ |
