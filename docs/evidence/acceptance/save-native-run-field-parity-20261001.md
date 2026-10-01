# Acceptance record — native run fields (S3.4a)

- Ticket: `save-native-run-field-parity` — verdict: **stays in `review/`** (row 2 is the ticket's own declared
  setup gap: no crafting drive path exists, so the crafted-recipe unlock stays unproven)
- Batch: `20261001-m` (Run A) — tickets `save-layer-end-save-and-restore`, `save-mid-run-consistent-cut`,
  `save-native-run-field-parity`, `save-native-character-field-parity`, `save-layer-time-not-carried`,
  `world-determinism-world-fingerprint`
- Commit: `e86241a5` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+e86241a5`
- Run: 2026-10-01 10:34 → 10:47 · Host: physical machine (Steam) · Guest: sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `input`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Rarity multipliers continue | machine + named gap | pass | the live `lootRarityMultiplier`/`trapRarityMultiplier` read `1`/`1` before and after the layer-3 Continue (`run-a/rarity-layer3-host.json`, `run-a/rarity-after-continue-host.json`, guest pair in `run-a/rarity-layer3-guest.json`); the trap census half is the specific-position check (`run-a/trap-approach.json`: 18 live traps, the consumed one at (-290,337); `run-a/trap-check-after-restore.json`: 0 within 3 units). Named gap: the run used the default run settings, so "non-default run settings accumulate correctly" was not driven |
| 2 | Recipe unlock survives | unproven — setup gap | **unproven** | a crafted recipe needs a crafting drive path this run does not build (declared in the batch scope); no weaker check was substituted |
| 3 | A deliberately-unpersisted field is named | machine | pass | the player-facing cut report names the classes it never carries — "no mid-run cut carries craft batch(es) in progress, item physics transient(s) (velocity/rotation), world-clock state, earthquake timer(s)" (`run-a/layer3-console-read.json`, also `run-a/smoke-console-read.json`), with no silent default |

## Residuals for the user

- None specific to this ticket beyond row 1's named gap.

## Limits

- The rarity-multiplier row ran with default settings, so both multipliers were `1` and equality is
  necessary but weak; a non-default settings pass is still owed before the ticket can close.
- The global trap census is not comparable across a restore (the live loading set differs by region): the
  verdict rests on the consumed trap's own position, not on the total count.
