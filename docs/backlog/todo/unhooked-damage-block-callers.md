# Host block damage reports: two native `DamageBlock` callers are not hooked

- Status: Todo
- Priority: Low-Medium
- Category: World block damage / report coverage
- Source: found while fixing `review/guest-hears-only-some-block-break-sounds.md` (the presentation half of the same report). The mechanism inventory for that fix censused every native `DamageBlock` call site and found two that never produce a report, because CUO patches only the `Vector2` overload.

## Problem

`WorldGeneration` has two `DamageBlock` entry points and CUO patches only the outer one
(`WorldGenerationDamageBlockPatch` — the `Vector2` overload that converts to a cell and calls the
`Vector2Int` overload). Two native callers enter the INNER overload directly, so their damage is
applied and their sound is played on the acting side while the peers learn nothing about it:

- `Body.cs:2709` — the footstep crush: a grounded body standing on a `health <= 1` block calls
  `DamageBlock(cell, 1f, hitSound: true, bonusMetal: false, ignoreLoot: false)` for each of the three
  cells at foot level. The host hears the break; the guest only sees the block vanish through the
  air-write relay, with no damage report, no hit/break sound and no folded drops.
- `SpiderHandler.cs:218` (`CheckForBlockDamage`) — a burrowing spider calls
  `DamageBlock(cell, burrowWallDamage, hitSound: true, bonusMetal: false, ignoreLoot: true)`. The air
  write replicates the wall's disappearance but not the partial damage, so a guest's crack state
  diverges from the host's until the 60 s snapshot.

Both are the same family as the reported defect ("the guest hears only some of the host's block hit
sounds"), but a different mechanism: the reported one was a PRESENTATION gap on a report that does
exist, this one is a REPORT-COVERAGE gap — no report is produced at all.

## Why it is not fixed in the same cycle

The clean fix is to re-anchor the patch to the `Vector2Int` overload (the true choke point: every
native damage roll passes through it, and the `Vector2` overload only forwards) and widen
`IPatchBridge.OnBlockDamaged` from a world position to the cell. That would cover all five native
call sites with one hook and delete the documented blind spot. It is a separate ticket because of the
second caller: the spider path is an ENEMY-caused world mutation, and the role semantics of a guest's
spider clone (whether a frozen remote copy can still run `CheckForBlockDamage` and would then report
its own local roll to the host) have to be verified first — with a re-anchor those reports would start
flowing, and that is a state-sync change, not a sound one. The footstep-crush half is unambiguously a
local player action and can be split out if the spider half needs its own cycle.

## Acceptance criteria (for the implementation cycle)

| # | Scenario | Expected |
|---|---|---|
| 1 | Host walks over a `health <= 1` block (footstep crush) | the guest hears the same break and receives the damage, not only the air write |
| 2 | Host's spider burrows through a wall | the guest applies the wall damage (crack state converges before the snapshot) and hears what the host heard |
| 3 | Guest's own footstep crush, host listens | same, reverse direction |
| 4 | A remote apply (the CUO applier's own `DamageBlock` roll) | no report, no echo (the `RemoteApply` guard) |
| 5 | Third peer | same cadence |
| 6 | Report volume | the footstep path reports once per crushed cell, the burrow path stays on its own bite cooldown — no per-frame stream |

## Non-goals

- Not changing who owns block damage authority (the host still arbitrates).
- Not adding a per-frame damage stream: both callers are discrete events.

## Evidence

- `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:711` / `:851` — the two overloads (the
  inner one is the body, the outer only converts and forwards).
- `grep -rn "DamageBlock(" reversing/Assembly-CSharp/Assembly-CSharp/*.cs` — the five call sites
  (`Body.cs:1929`, `Body.cs:2709`, `Limb.cs:384`, `SpiderHandler.cs:218`, `TurretScript.cs:144`).
- `src/CasualtiesUnknownOnline.GameAdapter/Patches/WorldGenerationDamageBlockPatch.cs` — the patch
  attribute names the `Vector2` overload, and its doc comment states the gap.
- `docs/backlog/review/block-damage-table-capacity-alignment.md` — records the same gap as a fact
  about the game's own damage list ("the direct `DamageBlock(Vector2Int)` callers — footstep
  crushing, the spider burrow — are not hooked").
