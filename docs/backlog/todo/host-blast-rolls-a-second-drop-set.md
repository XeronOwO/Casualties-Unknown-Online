# The host rolls a second drop set for a death a member already rolled

- Status: Todo — opened 2026-10-18 by the audit inside the entity-event drop bound (the
  `NetMsg.EntityEvent` row of `docs/backlog/todo/member-report-bounds.md`).
- Priority: Medium
- Category: Sync / item ownership
- Source: the bounds cycle's family audit — the same audit that gave a member's reported event drops a bound
  found that the host's OWN application of the event still rolls a set for the same death.
- Related: `docs/backlog/todo/member-report-bounds.md` (the bound that admits the member's report),
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
