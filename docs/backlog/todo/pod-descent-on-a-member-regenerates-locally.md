# A member's drill-pod descent regenerates a layer the session never agreed on

- Status: Todo
- Priority: Medium
- Category: World generation / layer transition / descent producers
- Source: found by the family sweep of the 2026-10-07 cycle that landed
  `review/layer-complete-choice-for-members.md` — NOT user-reported. The user's report named the
  end-of-layer choice (the panel); the sweep found the same sink reachable from a world object, and the
  rule "fix the family, not just the reported case" is why this is filed instead of silently left.
- Related: `review/layer-complete-choice-for-members.md` (the panel producer, landed 2026-10-07),
  `todo/layer-descent-spawn-separation.md` (where the members arrive, the same boundary),
  `todo/layer-change-member-recovery.md` (a member out of the world at the boundary),
  `reversing/Assembly-CSharp/Assembly-CSharp/DrillPod.cs` and `WorldGeneration.cs`

## What is read

- `DrillPod.Update` triggers the descent from the LOCAL body: after the pod is repaired and the local
  body crouches next to it for 5 s, it sets `WorldGeneration.world.doPod = true` and calls
  `WorldGeneration.world.StartCoroutine("RegenerateWorld", true)` (`DrillPod.cs:20-32`).
- `WorldGeneration.RegenerateWorld(bool twice)` is the same coroutine the end-of-layer panel's
  `ContinueRun` starts, with `twice: true`: it runs `IncreaseDepthByLayer()` twice and adds TWO to
  `biomeDepth` (`WorldGeneration.cs:1042-1067`).
- The `doPod` flag is read by `FinishWorldGeneration` (`WorldGeneration.cs:3611-3618`): the client that
  rode the pod takes hearing loss +15, hunger -10 and thirst -15 on ITS OWN body, speaks the `drillend`
  line and spawns a broken pod object where it arrived.
- CUO's layer baseline is the HOST's capture (`WorldParamsService.CaptureAtBoundary`, reached only from
  the host/solo branch of `RunCoordinator.OnWorldGenerate`), and a member's own regeneration applies
  nothing new — the params instance in its hand is the layer it is leaving
  (`WorldParamsService.EnsureGuestApplied` is idempotent per params instance).

## What is not covered by the landed panel fix

The landed change delegates exactly ONE producer: the panel's click. Its sink
(`WorldGenerationRegenerateWorldPatch`) sits on the coroutine every descent passes through, and it acts only
when the entry's own marker says the call came from `WorldGeneration.ContinueRun`. The pod never sets that
marker — it starts `RegenerateWorld` by NAME with `twice: true` — so a member that rides a pod still
regenerates locally, still by TWO layers, on a baseline the session never agreed on: the panel's request does
not see it, and the session still does not move. (The marker is also what keeps the console's `skiplayer` on
the game's own path.)

## What is not known yet

- Whether a member's pod is reachable in a live session at all, and what each client logs when it is
  used — a reading, not a guess (three clients; a member rides the pod while the others stand still).
- Whether the right shape is to route the pod like the panel (one request carrying "two layers" and the
  pod's own arrival flag, with the host driving `RegenerateWorld(twice)`) or to refuse the local descent
  on a member with a named log and let the pod's use drive the same request the panel sends. The pod's
  own arrival effects belong to the RIDER's own body in either shape, which is the part the panel's
  request shape does not express today.

## Required work

1. Attribute first, on three clients: a repaired pod, one member riding it, and each client's world,
   log and layer index read before and after.
2. Decide the shape with that reading in hand and write it down before implementing; the pod's
   two-layer step and its arrival effects are its own and must not be silently reduced to the panel's
   one-layer request.
3. Cover the failure paths: a pod ridden while the host is dead (the case
   `review/layer-complete-choice-for-members.md` exists for), a pod ridden by a member that is out of the
   world, and a pod whose repair kit was consumed by a different member than the rider.

## Non-goals

- The end-of-layer panel: landed in `review/layer-complete-choice-for-members.md`.
- Where the members arrive after a descent: `todo/layer-descent-spawn-separation.md`.
- Removing the debug console's `skiplayer` (the game's own command).
