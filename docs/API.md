# Tumblr API surface map

Tumblr has no public API for third-party desktop clients any more. The website
uses an internal REST API ("kraken") at `https://www.tumblr.com/api`. This
document maps that surface as observed from the web client and the working Linux
PoC. It is the specification the rewrite follows.

## 1. Authentication

| Layer | Mechanism |
|---|---|
| **Public web token** | The site serves a bearer token inside its HTML bootstrap (`API_TOKEN`). It authorises what a logged-out visitor can see. Deskboard fetches it at first launch and caches it locally. |
| **Session** | The user's own browser cookies (`sid`, `tmgioct`, `pfu`, `logged_in`, …) sent as a `Cookie` header. Imported from Firefox/Chromium or a `cookies.txt`. |
| **Writes** | Require the per-session **CSRF token** from the dashboard bootstrap: `"csrfToken":"<value>"` (sometimes JSON-escaped). Sent as `X-CSRF`. A stale/missing token yields `401 code 1022`. |
| **Credential login** | `POST /v2/oauth2/token` with `Authorization: Bearer <public web token>` and form `grant_type=password&username=&password=[&tfa_token=][&captcha_token=][&blackbox_session_id=]`. Success returns the session as `Set-Cookie` headers; failures distinguish bad credentials, 2FA (`tfa_token` needed), captcha and rate limiting. |

Every request sends: `Authorization: Bearer <public token>`, `Cookie: …`,
`Accept: application/json;format=camelcase`, `X-Version: 1`,
`Referer: https://www.tumblr.com/`. Without the camelCase `Accept`, the API
answers **snake_case** (`liked_posts`) — the single most common source of “empty
feed” bugs.

## 2. Conventions

- **Post format**: NPF. A post has `content: Block[]` and a `trail: TrailItem[]`
  (previous reblog contributions, oldest first). A reblog often has `content: []`
  and all text/media in the trail.
- **Block types**: `text` (subtypes `heading1`, `heading2`, `quote`, `indented`,
  `chat`, `ordered-list-item`, `unordered-list-item`, `ask`), `image`, `video`,
  `audio`, `link`, `poll`, `answer`. Text blocks carry `formatting: [{start,end,type,url}]`.
- **Pagination**: follow `response.links.next.href` for blogs/notifications/
  conversations; dashboard uses `response.timeline.links.next.href`
  (`wrapped-timeline=true` required); tag hubs paginate via `before`; Likes use
  `offset`.
- **Page size**: dashboard/Home return **~4 posts per request** regardless of
  `limit`; plan for pipelining rather than one large page.
- **Sorting**: tag pages use `/v2/hubs/{tag}/timeline` with `sort=top` for Top
  (the server currently returns the same set for both on some accounts).

## 3. Read surface

| Area | Request |
|---|---|
| Home / For you | `GET /v2/tabs/for_you?notes_info=false&reblog_info=true&limit=` |
| Following | `GET /v2/timeline/dashboard?reblog_info=true&wrapped-timeline=true&limit=` |
| Blog posts | `GET /v2/blog/{blog}/posts?limit=&reblog_info=true` |
| Tag (Latest/Top) | `GET /v2/hubs/{tag}/timeline?limit=14[&sort=top]` · fallback `GET /v2/tagged?tag=` |
| Explore | `GET /v2/explore/recommended?reblog_info=true` |
| Trending tags | `GET /v2/explore/trending/tags` → `timeline.elements[].resources[].tag` |
| Post search | `GET /v2/timeline/search?query=&limit=` |
| Blog/tag/community search | `GET /v2/search/blogs?query=` → `{blogs, tags, communities}` |
| Likes | `GET /v2/user/likes?limit=` → `{likedPosts, likedCount}` |
| Following blogs | `GET /v2/user/following?limit=` |
| Followed tags ("Your tags") | `GET /v2/user/tags?limit=&offset=` → `{count, tags[]}` |
| Blog info | `GET /v2/blog/{blog}/info` → avatar/title/followers/ask/canSubmit |
| Notifications | `GET /v2/blog/{blog}/notifications?limit=&before=` |
| Conversations | `GET /v2/conversations?limit=&before=` · requests: `/v2/conversations/requests` |
| Messages | `GET /v2/conversations/messages?conversation_id=` → `{messages:{data:[…]}}` |
| Inbox (asks/submissions) | `GET /v2/user/inbox?limit=&offset=&sort=` |
| Drafts / Queue | `GET /v2/blog/{blog}/posts/draft` · `/posts/queue` (`state.paused`) |
| Post notes | `GET /v2/blog/{blog}/notes?id={postId}&mode=all` (paginates by `cursor`) |
| Blog settings | `GET /v2/blog/{blog}/settings` |
| User settings | `GET /v2/user/settings` |
| GIF library | `GET /v2/gif/popular?offset=` |
| Mention autocomplete | `GET /v2/mention/{prefix}` |

