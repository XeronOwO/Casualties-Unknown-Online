# Acceptance record — The remaining generation-relative report families

- Ticket: `generation-identity-remaining-families` — verdict: **moved to `done/`** (rows 1, 2, 4, 5, 6, 7
  and 9 pass in this batch; rows 3 and 8 stand passed from batch `20261002-k`)
- Batch: `20261003-b` — tickets `generation-identity-remaining-families`, `world-layer-generation-identity`
- Commit under acceptance: `cc629be48ad9b4a0a21af7d311a37046a859977c` — the commit that staged this
  batch's probes and the build the artifact was made from. The probe fixes `f2917b64` and `853df375` are
  tool-only and do not enter the artifact. Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+cc629be48ad9b4a0a21af7d311a37046a859977c`, re-verified after the run (exit 0).
- Run: 2026-10-03, 00:50:54–01:20:35 +08:00 · Host: physical machine · Guest: sandbox `Steam1` ·
  Third client: sandbox `Steam2`
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (`dotnet`, `game`,
  `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`, `logs`, `artifacts`)
- Artifacts: `s3-af1-*`, `s3-af2-*`, `s3-af4-*`, `s3-af7-*`, `s3-af9-*`, `s3-w6-*`, `s3-wire-entity-*`,
  `s4-wire-trap-*`, `s5-af5-*`, `s5-final-object-*`, `b0-*` and the log excerpts cited below, in the
  directory named by `acceptance-artifacts-dir` under `20261003-b/`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A trap-layout repair crosses the guest's layer change — refused as stale; no previous-layer trap is materialized | machine | **pass** | A trap-layout snapshot stamped `run 1 layer 2` (its entries this member's own scanned layout plus one extra) was handed to the guest at `run 1 layer 3`: `[WorldReportGeneration] TrapLayoutSnapshot … belongs to run 1 layer 2 while this side is at run 1 layer 3 — refused as stale`, and the object census stayed at 3004 objects (`s3-af1-trap-stale-guest.json`, `s3-af1-object-before-guest.json`, `s3-af1-object-after-stale-guest.json`). The unstamped control on the same entries was applied — `[TrapLayout] aligning: 1 to materialize, 0 to destroy` and `materialized SpikeStabbed at (600.0,1100.0) from 'spikestabber'` — and the census rose to 3005 (`s3-af4-trap-none-guest.json`, `s3-af4-object-after-none-guest.json`, `s3-guest-refusals.log`) |
| 2 | A runtime-entity creation report crosses the host's layer change — refused; the host neither creates nor relays it, and the reporter is answered | machine | **pass** | A creation report stamped `run 1 layer 2` was handed to the host at `run 1 layer 3`: `[WorldReportGeneration] EntitySpawned … refused as stale` and `[EntitySpawn] refused stale creation shadecrawler at (600.5,1100.5) (creation 76561199526807662:999001) from 76561199526807662 — answering the reporter so its pending report ends.`; the host's accepted-creation table stayed `accepted=0` and the third client's census was unchanged (`s3-af2-stale-create-host.json`, `s3-af2-tables-before-host.json`, `s3-af2-tables-after-host.json`, `s3-host-entity-crossing.log`). The answer goes to the reporter as `RuntimeEntityRejected(StaleGeneration)`; what that answer does to the reporter's own pending entry is staged by row 9 |
| 4 | No stamp / no baseline — the pre-stamp behaviour (UNKNOWN is never treated as fresh) | machine | **pass** | The host accepted an unstamped creation report: `[EntitySpawn] created shadecrawler at (620.50,1120.50) (creation 76561199526807662:999002)`, its accepted-creation table went `0 → 1`, and the relay was sent, with no rejection answered (`s3-af4-none-create-host.json`, `s3-af4-tables-after-host.json`). The guest applied an unstamped trap layout (row 1's control: `aligning: 1 to materialize`, census `3004 → 3005`) |
| 5 | Third-party view — every peer's trap/entity world agrees with the host's after a layer change | machine | **pass** | The same stale trap-layout snapshot (stamped `run 1 layer 3`) was handed to BOTH members at `run 2 layer 0`: both logged the refusal (`s5-af5-stale-trap-guest.json`, `s5-af5-stale-trap-alt.json`, `s5-alt-trapsnapshot-refusal.log`) and NEITHER materialized anything — object censuses `1899 → 1899` on both (`s5-af5-object-after-guest.json`, `s5-af5-object-after-alt.json`). The three clients' closing censuses are identical: `total 1899, 34 groups` on host, guest and alt (`s5-final-object-host.json`, `s5-final-object-guest.json`, `s5-final-object-alt.json`) |
| 6 | The stamp is on the wire, not added by the caller | machine | **pass** | The guest's wire probe decoded the host's real `TrapLayoutSnapshot` frame — 18 819 bytes, `stamp=true run=1 layer=3` — during the guest's own world entry (`s4-wire-trap-read3-guest.json`, `s4-wire-trap-read4-guest.json`), and the host's probe decoded the alt's real `EntitySpawned` frame — 54 bytes, `stamp=true run=1 layer=3` — after the alt spawned a creature through the game's own console (`s3-wire-entity-read-host.json`, `s3-alt-spawn.json`). Both stamps are read from bytes the receiver actually received |
| 7 | A guest receives a stale relay / a stale absolute table — refused; a guest never answers | machine | **pass** | At the guest: a `RuntimeEntitySnapshot` stamped `run 1 layer 2` was refused whole (`[WorldReportGeneration] RuntimeEntitySnapshot … refused as stale`) and a relay-shaped `EntitySpawned` of the same generation was refused too (`… EntitySpawned … refused as stale`), with no answer sent in either direction (`s3-af7-snapshot-stale-guest.json`, `s3-af7-relay-stale-guest.json`, `s3-guest-refusals.log`); the block family's relay is refused the same way in the sibling record's row 6 (`s3-w6-*`) |
| 9 | A stale answer while a second creation sits in the same cell — only the answered creation's pending report ends | machine | **pass** | The guest held two unacknowledged creations in the SAME cell (two hand-built reports through the product's own `SendEntitySpawned`: sequences 999101 and 999102; pending `0 → 2`, `s3-af9-report1-guest.json`, `s3-af9-report2-guest.json`, `s3-af9-pending-2.json`). A stale rejection for the first key only, delivered through the client's own receive seam, ended exactly that entry — pending `2 → 1` (`s3-af9-reject-key1-guest.json`, `s3-af9-pending-1.json`) |

## Residuals for the user

None — every row is a `machine` row and every verdict is read from a probe result, a count or a log line
this run produced against the deployed artifact.

## Limits

- **A genuinely stale sender cannot be held for a scenario on this machine** (the same boundary the
  sibling record names): an inbound blackout past 15 s trips the member's host-silence watchdog, and
  lifting the blackout converges the member to the host's baseline within ~1 s. Rows 1, 2, 4 and 7
  therefore hand the frame to the product's own receive seam (`generation-forge`) with a chosen stamp;
  the retention of two same-cell pending entries in row 9 is a short blackout window (well inside 15 s)
  while the guest stays converged.
- **Row 2's reporter-side half** — its pending report ends and its local copy is removed on the answer —
  is the keyed rejection path row 9 exercises (`pending 2 → 1` on a stale answer); the host-side refusal
  and the answer are read from the host's own log. The adapter's local-copy removal is the same funnel
  batch `20261003-a` accepted.
- **Row 4's second half — "no committed run baseline here" — stays unit-pinned**
  (`RuntimeEntityGenerationIdentityTests.ReportWithoutAStamp_KeepsThePreStampBehaviour` /
  `TrapLayoutGenerationIdentityTests.ASnapshotWithoutAStamp_KeepsThePreStampBehaviour` pair): every
  deployed peer in this run held a committed baseline.
- **Rows 3 and 8 stand from batch `20261002-k`**: same-generation materialization once per peer, and the
  entry group's baseline-before-stamped-tables ordering.
- **The trap family's own census under a diverged world**: the trap refusals and the materialization control
  are read from the guest's own object census before/after and from the product's alignment log, not from a
  cross-peer trap-only census; the known layer-modifier baseline divergence (see the sibling record's
  Limits) keeps a full cross-peer identity read out of this session.
- Physical deployment and the unified dual-client acceptance remain the release-cycle action; this run is
  the agent-run acceptance.
