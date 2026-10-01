# Acceptance record — S2 layer-end save and restore

- Ticket: `save-layer-end-save-and-restore` — verdict: **stays in `review/`** (row 3 unproven: no live enemy
  was reachable in the layers this run entered; per the batch scope an unreachable setup is never closed on a guess)
- Batch: `20261001-m` (Run A) — tickets `save-layer-end-save-and-restore`, `save-mid-run-consistent-cut`,
  `save-native-run-field-parity`, `save-native-character-field-parity`, `save-layer-time-not-carried`,
  `world-determinism-world-fingerprint`
- Commit: `e86241a5` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+e86241a5`
- Run: 2026-10-01 10:34 → 10:47 · Host: physical machine (Steam) · Guest: sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `input`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Layer-end save → quit → continue | machine + residual | pass | layer-end cut committed at the layer boundary (`run-a/host-cuts.txt`), the two deliberate leaves commit `MenuReturn` cuts, and three `continue-run` cycles restore the character, run and item state (`run-a/cycle1-continue.json`, `run-a/cycle2-continue.json`, `run-a/cycle3-continue-decoy.json`, `run-a/host-restore.txt`); the restored reads are identical across the cycles. Residual: the player-visible "it all came back" half |
| 2 | Container tree after restore | machine | pass | the seeded bag keeps exactly two children after the first and the second restore (`run-a/restored-container-local.json`, `run-a/restored2-container-local.json`): each `dogfood` appears once with `parentType=trashbag`, the bag lists both ids, no duplicate |
| 3 | Terminal facts stay terminal | machine | **unproven** | trap half pass: the exploded trap's own position carries no live trap after the restore (`run-a/trap-check-after-restore.json`, `nearDestroyed=0`) and the archive keeps it terminal (world-entities rows `trap-consumption`/`trap-state`, restore report in `run-a/host-restore.txt`); enemy half not reached — no live `CrystalEnemy` on the layers entered (`run-a/entity-census-layer4-host.json`), so "a located enemy is killed" was never set up |
| 4 | Restore twice | machine | pass | two consecutive leaves + continues leave the three mutated cells identical (`run-a/restored-block-after-placed.json`, `run-a/r2b-placed.json`, `run-a/restored-block-after-partial.json`, `run-a/r2b-partial.json`, `run-a/restored-block-after-mined.json`, `run-a/r2b-mined.json`) and the item set identical (bag `2199344236483`, children `2203639203779`, `2207934171075`) |
| 5 | A fresh native `save.sv` is ignored | machine | pass | decoy staged at the native path with its hash recorded (`run-a/decoy-hashes.txt`): after the Continue the decoy is byte-identical (`379913A0700745CA…`), the host log has zero `save.sv` mentions, the CUO restore line is the one in `run-a/host-restore.txt`, and the original file was put back (hash `A1F141CA7C337E67…`) |
| 6 | Continue entry reachable with no native save | machine | pass | three continues report the pre-click reading from the CUO repository: `buttonOffered=true`, `buttonInteractable=true`, `buttonLabel="Continue"` (`run-a/smoke-continue-run.json`, `run-a/cycle1-continue.json`, `run-a/cycle3-continue-decoy.json`) |

## Residuals for the user

- Row 1: the player-visible half — that the continued world looks like the world that was left (same items in
  hand and on the ground, same map). Artifact: `run-a/restored-container-local.json` plus the run frames.

## Limits

- Row 3's enemy half needs a layer with a live enemy; this run entered layers 2–4 and found none, so the
  "kill an enemy, restore, it stays gone" half is unproven and its ticket stays in `review/`.
- The layer timer behaviour observed in this run is recorded in the sibling record
  `save-layer-time-not-carried-20261001.md`; it does not affect the rows above.
- The target the run used for the trap half is the game's own `explode`, the declared substitution of the
  batch scope; the trap's own trigger path was not exercised.