## 4. Write surface (all require `X-CSRF`)

| Action | Request / body |
|---|---|
| Like / unlike | `POST /v2/user/like` · `/v2/user/unlike` → `{id, reblog_key}` |
| Reblog (3 modes) | `POST /v2/blog/{blog}/post/reblog` → `{id, reblog_key, comment, tags, publish_on?}` — `publish_on` schedules |
| Follow / unfollow | `POST /v2/user/follow` · `/v2/user/unfollow` → `{url}` |
| Reply | `POST /v2/user/post/reply` → `{post_id, reply_text, reblog_key, tumblelog, reply_as}` |
| Send ask | `POST /v2/blog/{blog}/ask` → `{question, anonymous}` |
| Create post / submission | `POST /v2/blog/{blog}/posts` → `{content, layout, tags, state}` (`published`/`draft`/`queue`/`submission`, `publish_on`) |
| Edit post | `PUT /v2/blog/{blog}/posts/{id}` · quick state: `POST /…/post/edit` `{id, state}` |
| Delete post | `POST /v2/blog/{blog}/post/delete` → `{id}` |
| Pin / unpin | `POST`/`DELETE /v2/blog/{blog}/posts/{id}/pin` |
| Block / unblock blog | `POST`/`DELETE /v2/blog/{blog}/blocks` → `{blocked_tumblelog, force, post_id?}` |
| Mute post | `POST`/`DELETE /v2/blog/{blog}/posts/{id}/mute` |
| Report | `POST /v2/report` (multipart or form; field names partially inferred) |
| Vote poll | `POST /v2/polls/{pollId}` → `{selected}` |
| Follow / unfollow tag | `POST /v2/user/tags/add` · `/remove` → `{tag}` |
| Subscribe blog/post | `POST`/`DELETE /v2/blog/{blog}/subscription` · `/posts/{id}/subscription` |
| Queue pause/shuffle/reorder | `POST /…/posts/queue/state` `{paused}` · `/shuffle` · `/reorder` `{post_id, insert_after}` |
| Activity read marker | `POST /v2/blog/{blog}/activity_last_read` |
| Inbox delete | `DELETE /v2/user/inbox` · `DELETE /v2/blog/{blog}/posts/submission` |
| DM send / accept / read / delete / flag | `POST /v2/conversations/messages` · `/accept` · `/mark_as_read` · `DELETE /v2/conversations/messages` · `POST /v2/conversations/flag` |
| Delete own reply | `POST /v2/blog/{blog}/notes/delete` → `{note_tumblelog, note_created_time, post_id}` |
| Flag note | `POST /v2/blog/{blog}/notes/flag` |
| Settings writes | `POST /v2/blog/{blog}/settings` · `POST /v2/user/settings` |
| Upload image / audio | `POST /v2/media/image` · `/v2/media/audio` (multipart `file`) |

## 5. Deliberate exclusions

Premium/Blaze/ignite purchases, domain management, communities, 2FA and
login/password recovery, session management. These are payments or browser
security flows, not client features.

## 6. Verification notes

The Linux PoC verified the read surface live, plus one authorised like/unlike.
Write payloads marked *inferred* in the PoC's `docs/COVERAGE.md` were never fired
at a real account. Treat the whole surface as **private and unstable**: Tumblr can
change it at any time, and this map is a snapshot, not a contract.
