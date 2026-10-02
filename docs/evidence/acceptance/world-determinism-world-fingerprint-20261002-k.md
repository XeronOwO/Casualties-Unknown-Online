# Acceptance record — World determinism / WorldFingerprint comparison

- Ticket: `world-determinism-world-fingerprint` — verdict: **back to `todo/`** (row 3 unproven)
- Batch: `20261002-k` — tickets `guest-block-mutation-re-report`, `runtime-entity-spawn-backfill`,
  `enemy-snapshot-binding-recovery`, `item-creation-registration-first`, `world-layer-generation-identity`,
  `generation-identity-remaining-families`, `world-determinism-world-fingerprint`
- Commit: `393d79a8` (artifact) · tree `b20984cb` (docs-only) · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+393d79a8c0d8f4a20320b667f6122884daa656ae`
- Run: 2026-10-02, 16:38–17:03 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present
- Artifacts: `k-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 3 | Re-capture the two `[WorldFingerprint]` lines after a mining/placement/earthquake pass; a difference the 60 s block-state resend does not heal is a defect | machine | **unproven** | the entry pair was captured and identical on all three clients — `[WorldFingerprint] 1024x1024: A42E91B44E661D7B 661F28180D7F4B2E D12E8E1CD6A34133 28A4CF5DCE34A35F 8B5D98F8D52ACF7C 59B02C3895A2403A 3B400E00CADDAFFF 04BD7B68A66B6F8F total E8EE9E5455217CDA` (`k-A-entry-host.log`, `k-A-fingerprint-guest.log`, `k-A-fingerprint-alt.log`), re-confirming steps 1–2 of the procedure; the **post-mutation re-capture did not happen**: the one-shot log is re-armed on session end, and leave-world + Continue produced no new `[WorldFingerprint]` line on any client (`k-F-fingerprint-only-host.log`, `-guest.log`, `-alt.log` are empty). The mutation pass itself (guest mine/place, host break, runtime spawns, item operations) is recorded in the other batch records. |

## Adjacent live finding (recorded, not this row's verdict)

The same re-entry exposed a determinism-relevant divergence the ticket's diagnostic would have had to explain:
the Continue restored the run's **layer-end cut** (`Projected kernel run baseline (run 1, layer 1)`,
`Captured world baseline (1024x1024) — a restored cut is pending; … the runtime-entity table is reset` —
`k-F-world-host.log`), and both guests then repeated
`[LayerMod] baseline divergence — local segment start 5200E7D148E10BB7426A68F7E2407117 vs host's E76DFACE27BBFC1FC9FFB1C2648822EB (world effects may diverge)`
every 10 s (`k-F-baseline-guest.log`, `k-F-baseline-alt.log`) while the host's enemy set stayed at 74 against
the guests' 85 (`k-census-F-*.json`). Filed as `docs/backlog/done/layer-mod-baseline-divergence-on-continue.md`.
**Attributed 2026-10-02**: the Continue had restored a stale world (the repository pointer still named a
2026-10-01 world), not the run's own; the missing first-cut pointer write is fixed there, and rows 3/4/8 of
`enemy-snapshot-binding-recovery` are re-run in batch `20261002-l`.

## Limits

- The re-capture needs a **session restart** (quit + relaunch + re-join) or a device that re-arms the one-shot
  log; a world re-entry does not re-arm it (this run's finding).
- The entry pair is one sample; the fingerprint is a diagnostic, not a repair path.
