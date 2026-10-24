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

**Still owed**: slice 2 (the chain blast replayed inside the scope instead of `MineScript.OnDestroy` running it
natively outside) and slice 3 (the host-authored report channel for a death only the host's roll produced), plus the
ticket's three acceptance rows, which need two clients and a mine placed next to a droppable building.

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

