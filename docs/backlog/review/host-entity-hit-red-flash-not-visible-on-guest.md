# Host entity hit red flash not visible on guest

- Status: Review
- Priority: Medium
- Category: Enemy/entity presentation sync
- Source: User report (2026-09-05) — when the host attacks an entity, the entity has a red flashing/hit effect locally, but the guest cannot see it.

## Landed (2026-09-05)

The melee red HitFlash now rides the existing `BuildingEntityDamaged` relay as a
presentation-only flag:

- `BuildingEntityDamagedMsg` gains `PlayHitFlash` (protobuf member 4);
  `ProtocolVersion.Current` 7 → 8 (behavioral wire extension, mixed-version
  sessions rejected by handshake).
- `BodyPatches.BodyAttackPatch` sets `playHitFlash: true` only for the native
  `Body.Attack` melee hit; explosion, silent cactus self-damage and
  item-vs-enemy damage keep it false because those paths do not spawn the red
  flash locally.
- `WorldBuildingEntitySync.OnRemoteBuildingEntityDamaged` replays the exact
  native red flash through `WorldGeneration.CreateHitFlash` with the local
  deterministic copy's sprite/position/rotation and `Color.red`. The replay is
  presentation-only and does not mutate health, authority, drops or CUO state.
- Because the existing star relay already covers host → guest, guest → host and
  third-party views, every non-attacker view receives the same flag by the same
  remote-apply path.
- Evidence:
  `docs/evidence/selfchecks/presentation/building-entity-hit-flash-sync-selfcheck.md`.

## Problem

On the host's own client, attacking an entity produces a visible red flash
(typically the game's hit/damage feedback on the entity). On the guest client
the same attack does not show that red flash; the entity appears to take the
hit without the presentation feedback.

## Goal

Make the host-triggered entity hit/damage red-flash presentation visible to the
guest (and, by the same family rule, to every remote/third-party view), without
re-running the host's hit simulation on the guest:

- Identify the exact native mechanism that produces the red flash on the host
  (entity/health/damage component or effect) and where it is gated or not
  replicated.
- Use the existing dedicated enemy/entity effect or event channel where one
  exists, instead of adding a full snapshot/prediction path.
- Cover all directions and roles: host → guest, guest → host, and third-party
  views.
- Ensure the effect is presentation-only; it must not mutate authoritative
  entity state or re-apply damage on the guest.
- Add regression/runtime evidence for the exact user reproduction before moving
  to `review/`.

## Acceptance criteria

- A guest watching the host attack an entity sees the same red flash / hit
  feedback as the host.
- The reverse direction (guest attacks entity, host/third-party sees the flash)
  is also covered or explicitly recorded.
- The fix reuses the game's existing native hit/flash presentation and existing
  CUO effect/event channel; no duplicate presentation path is introduced.
- Full build, tests, architecture/event/entity gates pass; deployed artifact
  verified before `review/`.

## Non-goals

- Not re-simulating the host's hit/damage on the guest.
- Not using raw Transform/state snapshots for transient presentation effects.
- Not changing enemy/entity damage authority or arbitration.
