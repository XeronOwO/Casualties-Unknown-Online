# The crafted recipe's flag survives a mid-run cut (2026-11-08)

- Ticket: `docs/backlog/review/save-native-run-field-parity.md`, acceptance row 2 (continue after crafting a recipe → the recipe stays unlocked)
- Batch: `20261108-s` (the three clients already up from batch `20261108-r`; host at `(0, 505.615)`)
- Build: the deployed `0.1.0+9869d734` (acceptance channel)
- World: `w-20261108-d1cf`

## What was done

1. The host body was dead on the first read (`alive: false`, `conscious: false`, `consciousness: 0`) — the grant answered `no-free-slot` / "every body slot is occupied or unusable". `/heal @p` revived it (the console answered that the character had been dead and was alive again).
2. `recipe-unlock-read index=7` before the craft: `INT: 4, hasMadeBefore: false`.
3. `item-provide type=glowplantfruit mode=create` → `ok, instanceId 1473494763459, slot 0`.
4. `craft-drive mode=make,index=7` → `ok, result: biochem`.
5. `recipe-unlock-read index=7` after the craft: `INT: 4, hasMadeBefore: true`.
6. `/save` → "Save queued: the cut is taken at the next pump boundary (it waits for any in-flight operation that must resolve first), and the result appears here."
7. `leave-world` → `ok, inWorld: false`; `continue-run` → `ok, inWorld: true`.
8. `recipe-unlock-read index=7` after the continue: `INT: 4, hasMadeBefore: true`.

## The cut and the restore, from the host log

- `Cut MenuReturn committed for world w-20261108-d1cf: mid-run cut taken at frame-end, revision 2264, layer 0, 11 file(s), backup …mid-run-20261108-064739.cuoz; … native run fields present (133 recipe row(s)).`
- The archive's `run.json` `native-run-fields` row carries `index 7 { madeBefore: true, intValue: 4 }` (read from that backup).
- `Handed 49 keypad code(s), 97 geyser liquid type(s), 2 game block-damage row(s) and run fields (clock 3921.3, 133 recipe row(s)) to the native applier.`
- `[SaveFacts] committed the restored native world facts (… 133 recipe unlock row(s)): the live world has them.`
- `[SaveFacts] restored the live world: … 133 recipe unlock row(s) applied (the archive carried 133) …`

## What the reading settles

Row 2 passes on this build: a recipe crafted before a mid-run cut is still marked made-before after the continue, and its INT requirement is unchanged. The earlier batch `20261025-s` read `hasMadeBefore: false` after its continue; this batch does not reproduce that on `0.1.0+9869d734`.

## Limits

- One recipe (index 7) and one cut kind (mid-run, `MenuReturn`). The layer-end cut carries the same typed row (the cut log says so for the `LayerAdvance` cut in batch `20261108-a`), but no craft was re-read across a layer-end cut in this batch.
- The restore reported `restore INCOMPLETE (… 7 applied / 7 refused world-entity row(s), 7 row(s) not taken)` — the recipe half is not implicated, but the refused world-entity rows are a separate finding (a world-entity restore gap), not part of this row.
