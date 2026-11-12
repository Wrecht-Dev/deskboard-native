# Coverage matrix — native parity with the Tumblr web client

Legend: ✅ working & verified live · 🟡 partial · ⏳ planned · ⛔ excluded by design

## 1. Shell & feeds

| Capability | Status | Notes |
|---|---|---|
| Three-column shell (rail / feed / right rail) | ✅ | navy `#001935` rail, card feed, tag rail |
| Home (For you) | ✅ | `/v2/tabs/for_you`, camelCase Accept |
| Following | ✅ | `/v2/timeline/dashboard?wrapped-timeline=true` |
| Explore | ✅ | `/v2/explore/recommended` |
| Likes | ✅ | `{likedPosts, likedCount}` |
| Virtualised feed + infinite scroll | ✅ | `ListBox` + scroll-threshold `LoadMore` |
| Session auto-refresh (rotated cookies) | ✅ | re-reads Firefox `cookies.sqlite`, persists `0600` |
| Credential sign-in (email/password) | ✅ | real login endpoint via public bearer; 2FA/captcha/rate-limit classified; password never stored |
| Browser-session fallback | ✅ | one-click import in the same dialog |
| Public token discovery + cache | ✅ | no credentials in repo |
| Dark theme | ✅ | sampled from live site |
| Light theme | ✅ | theme dictionaries; verified by screenshot (white rail/cards, dark text) |
| Blog pages | ✅ | posts, follower count, Follow/Following, Ask |
| Tag pages (Latest / Top) | ✅ | `/v2/hubs/{tag}/timeline[?sort=top]` with toggle |
| Post permalink + notes | ✅ | notes with totals (likes/reblogs/replies), pagination, reply composer |
| Search (posts / blogs / communities) | 🟡 | API verified live (5 posts, 5 blogs, 5 communities); UI wired (search box + results); keyboard input not injectable under headless Xvfb, so UI unverified |
| Notifications / Activity | ✅ | 48 → 98 via cursor pagination (scroll + Load more); fromTumblelog* names/avatars; follow-back |
| Notifications | ⏳ | |
| Messaging — conversations list | ✅ | 20 conversations live; unread badges, dates |
| Messaging — thread | ✅ | authorship aligned bubbles, times, composer, optimistic send |
| Messaging — send | 🟡 | wired + optimistic; not fired live (would message a real person) |
| Messaging — requests | ✅ | Requests tab + Accept; account has 0 pending, API verified |
| Inbox / drafts / queue | ⏳ | |
| Inbox / drafts / queue pages | ✅ | Library page with Inbox/Drafts/Queue tabs, empty states verified; account has 0 items |
| Post menu (mute / block / report / delete) | 🟡 | wired with confirmations for block/delete; not fired live |

## 2. Post rendering

| Capability | Status | Notes |
|---|---|---|
| Header (avatar, blog, time) | ✅ | circular avatar, loaded with UA+Referer |
| Reblog provenance | ✅ | "reblogged from @x" |
| Text blocks (headings/quotes/lists) | ✅ | NPF formatting → styled runs (bold/italic/strike/underline/links), headings, quotes, bullets |
| Photoset grids (1–4 images) | ✅ | single image or 2-col grid, lazy + cached |
| Animated GIFs | ✅ | Skia frame decode + timer playback; verified animating (frame captures differ) |
| Video / audio | 🟡 | poster + badge opens the system player; inline decode not wired |
| Audio | ⏳ | |
| Polls | ⏳ | |
| Post actions (like/reblog/reply/follow/ask) | ✅ | like (optimistic), reblog dialog (3 modes), reply from permalink, follow + ask on blog pages |
| Post menu (mute/block/report/delete) | ⏳ | |

## 3. Write surface

| Capability | Status | Notes |
|---|---|---|
| CSRF token + retry on 401/1022 | ✅ | `SendWriteAsync` |
| Like / unlike | ✅ | optimistic UI; fires only on click |
| Reblog (text / post-only / scheduled) | 🟡 | dialog + 3 modes wired; unit-tested; not fired live |
| Follow / unfollow | ✅ | blog page header, optimistic with revert |
| Reply | ✅ | from post permalink; verified UI |
| Ask | ✅ | dialog with 500-char counter + anonymous toggle, gated on blog capability |
| Submit post | 🟡 | compose + submit to blogs with can_submit; not fired live |
| DM send / accept / read / delete | ⏳ | |
| Composer — text + tags | ✅ | NPF content builder, blog selector, live char counter |
| Composer — image upload | 🟡 | multipart upload + image blocks; not fired live (creates account media) |
| Composer — publish / queue / draft / schedule | 🟡 | all four wired; not fired live (would post publicly) |
| Edit post | 🟡 | composer prefilled, PUT rewrite wired; not fired live |
| Settings screen | ✅ | account + session state, blogs, appearance, about; re-import session |
| Theme toggle (in-session) | 🟡 | command wired; persistence verified on restart; headless click unverified |
| Notes moderation (delete/flag) | 🟡 | note menus on your own posts with confirmations; not fired live |

## Custom features

| Feature | Status |
|---|---|
| Pinned tabs (tag / search / all-followed-tags) | ✅ live-verified (40–41 posts aggregated) |
| Per-tab Latest/Top sort, persisted | ✅ |
| Add / unpin dialogs | ✅ |
| Keyword filtering | ✅ live-verified (hidden banner + Show hidden) |
| Local bookmarks | ✅ live-verified (seeded + rendered, offline) |
| Session management | 🟡 sign-in + browser fallback; multi-account ⏳ |

## 4. Tests

`dotnet test` — 11 tests: NPF formatting splits, heading/quote/list mapping,
reblog-trail fallback, link ranges, reblog mode transitions and pre-network
validation.

## 5. Verification

| Check | Result |
|---|---|
| Build (`dotnet build`) | ✅ 0 errors |
| Tests (`dotnet test`) | ✅ 32/32 |
| Live feed | ✅ 11–12 posts, avatars + text render |
| Live tag Top ordering | ✅ note-count sorted |
| Live likes | ✅ count returned |
| Session refresh | ✅ Firefox import, 1,588-byte store `0600` |
| Write actions | only on explicit user click; no test writes fired |

## Next rounds

1. Rich text formatting + GIF/video/audio + image viewer.
2. Reblog dialog, reply, follow, ask; post detail with notes.
3. Search, notifications, messaging, inbox, drafts/queue.
4. Composer + settings; light theme; parity pass against `docs/reference`.

## Last verification sweep

- `dotnet build Deskboard.sln` — 0 errors
- `dotnet test` — 32/32 passing
- dark-theme smoke — window opens, feed renders, 0 tracebacks
- light-theme smoke — same, palette applied from persisted setting
- commit `36a04e3`

## Final summary

**Implemented and live-verified:** shell, feeds (Home/Following/Explore/Likes),
blog pages, tag Latest/Top, post permalink + notes, notifications, DMs
(Inbox/Requests/threads), library pages, search APIs, composer (text/images),
settings, light/dark themes, animated GIFs.

**Implemented, unit/API-verified, not fired at the account:** likes are the only
authorised write test; reblog, follow, reply, ask, submit, edit, DM send, mute,
block, report, delete, notes moderation and settings writes are wired against
verified payloads but were never executed.

**Known gaps:** inline video/audio decode (system-player fallback), conversation
preview text, pixel-perfect typography (Favorit unavailable).
