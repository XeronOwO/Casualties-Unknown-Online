# A member out of the world writes one Warning per clone per frame, unbounded

- Status: Done (batch `20261007-c`, 2026-10-07: the runtime row PASSES — with both members driven out of the
  world, each client writes three lines per (member, failure) subject plus one summary per run that ended
  (8 lines for two subjects) and grows at ~0.40 MB/min against this ticket's 7.25 MB/min reading, while the
  parked attempt that holds both members in the world reads 0 of the line. History: the code and the gates
  landed 2026-10-07 in the cycle above, and batch `20261007-a` measured the unbounded shape.)
- Priority: Medium
- Category: Observability / log volume (the log-storm family)
- Source: acceptance batch `20261007-a` (2026-10-07), the row `done/layer-change-member-dropout.md`
  scheduled for that batch. Filed rather than fixed because an acceptance run changes no code
  (`docs/acceptance/AGENTS.md` rule 8, and that ticket's own rejection).
- Related: `done/layer-change-member-dropout.md` (the batch's ticket; this is the third producer of the
  family that cycle bounded), `todo/layer-change-member-recovery.md` (the same session's attribution: the
  member is out of the world with no local body, which is the state this line repeats in),
  `review/steam-transport-send-limit-runaway.md` (the sibling unbounded-warning family),
  `docs/evidence/acceptance/layer-change-member-dropout-20261007-a.md` (this reading),
  `docs/evidence/selfchecks/items/remote-clone-warning-storm-selfcheck.md` (this cycle's fact sheet),
  `docs/evidence/selfchecks/items/layer-change-warning-storm-selfcheck.md` (the shape the cycle bounded)
- Acceptance record: `docs/evidence/acceptance/remote-clone-warning-storm-on-member-dropout-20261007-c.md`

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

## Landed (2026-10-07)

Both failure lines are bounded and the census names them; the runtime row is the next batch's. Fact sheet:
`docs/evidence/selfchecks/items/remote-clone-warning-storm-selfcheck.md`.

- **The line is bounded by the mechanism already in the tree.** Both failure branches of
  `RemoteBodyFactory.CreateRemoteBody` (`"Experiment" player object not found in scene`, `no Body component in
  "Experiment" clone`) now ask a `LogRepetitionGuard` and write their Warning INSIDE that ask, so one unchanged
  failure costs three lines per subject instead of one line per frame per member. The subject is a new
  `RemoteCloneFailureKey` — the failure's own wording AND the member — because a scene that has no template and
  a template whose clone carries no Body are different facts, and the second must be news rather than a repeat
  of the first. The level stays Warning and the line is not removed: the diagnostic is kept, only its
  repetition is bounded.
- **The line names the member it is about**, which the storm's own log could not: it named none, so that log
  cannot say which member — or how many — it was reporting, and the next batch's census can separate them.
- **Every run is drained where it ends, and a run that outlives the session is drained too.** The renderer
  reports what a window swallowed when a clone is built again (`Update`), when the member leaves the world
  (`OnRemoteSceneChanged`, which also re-arms the window so a failure that returns reports its first line) and
  when the session ends (`DestroyAllClones`). That last one walks the guard's new `Subjects` — a bounded copy of
  the subjects it is still holding — which exists because the storm's own run never ended any other way: the
  wind-down is the only path that could have told its size.
- **The census names the producer.** `LogVolumeGateTests` gains a fact for the two failure lines (the log must
  SIT inside the ask; the containment matcher is new, with positive and negative samples, one of them "the ask
  is there and the log is outside it"), a fact for the drain chain, a renderer census floor and two more site
  floors — five in total.
- **Red first, then four mutations.** The new ask fact was run against the unmodified tree and reported exactly
  one failure, naming this producer and the 7.25 MB/min reading (the new matcher's positive sample also caught
  a real defect in the matcher — `DescendantNodes` cannot see a condition that IS the call — fixed before the
  implementation). On the fixed tree, four mutations each turned exactly the expected case(s) red: the line
  moved outside its ask, the ask deleted, the session-end drain dropped, and `Subjects` returned as the live
  list instead of a copy (1, 1, 1 and 2 cases). Every mutated file was restored byte-identically, SHA-256
  compared.
- **No wire member, no save shape and no protocol number** changed; the detector is untouched.

## Required work

Status of each item after the 2026-10-07 cycle: all four are done — items 1-3 in the cycle's own change
(see *Landed*) and item 4 by batch `20261007-c`, which read the runtime row.

1. **Bound it by the mechanism already in the tree, not by a new one.** — **done**: both failure lines ask a
   `LogRepetitionGuard` keyed on (failure, member) and are written inside the ask; the level policy's answer
   (the trigger frequency chooses the level, a repeatable diagnostic goes behind a window) is unchanged, so the
   first line of a real defect is still there.
2. **Extend the census so the next producer of this family is loud.** — **done**: this producer is named in
   `LogVolumeGateTests` with its own census floor (five floors over five files), and a renamed or deleted ask
   fails the gate instead of filling a log.
3. **Keep the detector's information.** — **done**: the first three lines carry the whole message plus the
   window's index, the member is named, and each ended run reports how many identical lines its window refused.
4. **Read the storm's own cost as a size, not as a line count**, when the row is judged. — **done, read by
   batch `20261007-c`**: the re-run of `layer-change-member-dropout`'s fixture drove the change with no park,
   which put both members out of the world for ~8 s, and the census since the mark read 8 lines per client
   (two (member, failure) subjects, each three lines plus one summary) against the 58,148 lines of batch
   `20261007-a`, with the summaries reporting 156 / 207 (guest) and 108 / 221 (alt) refused lines as the
   volume the window swallowed. The same batch's parked attempt reads 0 of the line on all three clients.
   Record: `docs/evidence/acceptance/remote-clone-warning-storm-on-member-dropout-20261007-c.md`.

## Non-goals

- Re-attributing the member's exit from the world: that reading is
  `todo/layer-change-member-recovery.md`'s, and this ticket only owns the volume its state produces.
- Touching the wire or a save shape: this is a log-level change on one producer.
- Bounding the attempt itself: each failed attempt still clones and destroys a full character proxy (the
  template's `Body` is read only after the clone exists), which the storm's client paid hundreds of times per
  second and would still pay while the failure stands. This ticket owns the volume that state produces; a
  retry cadence would delay the clone of a member whose failure is transient, and the state that makes the
  failure stand is `todo/layer-change-member-recovery.md`'s to explain.
