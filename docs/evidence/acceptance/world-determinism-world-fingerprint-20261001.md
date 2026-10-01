# Acceptance record — world determinism / WorldFingerprint comparison

- Ticket: `world-determinism-world-fingerprint` — verdict: **stays in `review/`** (procedure step 3
  unproven: the run has no post-mutation fingerprint pair — the fingerprint is logged once per world entry
  and the Continue path did not re-arm it)
- Batch: `20261001-m` (Run A) — tickets `save-layer-end-save-and-restore`, `save-mid-run-consistent-cut`,
  `save-native-run-field-parity`, `save-native-character-field-parity`, `save-layer-time-not-carried`,
  `world-determinism-world-fingerprint`
- Commit: `e86241a5` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+e86241a5`
- Run: 2026-10-01 10:34 → 10:47 · Host: physical machine (Steam) · Guest: sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `input`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Step | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Capture both peers' `[WorldFingerprint]` | machine | pass | both clients in the same layer at world entry log one fingerprint each (`run-a/fingerprints.txt`) |
| 2 | Compare the eight 64-bit chunks and the total | machine | pass | the two lines are byte-identical: `1024x1024: D90C406954C18997 41DE529F12157592 B20542827DD193D8 33A0AF5BCFE0FBBF 16D5729D999A4216 729DA817AE28DFBC 5874AF063419F642 906DE1EB9D8A9114 total 71B579803C9D7767` |
| 3 | Re-capture after a mining/placement/earthquake pass | machine | **unproven** | the mutation pass ran (placed/damaged/mined cells plus an explosion quake, `run-a/trap-cut-read.json`) and the restores reproduced it (`run-a/host-restore.txt`), but neither client logged a second `[WorldFingerprint]` — each peer's log holds exactly one line — so the declared re-capture could not be produced; the bounded block reads are the recorded substitute |
| 4 | Record the result | machine | pass | this record states the comparison and the gap |

## Limits

- Step 3's substitute (absolute block reads across the restores) is weaker than a fingerprint pair: it shows
  the saved cells came back, not that both peers' whole block tables match after a mutation pass.
- Producing the re-capture needs a fresh world entry (a session that re-arms the one-shot log) or a periodic
  fingerprint feature; the latter is explicitly out of scope for this ticket.
