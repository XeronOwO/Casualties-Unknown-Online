# Acceptance record — Sounds whose native call is suppressed never reach the peers

- Ticket: `suppressed-native-call-sounds-stay-unheard` — verdict: **stays in `review/`** (rows 1-4b are
  unproven — the remote-medical view and the minigame setups do not exist in this run; rows 4 and 5-8
  judged)
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
| 1, 2, 3, 4b | The remote limb-treatment clips and the amputation / shrapnel minigame gore | unproven — setup gap | **stay open** | every one needs the remote-medical view or an amputation / shrapnel minigame; this run drives neither. Named in the ticket's `- Acceptance (20261001-l):` bullet |
| 4 | Injections, the AED and the manual defibrillator play no 3D clip | machine | **pass** | the run's own suite pins the uncarried census (`l-test.txt`, 300 gates + 4,538 tests, exit 0) |
| 5 | A world item lands on every client | machine (+ visual half unproven) | **pass for the sound; the dust puff was not captured** | the authority logged **219** `[ItemImpact] reported kind=…` events and each guest replayed **219** (`l-evidence-summary.txt`: guest 207 `ItemImpact` + 12 `PlushSqueak`; the alternate client the same 207 + 12) — one replay per reported landing; the specific `dogfood` drop is `l-F5-host-drop-dogfood.json` with its guest replay at (-1.8,498.5) index=0 in `l-F-guest-impacts.txt`; the guest's own copies are explicitly suppressed (655 suppression lines on the alternate client, excerpted in the same file) |
| 6 | A plush squeaks on every client | machine + residual | **pass** | `l-F6-host-drop-plushie.json`; both guests replayed `kind=PlushSqueak … index=12` (12 each, `l-F-guest-impacts.txt`, `l-evidence-summary.txt`) — one per native collision of the authority's plushie |
| 7 | Solo / no active session reports nothing | machine | **pass** | the run's own suite pins the session-active guard (`l-test.txt`) |
| 8 | A remote-driven item use stays the owner's own | machine | **pass** | the run's own suite pins the local-action scope (`l-test.txt`) |

## Residuals for the user

- Row 5's **dust puff** is the one half this run could not capture: the impact frames were not taken inside
  the particle's short life, and the game's own world zoom makes a few pixels unreadable — a person
  should look when playing. The sound half is proven by the 1:1 report/replay counts above.
- Row 6's audible half: each receiving client ran the game's own squeak replay (`index=12`) once per
  collision; physical audibility is yours to confirm.

## Limits

- Rows 1-4b are **missing setups**, not results; the ticket stays in `review/`.
- The two item drops were spawned through the game's own `Utils.Create` at a height above the body and fell
  under the game's own physics; the count comparison covers the whole session, so the loot collisions of
  the block breaks are included on both sides.
- One session proves the events observed; it does not disprove a rare loss between reports.
