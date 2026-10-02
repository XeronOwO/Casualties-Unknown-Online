# Acceptance record — World/layer generation identity is missing from the wire

- Ticket: `world-layer-generation-identity` — verdict: **moved to `done/`** (rows 1, 2, 4, 5, 6, 7 and 8 pass in
  this batch; row 3 stands passed from batch `20261002-k`)
- Batch: `20261003-b` — tickets `world-layer-generation-identity`, `generation-identity-remaining-families`
- Commit under acceptance: `cc629be48ad9b4a0a21af7d311a37046a859977c` — the commit that staged this
  batch's probes and the build the artifact was made from. The probe fixes `f2917b64` and `853df375` are
  tool-only and do not enter the artifact. Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+cc629be48ad9b4a0a21af7d311a37046a859977c`, re-verified after the run (exit 0).
- Run: 2026-10-03, 00:50:54–01:20:35 +08:00 · Host: physical machine · Guest: sandbox `Steam1` ·
  Third client: sandbox `Steam2`
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (`dotnet`, `game`,
  `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`, `logs`, `artifacts`)
- Artifacts: `s1-*`, `s1b-*`, `s3-w2-*`, `s3-w4-*`, `s3-w6-*`, `s3-w8-*`, `s5-*`, `b0-*` and the log
  excerpts cited below, in the directory named by `acceptance-artifacts-dir` under `20261003-b/`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The air-write report is lost but the drops report arrives in the SAME generation — accepted; the drops are registered, not destroyed | machine | **pass** | The guest mined (512,1020) while the host's inbound dispatch was parked (`s1b-blackout-on-host.json`: `subscribersAfter=0`); the guest's pending tables then read `block=1, drops=1` (`s1b-pending-after-mine-guest.json`), and the product's own block-table reset (`block=1, damage=1, drops=0` → `block=0, drops=1`, `s1b-reset-guest.json`) left the drop carrier as the surviving report. The fallback re-sent it and the host took the lost-air-write verdict: `[BlockBreak] 76561199526807662's break at (512,1020) accepted as this generation's lost air write — 1 block drop(s) + 0 building drop(s) registered + relayed`, with the real drop materialized on the host (`[ItemSpawn] materializing plasticchunk (id 10156476526)`); the guest's pending tables returned to zero (`s1b-pending-now-guest.json`), the cell read air on host, guest and alt, and the guest logged no `ItemReject` (`s1b-host-lost-air-write.log`) |
| 2 | A previous layer's break report arrives after the layer change — refused as stale; the new layer's cell untouched | machine | **pass** | A break report stamped `run 1 layer 2` was handed to the host's own receive seam while the host was at `run 1 layer 3`: the gate refused it (`BlockDamaged … belongs to run 1 layer 2 while this side is at run 1 layer 3 — the report is about another world generation and is refused as stale`), the adapter refused its drops (`… belongs to another world generation — not applied; 1 block drop(s) + 0 building drop(s) rejected`), and the host's cells stayed `17`/`17` across the window (`s3-w2-break-stale-host.json`, `s3-w2-host-cell-*.json`). The air-write half of the same family was refused the same way (`BlockPlaced … refused as stale`, `s3-w2-placed-stale-host.json`, `s3-host-block-refusals.log`) |
| 4 | A stale `BlockDamageReport` row set for a regenerated cell — not applied to the new layer's block | machine | **pass** | An absolute damage report stamped `run 1 layer 2` was handed to the host's receive seam at `run 1 layer 3`: `[WorldReportGeneration] BlockDamageReport … refused as stale`, no answer was sent, and the host's live damage table read 2 rows before and 2 rows after (`s3-w4-damage-report-stale-host.json`, `s3-w4-census-before-host.json`, `s3-w4-census-after-host.json`) |
| 5 | Session end / new run — the identity resets with the run baseline; no cross-run attribution | machine | **pass** | The run was ended for real (the host left the lobby and created a new one, `s5-host-leave-lobby.json`, `s5-create-lobby-host.json`; both members left the old lobby and joined the new one, `s5-join3-*.json`) and a new run started: host and guest read `run 2 layer 0` (`s5-final-generation-host.json`, `s5-final-generation-guest.json`) and all three clients' object censuses are identical afterwards (`s5-final-object-*.json`). A report stamped `run 1 layer 3` was then handed to the host's receive seam: `[WorldReportGeneration] BlockDamaged … belongs to run 1 layer 3 while this side is at run 2 layer 0 — refused as stale`, and the cell stayed `9` (`s5-cell-before-host.json`, `s5-cell-after-host.json`, `s5-cross-run-stale-host.json`) |
| 6 | Third-party view after a layer change — every peer refuses the other generation's relay and does not re-attribute it | machine | **pass** | A relay-shaped break stamped `run 1 layer 2` was handed to the guest's receive seam at `run 1 layer 3`: `[WorldReportGeneration] BlockDamaged from 76561198281246659 … refused as stale` and `[BlockBreak] the host's relay at (480,984) belongs to another world generation — not applied and its drops not materialized.`; the guest's cell did not change and its own outstanding report was not answered by that relay (`s3-w6-relay-stale-guest.json`, `s3-w6-guest-cell-before.json`, `s3-w6-guest-cell-after.json`, `s3-guest-refusals.log`) |
| 7 | The stamp is on the wire, not added by the caller | machine | **pass** | Frame decode on the host: armed `wire-generation-probe kind=block`, the guest mined (512,1000) and the host decoded **two real frames from the guest** — `BlockPlaced` (13 B) and `BlockDamaged` (16 B) — both with `stamp=true run=1 layer=0`, the guest's own baseline read at send time. The probe reads the bytes the host received; no caller passes a stamp (`s1-wire-arm-host.json`, `s1-mine-guest.json`, `s1-wire-read-host.json`, `s1-wire-off-host.json`) |
| 8 | No stamp / no baseline on this side — never treated as current (the pre-stamp behaviour) | machine | **pass** | On the host, an unstamped break WITH a drop payload on a standing cell was refused without a generation proof (`[BlockBreak] … break report refused (the cell's break belongs to another writer) — 1 block drop(s) + 0 building drop(s) rejected`) and the cell stayed `17` (`s3-w8-break-none-host.json`); the probe's unstamped air write was accepted on the pre-stamp path and the cell went `17 → 0` (`s3-w8-placed-none-host.json`, `s3-w8-placed-none-after-host.json`) |

