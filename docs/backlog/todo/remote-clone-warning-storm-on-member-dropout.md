# A member out of the world writes one Warning per clone per frame, unbounded

- Status: Todo
- Priority: Medium
- Category: Observability / log volume (the log-storm family)
- Source: acceptance batch `20261007-a` (2026-10-07), the row `todo/layer-change-member-dropout.md`
  scheduled for that batch. Filed rather than fixed because an acceptance run changes no code
  (`docs/acceptance/AGENTS.md` rule 8, and that ticket's own rejection).
- Related: `todo/layer-change-member-dropout.md` (the batch's ticket; this is the third producer of the
  family that cycle bounded), `todo/layer-change-member-recovery.md` (the same session's attribution: the
  member is out of the world with no local body, which is the state this line repeats in),
  `review/steam-transport-send-limit-runaway.md` (the sibling unbounded-warning family),
  `docs/evidence/acceptance/layer-change-member-dropout-20261007-a.md` (this reading),
  `docs/evidence/selfchecks/items/layer-change-warning-storm-selfcheck.md` (the shape the cycle bounded)

## Symptom (read from batch `20261007-a`, three clients, one lobby)

After a `skiplayer` layer change the third client left the world (`inWorld: false`, `page: Home`) and did
not come back inside 60 s: `container-read mode=local` answered `no-local-body` at +40 s. Its own rolling
log then grew without bound, one line per clone per frame:

```text
[WRN] […GameAdapter.Character.RemotePlayerRenderer] Remote body: no Body component in "Experiment" clone.
```

Measured in that window:

| Reading | Value |
|---|---|
| Lines in ~90 s | 58,960, of which **58,148** are this line |
| Bytes in ~90 s | 8.796 MB |
| Steady rate (a further 10-second sample) | **1.212 MB / 10 s = 7.25 MB / min**, 639–866 lines/s |
| Still climbing at close | yes — the census taken after the clients closed (`ev-alt-storm-census.txt`) reads **82,618** of these lines and **12.442 MB** for that client since the mark |
| For comparison | the storm this family bounded grew at ~8.2 MB / min before the fix |

The same shape appears whenever a member is out of the world: the condition a clone has no `Body` never
resolves for as long as the member stays out, so the warning repeats every frame rather than firing once.
`LogVolumeGateTests`' census does not name this producer, which is why it survived the cycle that bounded
its two siblings — the two it does name read 14 and 5 lines in the same run, and the divergence warning
this ticket's sibling is about read 0–3.

## Required work

1. **Bound it by the mechanism already in the tree, not by a new one.** The family's answer is
   `LogRepetitionGuard` with one window per SUBJECT: the subject here is the clone (or the member whose
   clone it is), so an unchanged fact costs a bounded window of lines and the window's end reports what it
   swallowed, while a clone that resolves reports again. A drop to Verbose/Debug with one Warning at the
   state change would also be bounded, but it loses the first line of a real defect; decide by the same
   standard the cycle applied to its siblings (the trigger frequency chooses the level, and a repeatable
   diagnostic goes behind a window).
2. **Extend the census so the next producer of this family is loud.** `LogVolumeGateTests` pins the sites
   the earlier cycle read; this one arrived unnamed. Add this producer (and the gate's census floor) in the
   same change, so a renamed or additional per-frame line fails the gate instead of filling a log.
3. **Keep the detector's information.** The line is the only signal that a remote clone has no body; it is
   the diagnostic, not noise, so it is bounded and never removed.
4. **Read the storm's own cost as a size, not as a line count**, when the row is judged: the batch measured
   the file at 8.796 MB in ~90 s and still climbing, and the client's log is what a player's disk pays for.

## Non-goals

- Re-attributing the member's exit from the world: that reading is
  `todo/layer-change-member-recovery.md`'s, and this ticket only owns the volume its state produces.
- Touching the wire or a save shape: this is a log-level change on one producer.
