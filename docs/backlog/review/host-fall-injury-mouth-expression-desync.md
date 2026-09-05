# Host fall injury mouth-expression desync

- Status: Review (code-complete)
- Priority: Medium
- Category: Player presentation / sync root cause
- Source: User report (2026-09-05) — host falls and is injured; on the guest's view the host's mouth opens, but on the host's own view it does not.

## Observed symptom

- User controls the host and causes a fall/injury.
- After the injury, the guest sees the host's mouth open.
- The host's own view does not show the mouth open.

## Assessment

This is likely a surface symptom of a deeper synchronization mismatch, not a
one-off visual patch for the mouth. The open mouth probably comes from one of
the remote-clone presentation paths reading a synced state that disagrees with
the owner's local state (or a local event that was reported/rendered on the
peer but not applied on the owner).

## Investigation direction

- Identify which presentation mechanism drives the remote host's mouth open:
  facial-expression vitals, pain/voice one-shot, ragdoll/fall pose, or a
  separate injury-expression path.
- Determine whether the remote expression is derived from:
  - a synced physiological fact (pain, shock, consciousness, injury) that
    differs from the owner's local simulation, or
  - a dedicated event/sound/pose that is routed to peers but not replayed on
    the owner, or
  - a stale/incorrect remote state that was not corrected after the fall.
- Compare the host-local path and the remote-clone path for the same fall
  injury; find where the two views diverge at the source rather than patching
  the guest-visible mouth directly.
- Check whether this is related to the existing pain vocalization/face-vitals
  sync tickets and to the host-authoritative physiological sync surface.

## Root cause (this cycle)

The remote clone's head/mouth sprite is derived on the receiving side from
clone-local proxy inputs (`Body.HoldingItem(2)`, `limbs[0].dislocated`, and
the zeroed inheritance of `eatTime`). After a fall injury, those inputs can
disagree with the owner's own `FacialExpression` visual, producing the
remote-only open mouth. The 1 Hz character snapshot now carries the owner's
actual mouth decision (`HeadMouthState`), and a remote-clone postfix restores
that exact head sprite after the game's own `FacialExpression.Update` has run.
No gameplay or authority state changed.

Selfcheck: `docs/evidence/selfchecks/presentation/remote-clone-head-mouth-sync-selfcheck.md`.

## Non-goals

- Not adding a cosmetic "force remote mouth open" patch that hides the
  underlying state mismatch.
- Not treating the remote-only appearance as acceptable if the owner's local
  state is the source of truth for the same event.
