# Host severe sleepiness posture not synced to guest

- Status: Review
- Priority: High
- Category: Body pose sync / remote presentation (systemic)
- Source: User report (2026-09-05) — when the host is severely sleepy, the host's body cannot stand straight (posture is visibly slouched/bent), but the guest's view shows the host standing straight.

## Landed (2026-09-05)

The systemic root cause was not a missing one-off slouch patch: the owner's
HandleVisuals feeds `max(crouchAmount, 1 - legSpeedMult)` into the CrouchAmount
animator parameter, and the frozen render proxy could not compute
`legSpeedMult` (it is a get-only property over limb physics). The 1 Hz
character snapshot now carries the owner's computed `legSpeedMult`
(CharacterHealthMsg ProtoMember 80); the proxy stores it on
`RemoteBodyDriver.LegSpeedMult` and reconstructs the same CrouchAmount input
through the pure `BodyPosePresentation.ProxyCrouchInput` rule. This covers the
whole weakness/slouch family (severe sleepiness, low consciousness/stamina,
hunger, etc.) through one pose input rather than a per-symptom patch.

Selfcheck: `docs/evidence/selfchecks/presentation/remote-clone-legspeed-pose-selfcheck.md`.

- [ ] Awaiting final unified acceptance.

## Observed symptom

- Host has severe sleepiness.
- Host's own view: the body is not standing straight; the posture reflects the
  sleepiness/weakness state.
- Guest's view: the host appears to be standing straight, losing the actual
  postural state.

## Assessment

This is not an isolated cosmetic issue. It is another symptom of the same
body-pose synchronization family that has repeatedly failed on the remote view
(head orientation, mouth expression, carry/riding posture, water/lying pose,
fall injury posture, and now sleepiness posture). The current approach of
patching individual presentation gaps is not enough.

This ticket requires a careful systemic analysis of the whole body-pose sync
path and likely a rewrite/unified model rather than another local patch.

## Investigation / rewrite direction

1. **Inventory all owner-side pose/body state that affects remote appearance**:
   - standing/sleeping/crouch/lying/ragdoll flags;
   - sleepiness/fatigue-driven posture and any body bend/slouch;
   - head/neck/eye/mouth expression state;
   - limb pose, body-root anchor, carry/riding placement;
   - water/current interaction and other environment-driven pose changes.
2. **Map every pose/state source to what is currently sent and what the remote
   clone reconstructs**:
   - find where the owner's actual posture is lost before/when it enters the
     player stream/entity state;
   - find where the remote clone uses defaults or simplified pose instead of the
     owner's true posture.
3. **Design a unified body-pose projection** if the audit shows the current
   piecemeal paths cannot be made correct:
   - one owner→peer representation for body posture/pose state;
   - one peer-side projection/application path used by all views (owner,
     participant, third-party);
   - no per-feature Harmony patch that only fixes one visual gap.
4. **Preserve the existing architecture constraints**:
   - each player simulates only its own body;
   - no host/remote simulation of another player's per-frame body behavior;
   - semantic state before raw Transform where possible, but with enough pose
     fidelity to reproduce the actual owner posture.
5. **Cover the whole family, both directions, and third-party views** for every
   pose state, not just the reported sleepiness case.

## Related backlog

- `todo/guest-remote-pose-head-orientation-desync`
- `todo/host-fall-injury-mouth-expression-desync`
- `review/carry-piggyback-rider-position-smoothing`
- `review/carrier-sit-while-carrying`
- `todo/guest-water-current-pose/height variant` (now within guest remote pose head-orientation desync)

## Non-goals

- Not treating this as a one-off "make the remote clone slouch" patch.
- Not accepting local/remote posture divergence in any remaining pose family.
- Not stopping with "the normalized pose looks close enough"; the remote view
  must match the owner's actual posture.
