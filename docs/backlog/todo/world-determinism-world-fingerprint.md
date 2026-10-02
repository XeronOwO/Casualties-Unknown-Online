# World determinism / WorldFingerprint comparison

- Status: Todo — Rejected (batch `20261002-k`: row 3 unproven — the entry pair is identical on all three clients, but the one-shot `[WorldFingerprint]` log is re-armed on session end only, so no post-mutation re-capture could be taken; the adjacent layer-modifier divergence is filed separately; record `docs/evidence/acceptance/world-determinism-world-fingerprint-20261002-k.md`)
- Priority: High
- Category: Final acceptance

Dual-side runtime world determinism / fingerprint comparison.

## Current implementation (audit 2026-09-09)

The runtime side is a **diagnostic, not a repair path**:

- One-shot FNV-1a fingerprint of the whole block table, logged once per world
  entry: `src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs:70`
  (`private bool _worldFingerprintLogged;`), `:295` (`if (inWorld && !_worldFingerprintLogged)`),
  `:400` (`"[WorldFingerprint] {W}x{H}: ..."`), re-armed on session end (`:501`).
- There is no wire message, no peer comparison, no periodic re-sample and no
  automatic repair; divergence is only observable by comparing the two peers'
  log lines. The block-state 60 s absolute resend
  (`src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:122-133`)
  can mask block-cell differences but is not a fingerprint comparison.
- The generation-segment fingerprints (`[GenStream]`) are the companion
  diagnostic; see `docs/history/audits/worldgen-determinism-audit.md:25-35`.

Audit rows: `docs/evidence/sync-coverage-matrix.md` R7 (verdict
`Transient-by-design`: diagnostic only) and W1 (the guest→host block gap that a
fingerprint comparison would have surfaced faster — closed 2026-09-09 by
`todo/guest-block-mutation-re-report.md`).

## Final-acceptance procedure

1. Host + guest enter the same layer; capture both `[WorldFingerprint]` lines.
2. Compare the eight 64-bit chunks and the total; any difference is a divergence.
3. Re-capture after a mining/placement/earthquake pass; a difference that the
   60 s block-state resend does not heal is a defect.
4. Record the result in the acceptance note.

If an automatic detector is wanted later, it is a new feature (periodic
fingerprint exchange + divergence alert), not part of this acceptance item.

## Acceptance — batch 20261001-m (Run A)

Run record: [world-determinism-world-fingerprint-20261001.md](../../evidence/acceptance/world-determinism-world-fingerprint-20261001.md).
Procedure steps 1, 2 and 4 pass (the entry fingerprint pair is identical); step 3 unproven — no
post-mutation fingerprint pair, because the one-shot log was not re-armed, so the bounded block reads are
the recorded substitute. The ticket stays in `review/`.
