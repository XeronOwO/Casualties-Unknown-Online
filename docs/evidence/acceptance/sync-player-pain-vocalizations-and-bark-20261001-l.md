# Acceptance record — Sync player pain vocalizations and B-key bark to remote players

- Ticket: `sync-player-pain-vocalizations-and-bark` — verdict: **stays in `review/`** (rows 1-3 are
  `unproven`: the lockpick-failure setup does not exist in this run; rows 4-6 pass on the run's own suite,
  and the one-shot kinds were exercised live on the sibling rows)
- Batch: `20261001-l` — tickets `host-eating-sound-not-heard-on-guest`,
  `host-metal-scrap-block-place-sound-not-synced-to-guest`, `guest-hears-only-some-block-break-sounds`,
  `sync-player-pain-vocalizations-and-bark`, `unhooked-item-and-body-sound-families`,
  `suppressed-native-call-sounds-stay-unheard`
- Commit: `a91ca24a` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+a91ca24a04deaa0aa6994abec495651c810eaaff` (`deploy.ps1` then `verify-deploy.ps1`, exit 0)
- Run: 2026-10-01 09:46 → 09:59 (+08:00) · Host: physical machine (18590) · Guests: primary sandbox
  (18591) and alternate sandbox (18592), one world
- Dependencies: preflight `11 present`, exit 0
- Artifacts: the `batch-l` directory under `acceptance-artifacts-dir`; the run log is `l-run-log.txt`

## Row verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1-3 | A lockpick failure's `gore2` reaches the peers (host → guest, guest → host, third listener) | unproven — setup gap | **stays open** | the failure needs a lockable entity whose lockpick minigame can be held stuck (`timeWasStuck > 0.5f`); this run's world carried none the driver could reach, and none is invented. The ticket's `- Acceptance (20261001-l):` bullet names the gap |
| 4 | A successful `unlock` is not replayed as pain | machine | **pass** | the run's own suite pins the origin classification (`CharacterSoundPolicy` rejects `unlock`), 300 gates + 4,538 tests exit 0 (`l-test.txt`) |
| 5 | A replay is not captured and reported again | machine | **pass** | `l-evidence-summary.txt`: the source client's log carries **0** replays for its own id across the run; the capture's local-action guard is pinned by the suite (`l-test.txt`) |
| 6 | Solo / inactive session reports nothing | machine | **pass** | the session-active guard is pinned by the run's suite (`l-test.txt`) |
| + | The ticket's own one-shot kinds (bark, pain, yawn) reach the peers | machine + residual | **pass** (live evidence, on the sibling family) | `l-D1-host-bark.json` → the guest replayed `Bark pain1` for the host once; `l-D2-guest-bark.json` → the host replayed `Bark death1` for the guest; `l-D3b-host-pain-force.json` → the guest replayed `Pain exp15` and `Pain exp14`; the natural yawn replayed as `Yawn yawn1` (excerpts in `l-charsound-all-guest.txt`, `l-D-*-replays.txt`) |

## Residuals for the user

- The audible half of the live kinds above (bark, pain, yawn): proven here is that each receiving client
  ran the game's own replay of the exact clip; the physical audibility is yours to confirm.
- The lockpick rows are not judged at all — they are a missing setup, not a result.

## Limits

- **The lockpick setup is the open gap**: no lockable entity was reachable in this run's world, so rows
  1-3 stay `unproven` and the ticket stays in `review/`; a later batch that can place a lockable reaches
  them.
- The forced pain wrote the LIMBS' pain (the input), not `Body.averagePain` (a per-frame derived field): a
  first probe writing the derived field produced no play at all — recorded in
  `docs/acceptance/lessons.md`.
- A single session proves the events observed; it does not disprove a rare loss between reports.
