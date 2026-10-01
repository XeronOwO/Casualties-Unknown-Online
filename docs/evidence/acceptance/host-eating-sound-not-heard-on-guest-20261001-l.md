# Acceptance record — The host's eating sound is not heard on the guest

- Ticket: `host-eating-sound-not-heard-on-guest` — verdict: **moved to `done/`** (every row judged; the
  audible half of a sound row is a residual for the user)
- Batch: `20261001-l` — tickets `host-eating-sound-not-heard-on-guest`,
  `host-metal-scrap-block-place-sound-not-synced-to-guest`, `guest-hears-only-some-block-break-sounds`,
  `sync-player-pain-vocalizations-and-bark`, `unhooked-item-and-body-sound-families`,
  `suppressed-native-call-sounds-stay-unheard`
- Commit: `a91ca24a` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+a91ca24a04deaa0aa6994abec495651c810eaaff` (`deploy.ps1` then `verify-deploy.ps1`, exit 0)
- Run: 2026-10-01 09:46 → 09:59 (+08:00) · Host: physical machine (evaluator 18590) · Guests: primary
  sandbox (18591) and alternate sandbox (18592), all three in one world
- Dependencies: preflight `11 present`, exit 0
- Artifacts: the `batch-l` directory under `acceptance-artifacts-dir`, cited by file name; the run log is
  `l-run-log.txt`

## Row verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The guest hears the host's eat sound | machine + residual | **pass** | `l-A1-guest-charsound.txt` and `l-A1-alt-charsound.txt`: the guest and the alternate client each logged exactly one `[CharacterSound] replayed Consume eatCrunch for owner <host> at (0.0,499.6)` at 09:48:00.75; probes `l-A1-host-provide-geofruit.json` (slot 0) and `l-A1-host-use-geofruit.json` (`usable=true`) |
| 2 | The host hears the guest's eat sound | machine + residual | **pass** | `l-A2-host-charsound.txt`: one `replayed Consume eatCrunch for owner <guest>` at 09:48:17.51; the alternate client the same (`l-A2-alt-charsound.txt`); probes `l-A2-guest-provide-geofruit.json`, `l-A2-guest-use-geofruit.json` |
| 3 | A third peer hears each meal once | machine + residual | **pass** | the alternate client's log carried exactly one replay for the host's meal (`l-A1-alt-charsound.txt`) and one for the guest's (`l-A2-alt-charsound.txt`) — no double audio |
| 4 | The meal-end burp joins the same family | machine + residual | **pass** | `l-A4-host-burp-arm.json` (the timer armed through the game's own `Body.Burp`); the guest and the alternate client each replayed `Consume burp for owner <host>` exactly once at 09:49:00.59 (`l-A1-guest-charsound.txt` and `l-A2-alt-charsound.txt` windows, excerpt in `l-run-log.txt`) |
| 5 | The sibling consumables ride the path | machine | **pass** | `l-A5-guest-charsound.txt` / `l-A5-alt-charsound.txt`: the `waterbottle` use replayed `Consume drink` once on each peer; probes `l-A5-host-provide-waterbottle.json`, `l-A5-host-use-waterbottle.json`; the classification's negative rows are the run's own suite (300 gates + 4,538 tests, exit 0 — `l-test.txt`, `l-build-postcommit.txt`) |

## Residuals for the user

- The audible half of rows 1-5: the run captures no audio, so what is proven here is that each receiving
  client ran the game's own replay of the exact clip once (the last CUO-controlled step). Whether the
  sound is audible on that machine is yours to confirm.

## Limits

- The items were created through the game's own `Utils.Create` + pickup path (`item-provide`), and the
  burp was armed through `Body.Burp` — a declared substitution for the meal path's own `hunger > 90` and
  10% roll (the host's hunger read 109.6 at that moment); the record names both.
- The source client never replays its own sound; `l-evidence-summary.txt` counts zero replays for the
  host's own id across the whole run, which is the no-echo half of the family.
- One session proves the meals observed, not the absence of a rare loss.
