# Acceptance — a member's drill-pod descent is the session's step, and its two layers reach the host

- Ticket: `docs/backlog/todo/pod-descent-on-a-member-regenerates-locally.md` — verdict: **the main row passes;
  the ticket stays in `todo`** for the refusal rows (host dead, rider out of the world, the repair kit consumed
  by another member), which need their own named-refusal lines.
- Batch: `.acceptance/20261024-c/` (local, gitignored; run `pr2`)
- Artifact: the tree's build deployed for the run (the fix's own build; the deliverable commit carries it)
- Date: 2026-10-24
- Result: **the member's local regeneration is suppressed, the pod's TWO-layer step reaches the host, and the
  member follows the host's edge to the same layer.**

## The change measured

- `LayerAdvanceRequestMsg.Twice` carries the pod's two-layer step with the member's request.
- The sink patch now recognizes the pod's own descent (`WorldGeneration.world.doPod` set immediately before its
  `RegenerateWorld(true)` call) as a session-owned call and delegates it like the panel's click, passing the
  step. The console's `skiplayer` sets neither flag and keeps the game's own path.
- The host drives `RegenerateWorld(twice)` for the request and announces the edge with the same flag, so the
  members (the rider included) run their own regeneration with it; the rider's `doPod` stays set for its arrival
  effects and is cleared by the game's own `FinishWorldGeneration`.
- A member's own regeneration for the host's edge is MARKED (`LayerAdvanceCoordinator.InHostEdgeDescent`) so the
  sink never delegates it a second time — without the mark the pod's still-set `doPod` made the follow-up look
  like a second request (measured: two `[LayerChoice] sent ... twice: True` lines on the first build).

## The run

Two clients, a Steam lobby, a world on layer 1 with 26 `DrillPod` instances on both sides. The guest's client
started the pod's own sink (`doPod` plus `StartCoroutine("RegenerateWorld", true)`). Then:

| read | host | guest (rider) |
|---|---|---|
| before | `depth:0, generating:false, pods:26` | `depth:0, generating:false, pods:26` |
| immediately after the guest's call | `depth:2, generating:true, pods:0` | `depth:2, generating:true, pods:0` |
| after both finished | `depth:2, generating:false, doPod:false` | `depth:2, generating:false, doPod:false` |

The guest's log carries exactly ONE request and then the host's edge:

```
[LayerChoice] sent this side's end-of-layer choice to the host (run 1 layer 0, twice: True).
[LayerDescent] the host is advancing a layer now (twice: True, run 1 layer 0) — this member follows ...
[LayerDescent] host descent accepted (twice: True) — this member runs its own new-layer generation ...
[LayerDescent] this member starts its own new-layer generation (twice: True) from the host's descent edge.
```

and the debug line `this side's descent (Guest, session active: True) is not the session's to announce` proves
the member's own regeneration is not re-announced.

## What this replaces

Before the change the same probe on a guest left the HOST at `depth:0` with 26 pods while the guest's world moved
`0 -> 2` on its own (the ticket's own earlier reading). The member no longer regenerates a layer the session
never agreed on.

## Limits

- The pod's producer (`DrillPod.Update`'s repair + 5 s crouch) was not staged; the probe starts the same sink
  call the producer starts, which is the behaviour under test. The producer's own UI reachability is unchanged.
- The refusal rows (host dead, rider out of the world, the repair kit consumed by another member) are NOT yet
  read: the delegation currently refuses by keeping the game's own descent when there is nothing to report, and
  the ticket's named-refusal shape for those cases is still to implement.
- No OS-level mouse or keyboard input was used.
