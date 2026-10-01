# Acceptance record — S3 mid-run consistent cut and world diff

- Ticket: `save-mid-run-consistent-cut` — verdict: **stays in `review/`** (rows 2–6 unproven: the run did not
  stage the ground/worn item restings, the fluid/enemy half, the in-flight save window or the half-applied
  read; each gap is named below)
- Batch: `20261001-m` (Run A) — tickets `save-layer-end-save-and-restore`, `save-mid-run-consistent-cut`,
  `save-native-run-field-parity`, `save-native-character-field-parity`, `save-layer-time-not-carried`,
  `world-determinism-world-fingerprint`
- Commit: `e86241a5` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+e86241a5`
- Run: 2026-10-01 10:34 → 10:47 · Host: physical machine (Steam) · Guest: sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `input`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Mined/placed/quaked blocks + partial damage | machine + residual | pass | placed cell `0→3` (`run-a/block-after-placed.json`), damaged cell `damage -1→5` (`run-a/read-partial.json`), mined cell `13→0` (`run-a/read-mined.json`, `run-a/hit-mined.json`); an `explode` quake produced 1 244 world-block rows in its cut (`run-a/trap-cut-read.json`) and the restore applied 1 424 world-block rows (`run-a/host-restore.txt`); all three hand-made cells read identical after two restores (`run-a/r2b-*`). Residual: the cracked/damaged sprites on screen |
| 2 | World items (ground/container/hand) | machine + named gap | **unproven** | carried + container halves pass: the bag and its two children keep identities/locations across restores (`run-a/restored-container-local.json`, `run-a/restored2-container-local.json`); the ground resting was not driven (`item-drop`/`spawn` not exercised) and the worn resting is the ticket's named gap |
| 3 | Buildings/traps/fluids/enemies | machine + named gap | **unproven** | buildings/traps half pass: the archive carries 3 `building-health` rows (two at health 0) and 4 `trap-consumption` + 4 `trap-state` rows, the restore applied all 12 world-entity rows (`run-a/host-restore.txt`), and the consumed trap's position is empty after the restore (`run-a/trap-check-after-restore.json`); fluids and enemies were not staged (no `floodfill`, `CrystalEnemy=0`), opened lockables are the named gap |
| 4 | In-flight states | machine + partial | **unproven** | the run never took a cut across a deferred window: no lethal hit was applied before a `/save`, so the deferral/count report was not observed live; the cut report does name the classes it never carries (`run-a/layer3-console-read.json`: craft batches, item physics transients, world-clock state, earthquake timers) |
| 5 | Save → load → save → load | machine | **unproven** | three cut/continue cycles ran and the reads stayed stable (same item ids, same cells, `run-a/host-restore.txt`), but the fingerprint half needs a second fingerprint and this run has only the world-entry pair (`run-a/fingerprints.txt`) — see the sibling fingerprint record |
| 6 | Mid-frame cut consistency | machine + partial | **unproven** | the manifest/payload revision equality is visible (cut `revision 1597`, decoder `revision 1597`, `run-a/host-cuts.txt` + `run-a/host-restore.txt`); the "cut across a container operation" half was not staged |
| 7 | Same-layer restore | machine | pass | the cut names the layer, and after the Continue the live layer index and the layout reads are the same one: layer 2 restored with the three mutated cells, layer 3 restored with the same trap position empty (`run-a/rarity-after-continue-host.json`, `run-a/trap-check-after-restore.json`) |

## Residuals for the user

- Row 1: whether the replayed block diff looks right on screen (cracked/damaged sprites).

## Limits

- Rows 2 and 3 could not be completed with the setups this run drove: no ground drop, no worn item, no
  fluid fill, and no live enemy on layers 2–4. These gaps keep the ticket open.
- The trap target is the game's own `explode` (declared substitution); the trap's own trigger path was not exercised.
