# Batch 20261024-u — the count row is read: ONE set of a turret's drops, identical on both clients

- Ticket: `docs/backlog/review/host-blast-rolls-a-second-drop-set.md`
- Artifact: `0.1.0+19aeb2f5`, deployed and verified against `<game-dir>` before the run
- Session: host + one member, one launch. Two descents reached a layer whose census lists the same
  `TurretSelfDestructed` carriers on both clients.

## The staging that finally worked

`building-health-set` writes a building's health (the state a player's damage leaves it in) and the caller chooses the
value. That choice is the whole trick: a turret below **350** starts its OWN two-second countdown
(`TurretScript`), which ends in `WorldGeneration.CreateExplosion` — and therefore fires the carrier event and rolls that
carrier's drops natively. Writing **0** instead skips the countdown (the entity dies silently), which is why the previous
batch's kill produced standalone item spawns and no carrier event.

## What the run read

| side | evidence |
|---|---|
| member | `[TrapDropPending] kind=TurretSelfDestructed pos=(-373.0,-90.7) held for drop collection` → `[TrapDropFlush] … drops=4` — its own roll of the turret's four-row table |
| host | `[TrapEvent] kind=TurretSelfDestructed pos=(-373.0,-90.7) origin=HostApply from <member>`, then four `[TrapDropPresentation] … enriched with initial drop state` lines for `scrapmetal` (13787924117, 18082891413, 22377858709) and `circuitboard` (26672826005) — the member's OWN ids materialised, not a re-roll |
| host, absence | no `[ItemSpawned]` line for that death: the host rolled nothing |
| both worlds, after | **290 world items each, and exactly the same four ids present on both** (`item-world-read`, radius 2000) |

So the ticket's first row passes: a member-triggered carrier death leaves EXACTLY ONE set of that building's drops, the
same instance ids on every client. Its third row passes too: the host's own `[ItemSpawned]` line appears for a blast victim
**zero times** when the member's report is the owner.

## What is still owed, and why

The chain row (`a mine whose blast kills another mine`) is unread: the generated layers this session reached carry
**turrets only** (four more descents: 8 turrets, 8 turrets, then two empty layers, and no `MineExploded` carrier at all),
and a mine staged by hand exists only on the host's client (`WorldBuildingEntitySync` forwards damage/open/health for known
entities, never a new one's existence). The chain shape therefore needs either a generated layer with mines or a
generation-time placement; the code for it (slice 2, `ChainedBlastReplay`) is landed and unit-pinned.
