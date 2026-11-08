# Save — the mid-run cut's block diff after a Continue (batch 20261108-n)

- Batch: `20261108-n`, host desktop client, deployed `0.1.0+99ed67d6`.
- Ticket: `docs/backlog/review/save-mid-run-consistent-cut.md`, acceptance row 1 (mid-run save with mined/placed/quaked blocks + partial damage).

## What the row claimed only the player could see

Row 1's machine half pins the encode/decode shapes and the absolute replay. Its in-game half said: "**in-game: user pass** — that the replayed per-cell diff yields the same block map in the regenerated layer, and that the cracked/damaged sprites match."

The conversion: the block map and the partial-damage row are one read away (`block-read-at`), and the crack sprite is a pure function of that row (`BlockDamage.UpdateSprite`, `reversing/Assembly-CSharp/Assembly-CSharp/BlockDamage.cs:8-44` — `damage / GetBlockInfo(block).health` selects one of five `blockDamageSprites`), so the scene claim is read from the game's own table instead of the eye.

## Fixture

In one mid-run world (host at `(3.652, 497.615)`, body cell `(515, 1009)`):

1. `block-set-at x=516 y=1009 block=1` → `blockAfter: 1`.
2. `block-hit dx=1 dy=0 dmg=20` → the game's own damage roll on the cell: `blockBefore: 1`, `blockAfter: 1`.
3. `block-read dx=1 dy=0` → `block: 1, damage: 20` — the CHIPPED cell.
4. `block-set-at x=514 y=1009 block=1` → `blockAfter: 1`.
5. `block-hit dx=-1 dy=0 dmg=500` → the game's own break: `blockBefore: 1`, `blockAfter: 0`.
6. `block-read dx=-1 dy=0` → `block: 0, damage: -1` — the BROKEN cell.
7. `/save` → `Cut Command committed … mid-run cut taken at frame-end, revision 1000, layer 0, 11 file(s) … 90 world-block row(s), 137 transient row(s)`; the archive's `world-blocks.json` carries the cell rows (`blockState {x, y, block, supportLossSettled}`) and the cut reports one partial-damage row.
8. `leave-world` → `continue-run` → the host is back in world at `(3.085, 497.615)`; the restore log reads `[SaveFacts] restored the live world: 89 block-state row(s) written, 1 partial-damage row(s) applied, … restore complete.` and `CUO restore of world w-20261108-9711 reached the live world: the live world took every restored fact (89 block-state, 1 partial-damage, …)`.
9. `block-read-at x=516 y=1009` → `block: 1, damage: 20`; `block-read-at x=514 y=1009` → `block: 0, damage: -1`.

## What the readings settle

- The broken cell stayed broken: block 0 before the cut and after the Continue, not a regenerated solid cell.
- The chipped cell came back with the same block type and the same accumulated damage: 20 before and after; the restore named exactly one partial-damage row, and the cut named 90 block rows of which 89 were written (the one air row already matched the regenerated baseline).
- The crack sprite is the same sprite: `damage / health` is unchanged (same damage, same block type), so the sprite index is the same bucket. The pixels are not captured: the cell is a table coordinate, not a screen rect.

## Limits

- This is the host's own scene after a solo Continue; a member's client receiving the restored diff is the same writer path (`WorldEventSync.OnRemoteBlockState`) and stays a dual-client reading.
- The sprite bucket is derived from the value; a rendering fault with an identical damage row is outside this row (it would be a sprite bug, not a restore bug).
- The world-block rows do not carry damage; the partial-damage row is the domain that does — the restore log names both counts separately, which is why the two are read separately here.
