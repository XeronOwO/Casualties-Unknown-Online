# Host entity hit red flash not visible on guest

- Status: Todo
- Priority: Medium
- Category: Enemy/entity presentation sync
- Source: User report (2026-09-05) — when the host attacks an entity, the entity has a red flashing/hit effect locally, but the guest cannot see it.

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
