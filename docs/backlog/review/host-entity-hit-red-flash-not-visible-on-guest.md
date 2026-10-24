# Host entity hit red flash not visible on guest

- Status: Review — batch `20261025-g` could NOT read the presentation row yet, and the missing step is named below rather than
  guessed at.
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

## Batch `20261025-g` — what the reading needs and what stopped it

The reading is the guest's Debug line `[BuildingHitFlash] replayed red hit flash at (…)`, which the replay writes when the
relay reaches it. This batch built the approach up to the swing and stopped there:

- New probe `tools/acceptance/recipes/entity-attack.cs` finds the nearest live `BuildingEntity` and can stand the body beside
  it: on the deployed build the host's nearest entity was at `(0, 510)`, 3.38 units away, and `mode=place` moved the body to
  `(0.6, 510)`. A probe cannot fabricate `Body.Attack(AttackInfo)` — the game struct is the swing — so the attack itself must
  be the PLAYER's click, which is the path this batch then took.
- `screen-project` put that entity at screen `(608, 688)` on a 720-high screen, and the real click at that point produced **no
  hit on either side**: the guest log (level set to Debug first) carries no flash line, and the host log carries no damage
  line. The point sits at the very bottom edge, inside the HUD strip, so the click most likely never reached the entity — a
  wrong-point failure, not a failed relay.
- **The missing step, named**: a click that provably lands on the entity — the entity's own `SpriteRenderer` bounds projected to
  the screen, or a camera that puts it away from the HUD edge — followed by the host's own hit confirmation (a damage line at
  Debug). Only then does the guest's replay line mean what this ticket wants it to mean. The ticket stays in `review/`.

Follow-up in the same batch found **the real reason a click cannot reach the world in this state**: the CUO window was still
OPEN on the host (the menu work before this batch left it up), and the overlay's modal swallows the world's input — the frame at
that moment shows the window covering the screen. After `Esc` closed it the world is clickable again, so any click-based reading
must start from "the CUO window is closed", which is the same fact `docs/acceptance/workflow.md` records for the drag batch. The
recipe now also stands the body SOUTH of the entity (`entityPosition.y - 2.5`) so the target draws above the player rather than
under the HUD strip, and the next attempt starts from the closed window with the entity's own sprite bounds as the click point.

The retry with the window closed still produced no hit, and the host log says WHY rather than leaving it to guesswork: the click
produced `[ItemTrace] op=1 item=0 origin=OnBlockDamaged`, i.e. it damaged a BLOCK, not the `BuildingEntity` the probe picked. That
entity (the nearest at `(0, 510)`) has no clickable collider at the projected point — most likely it sits behind or inside the
room's own blocks. The next attempt must therefore target an entity whose collider the click can actually reach (a census of
entities WITH their screen bounds and a `collider != null` filter), not merely the nearest one by distance. Recorded here so the
next cycle does not re-run the same click.

The probe now answers that question directly instead of leaving it to another blind click: `mode=census` lists the entities whose
screen point is inside the view and which carry a `Collider2D`, and `mode=pick` reports what `Physics2D.OverlapPointAll` finds at
the chosen entity's own point. On this session `pick` answered **`lifepodheater` / `CircleCollider2D`** at `(0, 510)` — the point
is occupied by the entity itself, so the projection is not the problem; the swing at that point still produced a block-damage
entry rather than entity damage, which is the fact the next attempt has to explain (the heater may not be a melee damage target
inside the pod, and the click-time swing may break the pod floor instead). The reading therefore needs an entity the melee path
actually damages — a wall/rock-style `BuildingEntity` outside the pod — placed in view and clicked, with the host's own damage
line as the confirmation before the guest's replay line is read.

A `prefab` filter was added for exactly that (`entity-attack mode=place,prefab=leadbush`) and it closed the question by
elimination: the nearest `leadbush` on this world is **61 units from the pod**, at `(-31.5, 453.6)`, and its screen point is
`(135.8, -213.8)` — off the bottom of the viewport. Teleporting the body to it does not stick: the next read shows the body back
at `(0, 506.6)`, because the game's own movement/collision puts it back out of solid rock. So inside the pod there is no melee
target to hit at all, and reaching one means real travel rather than a placement. **This row is parked** until a batch can walk a
member to a plant/tree and swing there (the `move-drive` recipe is the natural instrument once its argument contract is
satisfied) — a capability question, not a defect in the replay path, which the ticket's own selfcheck already covers.
