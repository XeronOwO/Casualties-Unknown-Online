# Acceptance record — Local-only item and body one-shot sounds outside the ingest family

- Ticket: `unhooked-item-and-body-sound-families` — verdict: **stays in `review/`** (rows 1-2 unproven —
  the limb-treatment setups do not exist in this run; rows 3-5 and 7-9 judged; row 6 is partial)
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
| 1 | A limb treatment's clip reaches the peers | unproven — setup gap | **stays open** | needs the local wound view's limb selection and `PlayerCamera.ApplyWoundItem`; no probe drives that path in this run. Named in the ticket's `- Acceptance (20261001-l):` bullet |
| 2 | The remote-medical view stays silent (the native call is blocked) | unproven — setup gap | **stays open** | the remote view needs a second medical flow this run does not drive |
| 3 | A medical item through its WORLD use action | machine + residual | **pass** | the host's `rag` use replayed `Medical splint` once on the guest (`l-E3-use-rag.json`, `l-E3E5-guest-replays.txt`); the guest's `rosepod` use replayed `Medical goo` once on the host (`l-E3-guest-use-rosepod.json`, `l-E3-guest-items-host-replays.txt`) |
| 4 | A world-liquid drink | machine + residual | **pass** | the water branch replayed `Drink drink` once on the guest (`l-E4-host-drink-5.json`, `l-E4-guest-replays.txt`); the other four branches play **no** native clip — the decompiled `onDrink` delegates of `groundwater`, `lumalgae`, `oil` and `sap` carry no `Sound.Play` (checked against `reversing/…/Liquids.cs`), so there is nothing to carry and none is invented (zero replays for liquids 1-4, `l-E4-host-drink-*.json`) |
| 5 | Utility and gesture one-shots | machine + residual | **pass** | `flashlight` use replayed `Utility flashlighttoggle` once on the host (`l-E3-guest-use-flashlight.json`, `l-E3-guest-items-host-replays.txt`); `Body.SwitchHands` replayed `Gesture switch` once on the guest (`l-E3E5-guest-replays.txt`) |
| 6 | Body sickness / effort | machine + residual | **partial — unproven for nap and the water shake** | `Vomiter.Vomit` replayed `BodySound vomit1` once (`l-E3E5-guest-replays.txt`); `Body.TakeANap` did not start (its own `canTakeNap` gate) and the water-shake path was not driven, so this row stays open with those two setups named |
| 7 | A third peer hears it once, no double audio | machine + residual | **pass** | the alternate client replayed the same events exactly once: `l-A1-alt-charsound.txt`, `l-A2-alt-charsound.txt`, `l-B3-alt-replay.txt`, `l-B4-alt-replay.txt`, `l-B5c-alt-replay.txt` (and 171 replays in total, `l-evidence-summary.txt`) |
| 8 | The 2D prompts stay the acting player's own | machine | **pass** | the run's own suite pins the 2D census and the no-scope rows (`l-test.txt`) |
| 9 | A remote-driven replay reports nothing | machine | **pass** | the run's own suite pins the local-action scope (`l-test.txt`) |

## Residuals for the user

- The audible half of rows 3-7: each receiving client ran the game's own replay of the exact clip once;
  physical audibility is yours to confirm.

## Limits

- Rows 1-2 and part of row 6 are **missing setups**, not results; the ticket stays in `review/`.
- Items are created through the game's own `Utils.Create` + pickup path, and the world liquid is written
  into the body's own cell through `FluidManager.SetLiquid` before the game's own `DrinkLiquid` runs; both
  substitutions are named here.
- One session proves the events observed; it does not disprove a rare loss between reports.
