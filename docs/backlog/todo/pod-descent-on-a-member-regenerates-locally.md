# A member's drill-pod descent regenerates a layer the session never agreed on

- Status: Todo — the main routing landed 2026-10-24 (the member's pod descent is delegated with its two-layer
  step, the host drives it, the rider follows the edge; read on two clients). The ticket stays `todo` for the
  refusal rows (host dead, rider out of the world, the repair kit consumed by another member) and their named
  refusal lines. Record:
  `docs/evidence/acceptance/pod-descent-on-a-member-regenerates-locally-20261024-a.md`.
- Priority: Medium
- Category: World generation / layer transition / descent producers
- Source: found by the family sweep of the 2026-10-07 cycle that landed
  `review/layer-complete-choice-for-members.md` — NOT user-reported. The user's report named the
  end-of-layer choice (the panel); the sweep found the same sink reachable from a world object, and the
  rule "fix the family, not just the reported case" is why this is filed instead of silently left.
- Related: `review/layer-complete-choice-for-members.md` (the panel producer, landed 2026-10-07),
  `done/layer-descent-spawn-separation.md` (where the members arrive, the same boundary),
  `done/layer-change-member-recovery.md` (a member out of the world at the boundary),
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
- Where the members arrive after a descent: `done/layer-descent-spawn-separation.md`.
- Removing the debug console's `skiplayer` (the game's own command).

## The sink, read on the machine (2026-10-24)

Attribution first, as the ticket's step 1 asks. Two clients on the deployed `0.1.0+9c350df3...`, a world on
layer 1 (24 `DrillPod` instances present on BOTH clients, bodies at the same position). The guest's client then
started the pod's OWN sink — `WorldGeneration.world.doPod = true` plus
`StartCoroutine("RegenerateWorld", true)`, exactly what `DrillPod.Update` starts, without the repair/crouch
producer — and both clients were read before and after:

| client | before | after |
|---|---|---|
| guest (the rider) | `depth:0, doPod:false, generating:false, pods:24` | `depth:2, doPod:true, generating:true, pods:0` (its own world regenerating by TWO layers) |
| host | `depth:0, doPod:false, generating:false, pods:24` | `depth:0, doPod:false, generating:false, pods:24` (unchanged) |

So a member's pod-path descent regenerates the MEMBER's world by two layers while the host and the session keep
the layer they agreed on; the member's session then goes on receiving the host's layer-1 snapshots
(`TrapLayoutSnapshot`, `EnemyStateStream` in its traffic) against a layer-2 world. That is the defect, confirmed
at its own sink rather than only by reading.

The producer is not the only path: `DrillPod.Update` is the world object's own trigger, and the landed panel
fix's marker only recognizes the panel's `ContinueRun`, so the pod's call has no request in front of it.

## The shape (decided 2026-10-24, before any implementation)

**Route the pod like the panel, with the pod's own facts in the request.** A member's pod must not start
`RegenerateWorld(true)` locally; it sends a descent request that carries (a) the pod's TWO-layer step — not the
panel's one-layer advance — and (b) the pod arrival flag, because the rider's own arrival effects (hearing loss
+15, hunger -10, thirst -15, the `drillend` line, the broken pod object) belong to the RIDER's body and must not
be silently folded into the panel's descent. The host validates the request (in world, session alive, the pod
still repairable/its repair kit accounted for) and drives the regeneration for the session; the rider's client
applies its own arrival effects when its world reports the arrival the host committed, so the effects run once
on the body that rode.

Refusal shape: a pod used by a member while the session cannot take the descent (the host dead, the member out
of the world, the repair kit consumed by someone else) is refused ON THE MEMBER with a named line, and its local
regeneration is never started — the same "actor sees the refusal" rule the intent family already uses.

Failure paths this must cover: host dead when the pod is used; rider out of the world; a repair kit consumed by
a different member than the rider. Each is a row of the implementation's acceptance, not a detail.

The implementation is the next cycle for this ticket; this section is the reading and the decision it had to
come before.
