# Acceptance record — Host metal-scrap block placement sound not heard on guest

- Ticket: `host-metal-scrap-block-place-sound-not-synced-to-guest` — verdict: **moved to `done/`**
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
| 1 | The guest hears the host's scrapmetal placement | machine + residual | **pass** | `l-B1-guest-charsound.txt`: one `replayed ItemPlacement scrapmetal for owner <host> at (4.8,499.6)`; probes `l-B1-host-provide-scrapmetal.json`, `l-B1-host-use-scrapmetal.json` (aimed); the cell read `l-B1-placed-cell.json` shows block `3` at (516,1011) — the placement really happened |
| 2 | The host hears the guest's placement | machine + residual | **pass** | `l-B2-host-charsound.txt`: one `replayed ItemPlacement scrapmetal for owner <guest> at (3.8,499.6)`; probes `l-B2-guest-provide-scrapmetal.json`, `l-B2-guest-use-scrapmetal.json` |
| 3 | A third party hears it | machine + residual | **pass** | the alternate client replayed each placement exactly once: `l-B1-alt-charsound.txt`, `l-B2-alt-charsound.txt` |
| 4 | climbingrope | machine + residual | **pass** | `l-B3-guest-replay.txt` / `l-B3-alt-replay.txt`: one `replayed ItemPlacement ropeplace` each; probes `l-B3-host-provide-climbingrope.json`, `l-B3-host-use-climbingrope.json` |
| 5 | scaffoldingpack | machine + residual | **pass** | `l-B4-guest-replay.txt` / `l-B4-alt-replay.txt`: one `replayed ItemPlacement scrapmetal` each; probes `l-B4-*` |
| 6 | A failed/gated placement stays silent | machine | **pass** | the moving-body gate: `l-B5c-host-place-gated.json` reads `canPlaceBlockAtUse=false` immediately before the use, and `l-B5c-guest-replay.txt` / `l-B5c-alt-replay.txt` hold **zero** `ItemPlacement` replays (a placement with an occupied target is not a reachable failure — the native linecast pushes the target point back out into the air cell, so the body's own `canPlaceBlock` gate is the shape this run drives) |
| 7 | A remote apply does not echo | machine | **pass** | `l-evidence-summary.txt`: the host's log carries 150 replays of other clients' sounds and **0** replays for its own owner id; the capture's local-action guard is pinned by the run's own suite (`l-test.txt`) |
| 8 | Solo / no active session stays local | machine | **pass** | the run's own suite pins the session-active guard (`CharacterSoundSync.Report`), 300 gates + 4,538 tests exit 0 (`l-test.txt`); no solo client was launched |

## Residuals for the user

- The audible half of rows 1-5: what is proven is the replay of the exact clip (`scrapmetal` /
  `ropeplace`) on each receiving client once per placement; the physical audibility is yours to confirm.

## Limits

- Items are created through `Utils.Create` + pickup and aimed through `body.targetLookPos` — the field the
  native mouse aim writes — because this run injects no OS input; both substitutions are named here.
- Row 6's first attempt aimed at an occupied cell and still placed (the delegate re-aims to the surface
  point), so the gated placement driven is the moving-body one; both attempts are in the batch directory.
- The run observed two clients' placements plus the third listener; a rare loss between reports is not
  disproved by one session.
