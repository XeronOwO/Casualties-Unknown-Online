# A member joining a run in progress never binds the generated enemies

- Status: Todo
- Priority: High
- Category: Enemy sync / generation baseline / late join
- Source: batch `20261006-g`'s third client (2026-10-06) — a member that joined the lobby and entered the
  world AFTER the run had started logs the pairing failure once per 60 s cycle for the whole session while
  the member that was present at world entry reads the mapping as established in the same cycles. Found by
  the acceptance run while reading that batch's inventory rows, which are unaffected.
- Related: `done/enemy-runtime-spawn-classification.md` (the fix that made the generation baseline read
  `mapping=True` for a member present at world entry; this ticket is the case its acceptance never drove),
  `done/enemy-snapshot-binding-recovery.md` (the repair carrier), `done/layer-change-member-dropout.md`
  (the other member-lifecycle defect), `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemySyncCoordinator.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemySpawnArbitration.cs`,
  `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md`

## Symptom (read from batch `20261006-g`, three clients, one lobby)

The run started with two members in the world; the third joined the lobby and entered the world about two
minutes later. From its own entry onward it logs, every 60 s:

```text
[WRN] [Enemy] generation spawn pairing failed (69 host vs 69 guest generated enemies) — generated copies stay local (generation divergence); runtime spawns are still bound.
[INF] [Enemy] snapshot applied: 0 generated bound, 0 runtime spawns, mapping=False.
```

Its entry sequence shows the freeze itself working — `[Enemy] guest froze 69 enemy copies at generation
finish (before they move).` one second before the first failure — so **the first pairing already fails**;
this is not a case of copies drifting after a successful bind. The member that was in the world from the
start reads the same cycles as established: `[Enemy] snapshot applied: 69 generated bound, 0 runtime
spawns, mapping=True.`, and the host sends both peers identical snapshots (`[Enemy] snapshot sent to
<steamid>: 69 enemies, 0 runtime spawns.`).

The counts agree on both sides, so nothing is missing on the wire: the two ordered position lists do not
line up index by index.

## What the readings already rule out

- **Not a count mismatch**: the warning itself reports `69 host vs 69 guest`, and the host's own snapshot
  line says 69.
- **Not the runtime-spawn half**: both clients and the host report `0 runtime spawns`, and the warning says
  runtime spawns stay bound.
- **Not a late drift of the guest's copies**: the copies are frozen at generation finish, one second before
  the failing pass, so whatever is wrong is already true at freeze time.
- **Not a missing repair**: the failure repeats every cycle for the whole session (five consecutive cycles
  read), so the 60 s repair pass runs and keeps failing rather than never running.

## What the traces point at, and what a fix has to decide

The pairing is all-or-nothing: `EnemySpawnArbitration.TryPair` sorts both sides by `(x, y)` ascending and
fails the WHOLE set when any index-pair exceeds `PairTolerance = 0.5f` world units. One side of that
comparison is the host's own anchor set (`EnemyStateCapture`'s spawn position per bound id), which is the
same input for every member; the other is the joining member's own copies. So the open question is what the
late joiner's copies are and where they stand:

1. **Are the copies at the deterministic generation positions at all?** A member present at world entry
   generates its own animals from the same seed as the host; a member entering an ALREADY RUNNING world
   resets its generation stream to the host's captured baseline (`[WorldParamsService] Generation stream
   reset to captured baseline (…)`) and then generates. Whether that reset makes its
   `WorldGeneration.DistributeEntities` output identical to the host's generation-time output is the first
   thing to read — the fix needs the two ordered lists side by side and the first index whose distance
   exceeds the tolerance, not the aggregate count.
2. **Is the host's anchor still the generation position by then?** The capture comment states the anchor is
   the host's FIRST-BIND position, which equals the generation position only while generation has just
   finished. A late join is by definition a bind attempt long after the host's own generation — if any
   anchor was written at a first bind that happened after the animal moved, the late joiner can never match
   it, while the member present at entry still can.
3. **The decision the fix takes**: either the joining member is handed the host's authoritative generated
   positions (a deterministic key that survives a late entry) instead of being asked to reproduce them, or
   the pairing gains an identity that does not depend on where a copy stood at bind time. Either way the
   all-or-nothing verdict stays — what must not survive is a member whose whole generated set stays local
   for the session.
4. **The blast radius is the same family**: the pairing key is also what the runtime-spawn bind and the
   per-cycle repair use, so the fix has to say what happens to a member whose generated set never pairs
   (today: it keeps logging every 60 s and its animals never follow the host).

## Acceptance

Read on the deployed artifact, three clients, one lobby, and judged on the joining member's own window:

1. Join the lobby and enter the world BEFORE the host starts the run (the reference): the member reads
   `[Enemy] snapshot applied: N generated bound, 0 runtime spawns, mapping=True.` from its entry on.
2. Join the lobby and enter the world AFTER the run has started (the case this ticket is filed from): the
   member reads the same `mapping=True` line with the same `N generated bound` within one repair cycle of
   its entry, and **zero** `generation spawn pairing failed` lines for the rest of the session.
3. In that session, move a host-driven enemy and read its position on the late joiner: it follows the host
   like any bound copy, and the third client's view agrees.
4. A member that joins mid-run while the host holds runtime spawns reads them bound on the same pass
   (`R runtime spawns` matching the host's count).
5. Regression: the member present at world entry keeps its green line and the host's log carries no new
   warning.
