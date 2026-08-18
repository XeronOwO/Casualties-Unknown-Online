# Cactus self-damage sync (silent BuildingEntityDamaged)

Date: 2026-08-18
ProtocolVersion: 21 (`BuildingEntityDamagedMsg.PlayHitSound` — a v20 peer would
replay the entity hitSound for silent cactus self-damage, so the handshake
refuses cross-version mixing)

## Problem

A body bumping a cactus runs the native `CactusScript.OnCollisionEnter2D`:

- the player gets the local knockback/gore sound and limb effects, and
- the cactus itself takes `base.GetComponent<BuildingEntity>().health -= 30f`
  self-damage.

CUO already relayed the gore sound through the `CactusHit` entity event, but
the cactus self-damage stayed local. After enough bumps the trigger side's
cactus could be destroyed while peers still saw it alive — a recorded
presentation/state gap in `docs/backlog.md` ("cactus self-damage HP local").

## Change

`TrapCactusPatch` now reports the self-damage through the existing
`BuildingEntityDamaged` star channel as a **silent** damage report:

- New `BuildingEntityDamagedMsg.PlayHitSound` field (protobuf member 3,
  default false). Attack/explosion damage sends `true` so the receiver keeps
  replaying the entity's own `hitSound`; cactus collision self-damage sends
  `false` because the trigger side never plays the entity hitSound — only the
  player-local `DoGoreSound`.
- `TrapCactusPatch.Postfix` still reports `CactusHit` for the gore-sound replay
  and additionally calls `OnBuildingEntityDamaged(entity, 30f, playHitSound: false)`.
- `WorldEventSync` applies the silent damage through the normal remote-damage
  path: peers subtract the health, mark a death as `RemoteEntityDeath` when the
  cactus reaches < 0.5 health, and record it in the building-entity health
  snapshot for late joiners.
- Remote clone bodies (`RemoteBodyDriver`) are excluded from the trigger so a
  render clone's collision can never report a duplicate.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| `CactusScript.OnCollisionEnter2D` | Native self-damage (`health -= 30f`) stays the single source of truth on the trigger side | Decompiled `CactusScript.cs` (Assembly-CSharp.dll): `base.GetComponent<BuildingEntity>().health -= 30f` |
| `TrapCactusPatch` | Reports `CactusHit` sound + silent `BuildingEntityDamaged` (30, playHitSound=false); skips `RemoteBodyDriver` clones | `TrapCactusPatch.cs` |
| `BuildingEntityDamagedMsg` | New `PlayHitSound` field, default false; attack sends explicit true | `BuildingEntityDamagedMsg.cs`, `WorldService.SendBuildingEntityDamaged` |
| `WorldEventSync` relay | Applies damage; plays `entity.hitSound` only when `playHitSound=true`; marks `RemoteEntityDeath` on death and records health for late joiners | `WorldEventSync.OnRemoteBuildingEntityDamaged` |
| `CactusHit` replay | Unchanged — gore sound only | `TrapStateActions.ApplyCactus` |
| Late joiner | Damaged cactus health travels in the existing `BuildingEntityHealthSnapshot` (recorded by the host on every applied damage) | `WorldService.ReportBuildingEntityHealth`, `BuildingEntityHealthRegistry` |

## Why this is safe

- The existing `BuildingEntityDamaged` channel already handles the "remote
  entity damage cannot roll local drops" rule by marking `RemoteEntityDeath`;
  cactus damage reuses that semantics.
- The silent flag is a per-message property, so attack/explosion damage keeps
  its exact current sound behavior.
- No new mechanism, no new message id, no per-receiver component guessing: the
  source decides whether the entity hitSound belongs to the event.

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- L0 simulation:
  `WorldEventRelayTests.BuildingDamaged_SilentDamageFlag_RidesThroughRelay`
  asserts `playHitSound=false` survives the guest → host → other-guest star
  relay; the existing attack tests assert the default `true` path is unchanged.
- Full suite: 982 tests green.
- Static evidence: `TrapCactusPatch.cs`, `WorldEventSync.cs`,
  `BuildingEntityDamagedMsg.cs`, `WorldService.cs`, decompiled
  `CactusScript.OnCollisionEnter2D`.
- Development-period rule: L0/static evidence; **no manual acceptance**
  (user 2026-08-16 mandate).