# The host rolls a second drop set for a death a member already rolled

- Status: Todo — opened 2026-10-18 by the audit inside the entity-event drop bound (the
  `NetMsg.EntityEvent` row of `docs/backlog/done/member-report-bounds.md`).
- Priority: Medium
- Category: Sync / item ownership
- Source: the bounds cycle's family audit — the same audit that gave a member's reported event drops a bound
  found that the host's OWN application of the event still rolls a set for the same death.
- Related: `docs/backlog/done/member-report-bounds.md` (the bound that admits the member's report),
  `docs/evidence/sync-coverage-matrix.md` (row E2), `docs/architecture/member-report-validation.md`
  (`NetMsg.EntityEvent`), `done/trap-destruction-drop-quantity-desync.md` (the support-loss carrier's
  "one death, one roll" rule), `done/validation-and-source-checks.md` (S2b)

## The defect

The host's application of a member's destructive entity event runs the game's own explosion
(`TrapEffectApplier.ApplyMineExplosion` calls `WorldGeneration.CreateExplosion`), and that explosion damages the
host's living entities. The applier marks the TRAP entity as a remote death, but not the blast's other victims,
so when such a victim's health reaches the death line, `BuildingEntity.Update`'s death branch rolls its own
drops on the host and `ItemWorldSync.OnItemInstantiated` reports them through the standalone item path. The
reporting member's own copy of that blast rolled its own set at the same moment and reported it inside
`EntityEventMsg.Drops`, which the host now bounds and admits into the kernel and the relay. One death, two
sets: the member's reported items and the host's re-roll, with different instance ids and conditions.

## Evidence (paths plus the quoted text; `reversing/` keeps its line numbers because that tree is never edited)

- The blast really runs natively on the host:
  `TrapEffectApplier.ApplyMineExplosion` → `WorldGeneration.CreateExplosion(new ExplosionParams { position = position + Vector2.up });`
  (the turret's and the unstable crystal's appliers call it with their own parameters).
- The native damage is what kills the victim:
  `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:3989` →
  `buildingEntity.health -= param.structuralDamage * Random.Range(0f, 2f);`.
- The host's victim is not marked:
  `ExplosionBuildingSync.ReportDamaged` reports the health drop through
  `WorldBuildingEntitySync.OnBuildingEntityDamaged`, which only forwards the fact; the only
  `RemoteEntityDeath` additions are the remote-damage/open/snapshot applications, the support-loss mark and
  the trap entity itself.
- The roll then happens on the host:
  `reversing/Assembly-CSharp/Assembly-CSharp/BuildingEntity.cs:56-123` — the death branch rolls
  `itemsDropOnDestroy` / `guaranteedDropAmount` / `alwaysDrop` when the entity is not a remote death, and
  `ItemWorldSync.OnItemInstantiated` reports the resulting items standalone (`BuildingDeathDropOrigin` is
  attached by `ItemPatches.ItemAwakePatch`, and no pending trap or break owns the drop on the host).
- The member's set arrives through the event:
  `EntityEventMsg.Drops` (`TrapDropEntryMsg`) is the peer's own roll of the same death; the bound
  (`TrapDropBoundJudge`) admits it against the host's own tables, so both sets exist.

## Why the bounds cycle did not fix it

The obvious suppression — mark every entity the host's blast drives below the death line as a remote death in
the same scope that captures the drop tables — was attempted and withdrawn before the commit: it fixes the
direct blast but the geometry is not the whole family.

- **Chain explosions run OUTSIDE the apply scope.** A chained mine the blast destroys is marked remote-death,
  so its own `Update` does not roll, but its `OnDestroy` still chain-explodes
  (`reversing/Assembly-CSharp/Assembly-CSharp/MineScript.cs:14-23`:
  `if (this.build.health < 0.5f && !this.exploded) WorldGeneration.CreateExplosion(...)`). That second blast
  happens after the scope has closed, so its victims are neither captured nor marked, and the member's own
  chain event carries its own rolled set. Marking the direct victims would leave the chain carrier's
  double roll in place while making the cycle's summary claim a family it does not cover.
- **The damage roll is per-side and unseeded.** `Random.Range(0f, 2f)` is each process's own stream, so the
  host can kill an entity the member's blast did not. Suppressing the host's roll for that entity loses the
  drop on both sides: the member's copy survived, so no reported set exists, and the host's own roll was the
  only one. The ownership rule has to answer "who owns a death only one side produced" before it can
  suppress anything.

So the bound is honest on its own row (a report's drops are checked against the host's own tables) and this
ownership question is its own work item.

## What the next cycle must settle BEFORE writing code

- **Which side owns a death.** The general rule the project already uses is "the side that observed the death
  rolls, the other side only removes the entity" (`RemoteEntityDeath`). The host's own blast is the side that
  observed the death for the host's copies — so for a member-triggered event, the host must not roll, and the
  member's report is the owner. The chain case then needs the host's applier to be able to replay the chain
  blast (the way it replays the first one) instead of the chained entity's `OnDestroy` running it natively
  outside the scope.
- **What happens to a host-only death.** A victim only the host's damage roll killed has no reported set.
  Either the host's roll stays (and the duplication is accepted for that case, named), or the host reports the
  set itself through a channel the reporter can settle — that is a design decision, not a detail.
- **Where the mark belongs.** `TrapBuildingHealthScope` already sees every entity the host's blast damaged
  while it still exists; the support-loss path marks in the same capture
  (`WorldBuildingEntitySync.MarkSupportLossRemote`). If the ownership rule is "the triggerer owns", the mark
  belongs there only for the DIRECT blast; the chain case needs its own seam.

## Acceptance this row owes

A two-client session, one row per shape: a member-triggered mine next to a droppable building must leave
EXACTLY ONE set of that building's drops in every client's world; a chain mine (a mine whose blast kills
another mine) must leave one set per death, with no set lost and none doubled; and an ordinary session must
show the host's own `[ItemSpawned]` line for a blast victim zero times when the member's report is the owner.

## Non-goals

- No change to the member-report bound itself: the reported set is the claim, and that row is already bounded.
- Not an anti-cheat measure: nothing here models what a player can do; it decides which client's roll becomes
  the world's item facts.

## Decision point (2026-10-24) — waiting on the user

The ownership rule is the open question, and it is a gameplay decision because it decides whose roll becomes
the world's item facts:

1. **The triggerer owns the death** (recommended — it is decision 184/265's own rule: the side that observed
   the death rolls, the other side only removes the entity). The host's applier would mark the blast's victims
   as remote deaths and replay a chain blast instead of letting the chained mine's `OnDestroy` run it outside
   the scope. The cost is the chain seam.
2. **The host owns every death its world simulates**: simplest for the direct blast, but it contradicts the
   member's reported set and leaves the duplicate.

The host-only-death case needs one word too: when the host's own damage roll kills a victim the member's blast
did not, option 1 leaves the host's roll as the only set (no report exists) — accept that named hole, or add a
host-authored report channel.

> **The recommendation this ticket first carried was wrong, and the user called it out (2026-10-24).** It proposed
> "option 1 plus the named host-only case", i.e. it traded the correct behaviour for not touching the wire. The
> repository's own rule says compatibility is never a design input and the protocol number simply follows the
> change, so the host-authored report channel is the recommendation the ticket should have carried: a known hole is
> never cheaper than a message. The ruling below is the corrected one.


## The ruling (user, 2026-10-24): the side that set it off owns the death, and the host reports its own

Asked in plain terms (a member sets off a mine, the blast shatters a crate that drops items, and each machine rolls
its own set), the user chose:

1. **The side whose blast happened decides what the destroyed thing drops**; the other side only removes the entity
   and never rolls its own set. That is decision 184/265's own rule ("the side that observed the death rolls"), and
   it is what kills the duplicate this ticket is about.
2. **The host also reports its own rolls.** The damage roll is per-side and unseeded, so a victim can die only on
   the host's copy; that set must reach the members instead of being a named hole, which means a host-authored
   report channel rather than "the host's roll stays and the duplicate is accepted".

The work this settles, in the order it has to land:

1. **The direct blast's victims are marked as remote deaths on the non-owner side.** `TrapBuildingHealthScope`
   already sees every entity the host's blast damaged while it still exists, and the support-loss path marks in the
   same capture (`WorldBuildingEntitySync.MarkSupportLossRemote`), so the mark belongs there: a member-triggered
   event's blast victims stop rolling on the host, and the member's reported set is the world's only set.
2. **The chain case.** A chained mine the blast destroys is marked, so its `Update` stops rolling, but its
   `OnDestroy` still calls `WorldGeneration.CreateExplosion` natively OUTSIDE the apply scope
   (`MineScript.cs:14-23`), so the second blast's victims are neither captured nor marked. The host's applier must
   replay a chain blast inside the same scope instead of letting `OnDestroy` run it, which is the seam this ticket
   said it owed.
3. **The host's own report channel.** When the host's damage roll kills a victim no member reported (a host-only
   death), the host publishes that death's set so both sides agree. That is a new host→member message, and its
   member-report census row (`docs/architecture/member-report-validation.md`) follows with it: the set is authored
   by the host, so the receiving side treats it as a fact from the authority rather than a claim to bound.
4. **Acceptance** stays the ticket's own rows: one set per death for the direct blast, one per death for the chain,
   and the host's own `[ItemSpawned]` line absent for a blast victim whose death the member owns.

Nothing is implemented by this ruling itself; slice 1 is the next code cycle and slices 2–3 follow it.

## Slice 1 landed (2026-10-24, deployed `0.1.0+19881b9d`)

`TrapBuildingHealthScope` — the scope that already saw every entity the host's blast damaged while it still existed —
now marks each of them as a REMOTE death (`RemoteEntityDeath.Mark`, the same call the support-loss path uses for the
same reason), which is what the game's own death branch reads before it rolls. So for a member-triggered event the
host applies the blast, removes the entities and rolls nothing; the member's reported set is the world's only set.
The scope logs how many it marked (`[TrapBlast] marked N blast victim(s) as remote deaths …`).

**Still owed**: the ticket's three acceptance rows, which need two clients and a mine placed next to a droppable
building. Slices 1–2 are landed (below) and slice 3 turned out to need no message (also below).

## Slice 3 does not need a message: the host's own roll already reaches the members (read 2026-10-24)

The ruling above asked for a host-authored report channel. Tracing where a host-side drop ALREADY travels shows the
channel exists, so that half needs no new message:

- the host's own roll creates an item whose `Item.Start` reaches `ItemWorldSync.OnItemInstantiated`, which reports it
  through `IItemControl.SendItemSpawned` (`ItemWorldSync`'s `[ItemSpawned] local …` line is that call's own);
- on the host, `ItemMessageFlowService.SendItemSpawned` applies the row to the authoritative projection
  (`_projection.ApplySpawn(_session.LocalSteamId, …)` — it only sends a kernel COMMAND when the role is a guest),
  and the kernel's committed batch is what the members receive
  (`KernelProtocolService` subscribes `BroadcastCommittedBatch` to `BatchCommitted`);
- so a drop only the host's roll produced is already authoritative world state on every client. There is no
  "unsynchronised set" for that case: what the user's ruling protects is that the members must agree with the host,
  and they do — through the authority rather than through a report.

What that leaves is exactly slice 2 (the chain blast's victims falling outside the scope, so the host rolls a set for
a death the member's own client also rolled) and the acceptance rows. A new message would have been a second way to
say the same thing.

## Slice 2 landed (2026-10-24, deployed `0.1.0+eac739c9`): the chain fires inside the scope

A mine the blast drives below its firing line explodes from its OWN `OnDestroy`
(`MineScript.cs`: `if (this.build.health < 0.5f && !this.exploded)`), and that destroy runs after the apply scope has
closed — so the chain's victims were never captured as the event's health entries and never marked as remote deaths,
and the host rolled a second drop set for a death the reporting member already rolled.

`ChainedBlastReplay` now fires those mines inside the same scope:

- `TrapBuildingHealthScope` records every entity the host's application damaged (`Damaged`), in the order it happened;
- after `ApplyEvent`, each damaged mine that has not fired and is below the line is marked FIRED through `Traverse`
  (the flag is private in the game's code) and exploded with the game's own `WorldGeneration.CreateExplosion` at the
  mine's position plus up — the same call and the same position the native path uses;
- because the scope is still open, every chain victim lands in it through the ordinary health seam, which is also the
  place slice 1 marks victims as remote deaths, so the member's own roll stays the world's only set;
- the iteration is over a snapshot: a chain of chains leaves the next link to its own destroy, which still explodes it
  (the shape this game's damage numbers make rare), and the log line says how many blasts were replayed.

The rule is `ChainedBlastPolicy` (five cases: below the line, exactly on it, already fired, not a mine, still healthy).

## The acceptance batch was attempted and this world cannot stage it (2026-10-24, `0.1.0+8b5baee6`)

A two-client session was formed and the host ran the census the family already owns
(`trap-drop-census`, read-only, over the whole layer):

| read | value |
|---|---|
| live `BuildingEntity` objects | 1923 |
| destructive entity-event carriers (mine / turret / fragile crystal / unstable crystal) | **0** |

So this generated layer carries none of the four entities a member could report an `EntityEventMsg` for, and
`trap-event-forge` (the product's own sender, the lever that publishes such a report without a gesture) would be
refused by the judge's empty-bounds arm rather than reaching the table/mark this ticket fixed. **The rows need a layer
that generated a carrier**, and there are two ways to get one:

1. descend until one appears (`trap-drop-census` is the cheap repeatable check, one call per layer), or
2. stage one: the world-generation containers/items the earlier scan found (269 items, 6 containers) include the
   droppable kinds, but no recipe places a MINE; the `trap-event-forge` lever needs the carrier to exist first.

Nothing about the code changes: slices 1 and 2 are deployed and unit-pinned; this is a staging fact, recorded so the
next batch does not repeat the attempt blind.

## The staging blocker is SOLVED (2026-10-24): `trap-stage` places the carrier

Route 2 above now exists as a committed recipe. `trap-stage` instantiates the GAME'S OWN prefabs — `Resources.Load("landmine")`
and `Resources.Load("containercrate")` — at the host body's own position plus a horizontal `dx`, and the census then finds
the mine as a live carrier:

| read | value |
|---|---|
| `trap-stage` on the host at (0,0), dx=3 | `ok:true`, mine at (0,0.5), crate at (3,0.5) |
| `trap-drop-census` radius 10 | `total` 1845 buildings, `listed` **1**: `MineExploded` at (0,0.5), prefab `landmine`, health 100, **`anchorOwn:true`**, `rowCount` 1 |
| the same census after the forge attempt | still 1 carrier — the staged mine survives the session |

**What the row still needs, precisely**: `trap-event-forge` refuses `id=<anything>` when no world item on the member's
client carries that id (`no-world-item`), so the member's claim must name the REAL dropped item — which means the run has
to let the blast happen first and then read both worlds' item sets (`item-world-read` on each client, comparing instance
ids). The staging makes that reachable; the trigger is the remaining question, and it has two shapes worth trying next:

1. the native one — the member's body walks onto the mine (the game's own trigger), or
2. `WorldGeneration.CreateExplosion` at the mine's position from the member's client, which is the member-side shape the
   host's applier replays for a report.

A recipe that presses neither is the lesson this batch adds: a forge that has to name an id cannot be the FIRST step of a
drop row.

## Batch 20261024-r: a member-pressed mine really reaches the host, and its victims are marked

The `trap-trigger` recipe presses the nearest mine by setting `MineScript.pressed` — the field a player's step sets — so the
game runs its own 0.8 s sequence, the member's client reports the `MineExploded` event through the ordinary patch, and the
host applies it. Two blasts were read on the machine
(`docs/evidence/acceptance/blast-marked-victims-20261024-r.md`):

| evidence | first blast | second blast |
|---|---|---|
| the member's report | `origin=Report` at (0, 0.5) | `origin=Report` at (0, 30.5) |
| the host applied it | `host applied mine explosion at (0.00, 0.50)` | same at `(0.00, 30.50)` |
| **the scope's own line** | **`marked 1 blast victim(s) as remote deaths`** | **`marked 2 blast victim(s)`** |

So slice 1 works on a live member-triggered event: every entity the host's application damaged is marked before the game's
death branch could roll it — the duplicate's cause, removed.

**Still owed, and narrower now**: the "EXACTLY ONE set" row needs a victim whose drop table is NOT empty. Both staged
crates (`containercrate`, `medcrate`) left zero items on both clients — a hand-placed container carries loot, not
`itemsDropOnDestroy` rows. The census already names a victim whose table IS non-empty: **the mine itself** (`rowCount` 1),
so the next batch stages two mines side by side (one fires, the other dies) and counts the second one's set on both
clients; or a worldgen-placed container instead of a hand-placed one.
