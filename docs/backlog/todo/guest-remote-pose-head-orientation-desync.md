# Guest water-current pose/height desync on host view

- Status: Todo
- Priority: Medium
- Category: Player presentation / sync root cause
- Source: User report (2026-09-05) — the guest lies in water and is pushed by the current; the host's view of the guest differs in pose from the guest's own view, and the guest appears to clip underground on the host view for a while.

## Observed symptom

- The guest is lying in water and being carried/pushed by the water current.
- The host sees a different pose than the guest's own client.
- On the host view, the guest visibly clips into/below the ground for a period of time.

## Assessment

Likely a presentation/synchronization mismatch between the guest's locally
simulated water/lying pose and the host-side remote clone. The underground
clipping is a visible symptom that the host's remote presentation is reading a
stale, offset, or incompatible pose/height fact rather than the guest's actual
simulated water state.

## Investigation direction

- Determine which remote pose data drives the host's clone in this scenario:
  player state stream flags, per-limb world pose, body-root height, sitting/lying
  pose, or a separate water-current pose path.
- Compare the guest's local simulation path (water current, body root, lying
  pose) with the host's clone projection for the same interval.
- Identify why the host-side clone diverges in pose/height and clips below
  ground: missing/incorrect state facts, stale stream values, interpolation or
  correction timing, or a pose anchor mismatch.
- Check whether the issue belongs to the existing player state stream / remote
  clone pose family and whether the same water-current scenario should be
  covered by the same root-cause fix.

## Non-goals

- Not adding a cosmetic host-side "push player up" correction that hides an
  underlying pose/height desync.
- Not treating the guest-local pose as automatically wrong; the host clone must
  reproduce the guest's simulated water pose from the correct synced facts.
