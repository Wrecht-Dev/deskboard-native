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
| Blog pages | ✅ | posts, follower count, Follow/Following, Ask |
| Tag pages (Latest / Top) | ✅ | `/v2/hubs/{tag}/timeline[?sort=top]` with toggle |
| Post permalink + notes | ✅ | notes with totals (likes/reblogs/replies), pagination, reply composer |
| Search (posts / blogs / communities) | 🟡 | API verified live (5 posts, 5 blogs, 5 communities); UI wired (search box + results); keyboard input not injectable under headless Xvfb, so UI unverified |
| Notifications / Activity | 🟡 | API verified live (48, paginated); list UI with follow-back; no screenshot for privacy |
| Notifications | ⏳ | |
| Messaging — conversations list | ✅ | 20 conversations live; unread badges, dates |
| Messaging — thread | ✅ | authorship aligned bubbles, times, composer, optimistic send |
| Messaging — send | 🟡 | wired + optimistic; not fired live (would message a real person) |
| Messaging — requests | ⏳ | |
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
| Animated GIFs | 🟡 | first frame + GIF badge; animation playback pending |
| Video posters / inline playback | 🟡 | poster image + play badge; inline playback pending |
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
| Submit post | ⏳ | |
| DM send / accept / read / delete | ⏳ | |
| Composer — text + tags | ✅ | NPF content builder, blog selector, live char counter |
| Composer — image upload | 🟡 | multipart upload + image blocks; not fired live (creates account media) |
| Composer — publish / queue / draft / schedule | 🟡 | all four wired; not fired live (would post publicly) |
| Settings writes / notes moderation | ⏳ | |

## 4. Tests

`dotnet test` — 11 tests: NPF formatting splits, heading/quote/list mapping,
reblog-trail fallback, link ranges, reblog mode transitions and pre-network
validation.

## 5. Verification

| Check | Result |
|---|---|
| Build (`dotnet build`) | ✅ 0 errors |
| Tests (`dotnet test`) | ✅ 28/28 |
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
