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
| Public token discovery + cache | ✅ | no credentials in repo |
| Dark theme | ✅ | sampled from live site |
| Light theme | ⏳ | tokens known; toggle pending |
| Blog pages | ✅ | blog name opens `/v2/blog/{blog}/posts` |
| Tag pages (Latest / Top) | ✅ | `/v2/hubs/{tag}/timeline[?sort=top]` with toggle |
| Post permalink + notes | ⏳ | client has notes endpoint; page pending |
| Search (posts/blogs/tags/communities) | ⏳ | |
| Notifications | ⏳ | |
| Messaging / conversations / inbox | ⏳ | |
| Drafts / queue | ⏳ | |

## 2. Post rendering

| Capability | Status | Notes |
|---|---|---|
| Header (avatar, blog, time) | ✅ | circular avatar, loaded with UA+Referer |
| Reblog provenance | ✅ | "reblogged from @x" |
| Text blocks (headings/quotes/lists) | 🟡 | plain text runs now; rich formatting next |
| Photoset grids (1–4 images) | ✅ | single image or 2-col grid, lazy + cached |
| Animated GIFs | ⏳ | badge + playback |
| Video posters / inline playback | ⏳ | |
| Audio | ⏳ | |
| Polls | ⏳ | |
| Post actions (like/reblog/reply) | 🟡 | like wired (CSRF, optimistic, revert); reblog/reply next |
| Post menu (mute/block/report/delete) | ⏳ | |

## 3. Write surface

| Capability | Status | Notes |
|---|---|---|
| CSRF token + retry on 401/1022 | ✅ | `SendWriteAsync` |
| Like / unlike | ✅ | optimistic UI; fires only on click |
| Reblog (text / post-only / scheduled) | ⏳ | client method ready |
| Follow / unfollow | ⏳ | client method ready |
| Reply | ⏳ | client method ready |
| Ask | ⏳ | client method ready |
| Submit post | ⏳ | |
| DM send / accept / read / delete | ⏳ | |
| Composer (NPF text/photo/queue/draft/schedule) | ⏳ | |
| Settings writes / notes moderation | ⏳ | |

## 4. Verification

| Check | Result |
|---|---|
| Build (`dotnet build`) | ✅ 0 errors |
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
