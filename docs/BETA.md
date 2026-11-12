# Beta status — evidence log

This document records what was **actually executed**, not just implemented.
Last sweep: v0.2.3, 53/53 tests.

## Verified by execution

| Check | How |
|---|---|
| Release build | `dotnet build -c Release` — 0 errors |
| Test suite | `dotnet test` — 53/53 |
| Dark + light startup | headless Xvfb runs, 0 unhandled errors |
| Install from GitHub release | `install.sh` with `DESKBOARD_VERSION=v0.2.3` → downloaded, installed, launched |
| Live API reads | home, following, explore, likes, blogs, tag latest/top, search (posts/blogs/communities), trending (10 topics), notifications (48→98 paginated), conversations (20), thread messages (8, authorship correct), library (inbox/drafts/queue), notes (231 totals), GIF library, followed tags (89) |
| Media | photoset grids, animated GIF playback (frame-diff verified), media lightbox (screenshot), video/audio poster + system-player fallback |
| Onboarding | welcome window screenshot; login window screenshot; invalid-credential classification (`invalid_grant`) |
| Custom features | pinned tabs (40–41 posts aggregated, per-tab Latest/Top), keyword filters (hidden banner), bookmarks (seeded store rendered), hide-mature toggle |
| CI | release workflow green for v0.1.3 / v0.2.0 / v0.2.1 / v0.2.2 / v0.2.3 (4 assets each) |

## Implemented, never fired against the account

Wired against payloads verified from the working GTK PoC, unit/API-tested where
possible, but **not executed** because they would change real data:

reblog (3 modes), follow/unfollow, reply, ask, submit, edit post, DM send,
message-request accept, mute/block/report/delete, pin, notes moderation,
queue pause/shuffle/reorder, settings writes, image upload.

Only `like`/`unlike` has been exercised live (with explicit permission).

## Known gaps (deliberate, documented)

- **Communities, Premium/Blaze/ignite, domains, 2FA management, login recovery** —
  out of scope for a client Beta; excluded in `docs/API.md`.
- **Inline video/audio** — LibVLC prototype crashed natively when libvlc or the
  video output was missing; reverted. Badges open the system player.
- **Credential login success path** — endpoint, 2FA/captcha/bad-password handling
  verified; a successful login needs the user's account.
- **Windows/macOS installers** — cross-built in CI and packaged; only the Linux
  installer and binaries were executed in this environment.
- **Mature filter** — honours Tumblr's `isAdult` flag, which the API often leaves
  false even for explicit blogs; keyword filters are the dependable control.

## Formerly missing, now implemented

- **Blog / account settings reads** — 39 blog keys and 34 account keys returned
  live and displayed in Settings.
- **@mention autocomplete** — live (`/v2/mention/art` → 5 suggestions).
- **Audio upload** — multipart endpoint (field `audio`) implemented; not fired.
- **Draft actions** — publish-now (state change) and delete, with confirmation.

Every endpoint listed in `docs/API.md` now has a client method; the remaining
gaps are the deliberate exclusions, the user-gated write/login verification and
inline video playback documented above.
