# A dead member is stranded behind the loading screen when the host descends a layer

- Status: Todo — found 2026-11-02 by the user: "the guest's screen is frozen". Read as a real defect and reproduced
  once, from the guest's own log and its live scene state. The fix depends on the intended rule for a dead teammate
  during a descent, which is a gameplay decision and is asked before any code changes.
- Priority: High (a dead teammate is unrecoverable for the rest of the run, and the client looks hung to the player)
- Category: Run / layer descent / start gate / death
- Source: user report, 2026-11-02, while the guest had been killed with `/kill` and the host then descended a layer.

## Design
Read from `docs/design/mechanics/layer-advance.md` (how a descent is decided and who follows it) and `docs/design/mechanics/death-and-drops.md` (what a dead body owns): the descent follows the players who are IN the run, and a dead body is dropped out of it — which is why the policy classifies a dead member as not playing. That reading is what makes this a design gap rather than a bug in the policy.

## What the player sees

The guest is dead. The host reaches a sink and descends a layer. The guest's screen keeps the game's loading image
("generating plants and hidden traps") and never leaves it: no world, no death menu, no way to act. The host cannot
reach the guest either — `/heal <that player>` answers "no players match that selector".

## What the client actually says (readings)

Guest `scene-read` while the picture is stuck:

```
loadingActive=True loadingName=LoadingImage generating=False doingRegen=False
worldExists=True x=2.45 y=494.49
```

The host, at the same moment: `loadingActive=False`, `x=342.08 y=228.62`. So the two clients are in different layers and
the guest is behind the loading screen with a live world object.

The guest's own log, in order:

1. `[LayerDescent] the host is advancing a layer now (twice: False, run 1 layer 0) — this member …`
2. `[LayerDescent] the host descended but this member is not playing in its world (Idle, in world: False) — it keeps the
   ordinary entry path.`
3. `[Gate] keeping the loading screen up while waiting for the host.`
4. `[ItemReconcile] 292 authoritative entry/entries: 0 bound, 292 materialized` — the new layer's generated items were
   applied, so the layer itself arrived.

`LayerDescentPolicy.DecideFollow` (`src/CasualtiesUnknownOnline.GameAdapter/Run/LayerDescentPolicy.cs`) returns
`NotPlaying` when `!inWorld || !playingOrWaiting`, and the controller returns, which is deliberately "the ordinary entry
path". The next thing the ordinary path does is the start gate, and the gate holds the loading screen waiting for a host
start that a descent never sends.

## Why the member is not playing

The member was killed through the `/kill` cheat verb minutes earlier. A dead body's run phase is `Idle` and it is not
in its world, so the descent policy classifies it as "not playing" — correctly, by its own definition — and every later
path assumes the member will re-enter through the ordinary join, which never happens on a descent.

## The decision this needs

What should a DEAD teammate do when the host descends a layer? The three shapes that are implementable:

1. **Carry the body down.** The dead member follows the descent like a live one: its body is placed in the new layer and
   the team can revive it there. Most forgiving, and the corpse keeps the run's continuity.
2. **Stay behind and become a spectator.** The member is shown the death/spectator state instead of a loading screen,
   and reviving it means the host returns or the feature is unavailable this run.
3. **Hold at the gate only while a descent is genuinely in flight** — i.e. fix the gate so a `NotPlaying` member is not
   parked behind a loading screen, and treat the death case separately afterwards.

Whichever is chosen, one half is not negotiable: **the loading screen must not hold forever for a member nobody will
start.** The gate needs a bound or an explicit "this member is dead" state, and the fix must be judged on the guest's
own screen.

## Acceptance rows

| # | Action | Expectation |
|---|---|---|
| 1 | guest dead, host descends | the guest's screen leaves the loading image within the run's normal descent time |
| 2 | the same guest afterwards | it can be revived (by the team, at whatever place the chosen rule puts it) |
| 3 | guest alive, host descends | unchanged from today: it follows and lands in the new layer |
| 4 | the host's own descent | unchanged: no waiting, no gate |

## First fix landed, and the next link in the chain (2026-11-02, 15:55)

`LayerDescentPolicy.DecideFollow` now takes the body's own liveness: a dead body follows the same generation as a live one instead of being classified `NotPlaying` and handed to the ordinary entry. Three policy cases pin it, the full suite and the gates are green, and the build is deployed and identity-checked.

On the running game the guest's own log changed exactly as intended — the stranded state is gone and the follow path runs:

```
[LayerDescent] host descent accepted (twice: False) — this member runs its own new-layer generation
[LayerDescent] this member starts its own new-layer generation
[LayerDescent] this member's new layer is generated — waiting for the host's release
```

But the guest stays on `LoadingImage` after the layer is generated, so the follow does not complete: the release it waits for does not arrive. Two candidates, and the next reading decides between them:

1. **The release is a gate the host opens by ARRIVING.** This batch descended with the `layer-descend` recipe, which calls the game's `WorldGeneration.RegenerateWorld` directly — the host never travels through a sink, so the arrival-shaped release a real descent produces may simply not exist in this fixture.
2. **The release exists but does not reach this member.** The host's log does show `Start gate released — everyone is in the world`, and the guest's own log carries a kernel baseline for a DIFFERENT layer than the host's, which would explain a member waiting on a layer the host has left.

So the acceptance rows are still open, and the fixture that can judge them is a descent through the game's own sink rather than the direct regeneration this recipe performs.