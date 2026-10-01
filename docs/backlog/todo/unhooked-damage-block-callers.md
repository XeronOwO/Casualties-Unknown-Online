# Host block damage reports: two native `DamageBlock` callers are not hooked

- Status: Todo — Rejected (batch `20261002-c`: row 4 fails — the receiving side's presentation write IS reported back to the host; rows 1, 3, 5, 6 pass from batch `20261002-b`; row 2 stays open — no reachable spider-burrow path on this machine)
- Acceptance records: `docs/evidence/acceptance/unhooked-damage-block-callers-20261001-x.md`, `docs/evidence/acceptance/unhooked-damage-block-callers-20261001-y.md`, `docs/evidence/acceptance/unhooked-damage-block-callers-20261002-b.md`, `docs/evidence/acceptance/unhooked-damage-block-callers-20261002-c.md`
- Priority: Low-Medium
- Category: World block damage / report coverage
- Source: found while fixing `done/guest-hears-only-some-block-break-sounds.md` (the presentation half of the same report). The mechanism inventory for that fix censused every native `DamageBlock` call site and found two that never produce a report, because CUO patches only the `Vector2` overload.
- Related: `docs/evidence/selfchecks/world/unhooked-damage-block-callers-selfcheck.md`, `tests/CasualtiesUnknownOnline.NormativeGates.Tests/DamageBlockHookCoverageGateTests.cs`, `docs/backlog/done/guest-hears-only-some-block-break-sounds.md`, `docs/backlog/done/block-damage-table-capacity-alignment.md`

## Problem

`WorldGeneration` has two `DamageBlock` entry points and CUO patched only the outer one
(`WorldGenerationDamageBlockPatch` — the `Vector2` overload that converts to a cell and calls the
`Vector2Int` overload). Two native callers enter the INNER overload directly, so their damage was
applied and their sound played on the acting side while the peers learned nothing about it:

- `Body.cs:2709` — the footstep crush: a grounded body standing on a `health <= 1` block calls
  `DamageBlock(cell, 1f, hitSound: true, bonusMetal: false, ignoreLoot: false)` for each of the three
  cells at foot level. The host heard the break; the guest saw the block vanish through the air-write
  relay, with no damage report, no hit/break sound and no folded drops.
- `SpiderHandler.cs:218` (`CheckForBlockDamage`) — a burrowing spider calls
  `DamageBlock(cell, burrowWallDamage, hitSound: true, bonusMetal: false, ignoreLoot: true)`. The air
  write replicated the wall's disappearance but not the partial damage, so a guest's crack state
  diverged from the host's until the 60 s snapshot.

## What landed (2026-09-26 cycle)

The hook is anchored on the CHOKE POINT instead of on the forwarder, and the report carries the cell.

- `WorldGenerationDamageBlockPatch` binds `DamageBlock(Vector2Int, float, bool, bool, bool)`
  (`WorldGeneration.cs:711`) — the body every native damage roll enters — and no longer converts: the
  cell is that overload's own parameter. All five call sites are covered by the one hook, and the
  documented blind spot is closed by that anchor and pinned by a source-read gate (an anchor written as
  stacked attributes or a manual `PatchProcessor` call is outside the gate's reach). A second anchor
  beside the old one was rejected: the forwarder's calls reach the body too, so it would report the
  mining/attack family twice and need a re-entrancy flag to hide it.
- `IPatchBridge.OnBlockDamaged`, its `GameAdapterBridge` forwarding and
  `BlockBreakSync.OnBlockDamaged` take the CELL (`Vector2Int`) rather than a world position. The wire is
  untouched — `BlockDamagedMsg` already carried `X`/`Y`, so the position never left the process — and no
  protocol version moves (a fact, not a merit).
- The game gates its WHOLE loot step on `ignoreLoot` (`WorldGeneration.cs:751`), so CUO's authored
  custom-tile drops are gated the same way: a caller that asked for no loot (the spider burrow) gets
  none. For the forwarder's callers the flag is always `false`, so every pre-existing path keeps its
  drops. The crush is the one NEW caller of that hook, and it has no executing test — the provider needs
  the running game — so its coverage is the native gate read plus the audit in the selfcheck, not a
  green case; the dual-client run is where it gets observed (crush a mod tile: exactly one authored drop
  set; burrow through one: none).
- A remote apply now passes through the patch too: it stays silent (`!IsLocalAction` returns before the
  bridge, `BlockBreakSync`'s `RemoteApply` guard remains the deep one), it cannot roll loot (every
  remote apply passes `ignoreLoot: true`) and it cannot spawn custom-tile drops (`OnCustomTileBroken` is
  local-only). The air write's `PlayerBreak` claim is untouched by it — `WorldEventSync.OnBlockSet`
  early-returns under `RemoteApply` — so a received break is not re-broadcast.
- The queue's own blocker is answered: a guest's spider clone cannot run the burrow path at all, because
  `EnemyPatches` prefix-skips `SpiderHandler.Update`/`FixedUpdate`/`OnCollisionEnter2D`/`OnCollisionStay2D`
  while the spider carries a `RemoteEnemyDriver`. The spider half therefore lands in the same cycle —
  the only new spider reports are the HOST's own, which is what the guest needs.
- Both the report coverage and the presentation claim now cover these two callers: the roll opens
  `DamageBlockOrigin`, so the air write goes out stamped as a player break and the peer PRESENTS it
  through the game's own damage roll instead of writing silent air.

## Acceptance criteria

| # | Scenario | Expected | Verified by |
|---|---|---|---|
| 1 | Host walks over a `health <= 1` block (footstep crush) | the guest hears the same break and receives the damage, not only the air write | the roll now reports (gate + selfcheck §3); the audible half is the user's dual-client pass |
| 2 | Host's spider burrows through a wall | a wall the burrow only DAMAGES converges on the guest through the report (crack state follows before the snapshot); a wall it BREAKS is presented on the guest through the air write's claim, and the damage report that follows is discarded there because the cell is already air (`BlockBreakSync`'s `blockIsAir` guard — by design, damaging air would invent a transient row and play a sound for a block that is gone) | the report path (gate + selfcheck §3); the two outcomes and the discard point are stated in selfcheck §8; the audible half is the user's dual-client pass |
| 3 | Guest's own footstep crush, host listens | same, reverse direction | the same hook sees a guest's local roll and reports it (`BlockBreakSync` sends, the host arbitrates) |
| 4 | A remote apply (the CUO applier's own `DamageBlock` roll) | no report, no echo | the chain-query guards (`CallContext.IsWithin(RemoteApply)`) in the patch, `BlockBreakSync` and `WorldEventSync.OnBlockSet` — batch `20261002-c` falsified the innermost-origin forms; pinned by `CallContextScopeCompositionGateTests` and `CallContextCompositionTests` |
| 5 | Third peer | same cadence | the existing broadcast channel (`SendBlockDamaged` → host relay), unchanged |
| 6 | Report volume | the footstep path reports once per crushed cell, the burrow path stays on its own bite cooldown — no per-frame stream | the native call sites are discrete (`Body.cs:2704-2711`, `SpiderHandler.cs:215-218`); the report follows the roll one-for-one |

## Evidence

- Selfcheck: `docs/evidence/selfchecks/world/unhooked-damage-block-callers-selfcheck.md`
- Gate: `tests/CasualtiesUnknownOnline.NormativeGates.Tests/DamageBlockHookCoverageGateTests.cs`
  (red recorded before the change: 2 failed / 8 passed / 10)
- Code: `src/CasualtiesUnknownOnline.GameAdapter/Patches/WorldGenerationDamageBlockPatch.cs`,
  `IPatchBridge.cs`, `GameAdapterBridge.cs`, `World/BlockBreakSync.cs`
- Native: `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:711` / `:851`,
  `Body.cs:2702-2712`, `SpiderHandler.cs:213-218`
- Gate inventory: `docs/evidence/normative-gates.md`

## Limits

- The new gate pins the ANCHOR and the seam's cell shape; that the peer hears the break, sees the crack
  converge and receives the drops is the user's dual-client acceptance run.
- The gate reads source: an anchor expressed another way (attribute stacking, a manual `PatchProcessor`
  call) is out of its reach, and the counts it asserts make a silent disappearance loud.
- The clone answer is static (the patches, not a running guest): an enemy not yet bound by a
  `RemoteEnemyDriver` on a guest would run natively and now report its roll — arbitrated like any other
  guest mutation, and that window is pre-existing.
- `ignoreLoot` on the custom-tile drop path has no Unity-side test; it is covered by the native gate and
  the family audit.

## Non-goals

- Not changing who owns block damage authority (the host still arbitrates).
- Not adding a per-frame damage stream: both callers are discrete events.
