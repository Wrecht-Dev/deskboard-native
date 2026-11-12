# Changelog

## 0.2.1 — Beta

- First-run welcome screen (sign in / browser session / browse).
- Credit sign-in hardening: explicit signed-out override, startup order fixed.
- Trending tags in the right rail; queue pause/resume/shuffle.
- GIF library picker in the composer.
- Activity: correct names/avatars and cursor pagination.
- Keyword filters and local bookmarks (from 0.2.0 work).

## 0.2.0 — Beta

First public Beta of the cross-platform rewrite.

### Features
- Native Tumblr client for Linux, Windows and macOS (Avalonia + .NET 10).
- Feeds: Home, Following, Explore, Likes, blog pages, tag pages with Latest/Top.
- Post permalink with the full notes breakdown; notifications with pagination.
- Direct messages: Inbox, Requests, threads, send.
- Library: Inbox, Drafts, Queue.
- Composer: NPF text + images, publish / queue / draft / schedule, edit.
- Social actions: like, reblog (3 modes), follow, reply, ask, submit,
  mute/block/report/delete, notes moderation — wired, not auto-fired.
- Rich text, photoset grids, animated GIFs, light and dark themes.
- Search across posts, blogs and communities.

### Custom features (not in the web client)
- Pinned tabs (tag / search / all followed tags) with per-tab Latest/Top.
- Keyword filters with a "hidden posts" banner.
- Local bookmarks that work offline.

### Sign-in
- Email/password login against Tumblr's endpoint with 2FA handling,
  plus browser-session import and automatic cookie refresh.
  Passwords are never stored or logged.

### Beta quality
- In-app update check, crash log, first-run sign-in prompt.
- CI releases for linux-x64, win-x64, osx-arm64, osx-x64.
- One-line installers for Linux/macOS and Windows.

## 0.1.3 — scaffold
- Linux-only GTK proof of concept predecessor (see Wrecht-Dev/deskboard).
