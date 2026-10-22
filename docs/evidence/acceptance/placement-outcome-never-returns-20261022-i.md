# Batch 20261022-i — placement-outcome-never-returns row 6 (the residual's race)

- Attempted row: row 6 only. Artifact: the same deployment as batch `20261022-h` (`0.1.0+fb782e5c…`); no `src/`
  change since that commit, so the deployment still names it.
- Run: host = giver (physical machine), guest = receiver (first sandbox), one world. The user authorised the
  pointer/keyboard takeover for these batches (2026-10-22: "接下来的鼠标键盘你随时可以接管").
- Scope: `20261022-i-scope.md`.

## What was attempted

Two staging designs, both with real pointer input:

1. **The receiver holds its own local move across the giver's release.** The receiver right-drags one of its own
   ring items and holds it over the free slot; the giver releases its give aimed at that slot; the receiver
   releases the held move, which must land after the answer and before the transfer applies.
2. **The receiver's pickup click inside the window.** A ground item at the receiver's feet, a burst of pickup
   clicks spanning the giver's release.

## What the attempt established

- **The shared pointer makes design 1 impossible.** Both gestures need the RIGHT button held on their own
  client. While the receiver held its right button down, the giver's right-press was a no-op at the OS level
  (the button was already down); the giver's ring never started a drag (`dragItem=none`), while the receiver's
  drag stayed live. The two holds cannot coexist on one physical button.
- **The foreground is single-owner and the switch is slower than the window.** Both clients' windows are
  side by side; only one can be the foreground for real input. `SetForegroundWindow` was refused until the
  batch-local helper pressed ALT around it, and the switch and the two calls around it cost more than the
  answer→transfer window measured in batch `20261022-h` (~540 ms, the answer at 23:49:53.812 and the apply at
  23:49:54.353). A gesture on the receiver cannot be aimed inside that window after a gesture on the giver.
- **No return ran.** Neither the receiver's report line nor the host's `[Placement] returned …` line appeared in
  this session.
- The attempt left the receiver with a bandage on the ground (its own right-drag ended with a release outside the
  ring) and its ring state unreliable; the run was closed there rather than re-staged, because the two
  constraints above do not change with another try.

## Verdict

**UNPROVEN**, and now with a mechanical reason rather than a timing guess: with one physical pointer and one
foreground window per client, the receiver's own body change cannot be made inside the answer→transfer window.
A future reading needs either a second independent pointer device, or a designed seam whose evidence the
acceptance rules accept for this row — that is a user ruling. The row stays `UNPROVEN` and the ticket stays in
`todo/`.

## Limits

- Nothing else was read in this batch; rows 1-5 remain the batch `20261022-h` readings.
- The clients were closed at the end of the attempt.