## Residuals for the user

None — every row is a `machine` row and every verdict is read from a probe result, a count or a log line
this run produced against the deployed artifact.

## Limits

- **A genuinely stale sender cannot be held for a scenario on this machine.** An inbound blackout longer
  than the member's 15 s host-silence watchdog ends that member's session locally (observed: `[GuestHostSilenceWatchdog] No frame from the host … for 15000 ms — ending the session locally`), and lifting the
  blackout converges the member to the host's baseline within ~1 s (the kernel stream delivers the
  checkpoint immediately, read from the member's own `Projected kernel run baseline`). Rows 2, 4, 5, 6 and 8
  therefore stage their frame through the product's own receive seam (`generation-forge`) and every
  decision downstream — the gate, the refusal log, the answer, the absence of an application — is the
  deployed product's own code. Row 1's lost-air-write shape is staged with the host's inbound blackout
  plus the product's own block-table reset, because the production wire cannot drop exactly one of the two
  same-tick frames.
- **Row 8's second half — "no committed run baseline here" — stays unit-pinned**
  (`WorldReportGenerationTests.WithoutARunBaselineHere_AVerifiedLookingStamp_IsStillUnknown`): every
  deployed peer in this run held a committed baseline.
- **Row 2's consequence on the breaker** (the refused drops rolled back on the reporter by `ItemReject`)
  was not restaged: the reporter would have to receive the host's answer while still on the previous
  baseline, which the watchdog above makes impossible. The refusal and the `ItemReject` fan-out are read on
  the host (`… rejected`), and batch `20261003-a` accepted the same rejection path end-to-end.
- **Row 3 stands from batch `20261002-k`**: four same-generation writes by three senders converged on all
  three clients, and the conflicting-claim branch stays unit-pinned.
- **A layer-modifier baseline divergence appeared during the session**: after the chain of live descents
  both members logged `[LayerMod] baseline divergence — local segment start … vs host's … (world effects
  may diverge)`, and their world fingerprints differed from the host's. An in-place world re-entry healed
  the guest (host and guest fingerprints identical afterwards — `fingerprint-host.json`,
  `fingerprint-guest.json`), the alt stayed diverged until the new run (`fingerprint-alt.json`), and after
  the new run all three object censuses agree (`s5-final-object-*.json`). This is the known condition batch
  `20261002-k` recorded — a different mechanism, carried by other tickets — and it does not bear on the
  generation-stamp verdicts, all of which are read from the gate's own comparison and the actors' own
  tables.
- Physical deployment and the unified dual-client acceptance remain the release-cycle action; this run is
  the agent-run acceptance.
