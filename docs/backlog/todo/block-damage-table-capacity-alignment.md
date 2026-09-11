# Block-damage tables: CUO's registry and the game's own list disagree about capacity and eviction

- Status: Todo
- Priority: Medium
- Category: Sync / world state
- Source: found while fixing S3.2's blocker B1 (the restore replay wrote CUO's registry rows into the
  game's own 128-entry list and pushed the game's OWN rows out). That write path is gone, but the two
  tables still disagree about what they hold.
- Related: `docs/evidence/sync-coverage-matrix.md` row W2, `todo/save-mid-run-consistent-cut.md`
  (S3.2 fixed the restore path), `docs/architecture/save-archive-format.md` §3.4

## Problem

Two bounded tables track "blocks that carry partial damage":

- the GAME's live list `WorldGeneration.world.blockDamages` — cap 128, and when it is full it EVICTS
  THE OLDEST entry and keeps the newest (`WorldGeneration.cs:716-737`: the new entry is added, then
  `blockDamages[0]` is dropped);
- CUO's `BlockDamageRegistry` — cap 256, and when it is full it REFUSES NEW CELLS and keeps the
  oldest (`BlockDamageRegistry.cs:26-27`, `:56-63`).

Past 128 live damaged cells the two tables therefore hold DIFFERENT cells: the game keeps the newest
128, CUO keeps the oldest 256. A late joiner receives CUO's set through `BlockDamageSnapshot`, applies
it into its own list with the game's own cap (`GameBlockDamageTable.Decide`, `:33`, `:76`), and every
row beyond the cap is logged and dropped — so a long run's late joiner ends up with cracks the host
does not have, or is missing cracks the host still shows.

The unhooked `DamageBlock(Vector2Int)` callers (footstep crushing, the spider burrow —
`GameBlockDamageTable.cs:16-17`) make the sets differ a second way: the game's list can hold damage
CUO never observed.

Multiplayer makes this MORE likely, not less: more players damaging more cells at once reach the cap
sooner, and the divergence then shows up as cracks that survive on one side and vanish on the other —
including for building support loss, which reads the crack state.

## Options (decide before implementing)

1. **Align to the game's own policy** — CUO's registry keeps the same bound and the same
   oldest-first eviction as the game, and an eviction is propagated (a dedicated message, or the
   existing remove path) so every side drops the same cell. Faithful to the game's bounded design,
   and a late-joiner snapshot then fits the game's list by construction.
2. **Raise the live capacity on BOTH sides** — take over the game's list bound (host and guests keep
   one larger shared bound) so a long multiplayer run stops silently forgetting the oldest
   half-mined block. This is the only option that improves PLAYER-VISIBLE behaviour beyond
   single-player parity, but it changes a gameplay mechanism (crack rendering, break timing, the
   game's own eviction at `WorldGeneration.cs:732-737`) and needs its own design and in-game proof.
   It is a user-visible change: get the decision first.

Option 1 is the parity fix; option 2 is a gameplay change. S3.2 deliberately fixed only the restore
path (`RestoredWorldFactReplay` writes the archive's game-table rows and nothing else) and left this
open.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | Long run, more damaged cells than the cap | Host, every connected guest and a late joiner hold the SAME damaged-cell set |
| 2 | Eviction | A cell leaving the table leaves it on every side (no side keeps a crack the authority dropped) |
| 3 | Unhooked writers | Damage written by the `DamageBlock` callers CUO does not hook reaches the shared view, or the gap is named in the report |

## Verification limits

The table/eviction rules are Runtime-testable; crack rendering, break timing and building support
loss reading the resulting state need the in-game dual-client pass.
