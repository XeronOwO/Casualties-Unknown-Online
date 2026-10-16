# The radiation line should stop above the layer floor, leaving a shared margin

- Status: Todo
- Priority: Medium
- Category: World hazard / layer progression / host rules
- Source: the user's 2026-10-07 backlog request — KrokMP's radiation line does not descend all the way to
  the layer floor; it locks at a position that leaves room, so a group can share supplies and rest. The ask
  is that behaviour as a switch, on by default, aligned with KrokMP.
- Related: `docs/en/reference/configuration.md` (host rules surface),
  `src/CasualtiesUnknownOnline.GameAdapter/World/RadiationLineSync.cs` (CUO's host-authoritative line state),
  `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/RadiationStragglerPolicy.cs` (the straggler
  behaviour that accelerates the line), `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/RadiationLineUpdatePatch.cs`
  and `RadiationLineDeactivatePatch.cs` (the KrokMP behaviour to align with),
  `reversing/Assembly-CSharp/Assembly-CSharp/RadiationLine.cs` (the native line)

## What is asked

One switch on the host's rule surface, on by default:

- On: the line's descent stops at a margin above the layer floor and stays there, so the bottom of the layer
  is livable and a group can share supplies and rest there.
- Off: today's behaviour — the line keeps descending until it reaches the floor.

KrokMP expresses the clamp as a comparable value: its `RadiationLineUpdatePatch` caps the line's own
`timeGone` at `WorldGeneration.world.height - 8U`, i.e. eight world units above the layer's height, and it
drives that value host-side while the straggler path accelerates the descent toward the cap.

## What is not known yet

- Whether the layer's end or any other progression step is keyed to "the line reached the floor". If it is,
  clamping the line changes progression and the switch has to carry that consequence explicitly instead of
  only moving a hazard boundary.
- How CUO's own two halves interact with the clamp: the host broadcasts the absolute line state at 5 Hz
  while a guest keeps running its local `RadiationLine.Update` between resends (see the type's own note in
  `RadiationLineSync`), so a clamp applied only host-side would be outrun locally.
- Which margin is right for this game's layer heights and body sizes; KrokMP's eight units is a starting
  number, not a measured one for CUO.

## Required work

1. Attribute first: read the native `RadiationLine` descent and every consumer of the line's position
   (progression, damage, presentation), and write down what a clamp would change besides the hazard.
2. Put the switch on the host rule surface with the default the user asked for, and make the value the same
   on every side — the host's broadcast and each guest's local continuation must agree, including for a late
   joiner and after a reconnect.
3. Keep the straggler policy meaningful: the line still accelerates toward a straggler, but it ends at the
   clamped position rather than at the floor.
4. Verify on three clients that the clamped line is at the same height on every view, that the switch's off
   position restores today's behaviour in the same session, and that nothing else about layer progression
   moved.

## Non-goals

- Not a new hazard: the line keeps every effect it has today, only its final position changes.
- Not a per-player setting: this is host-side world state, so it belongs on the host's rules and must not be
  a local toggle that desyncs the line.
## Attribution done (2026-10-17, before any implementation)

Read: `reversing/Assembly-CSharp/Assembly-CSharp/RadiationLine.cs` (the whole type), KrokMP's
`RadiationLineUpdatePatch`, and CUO's own `RadiationLineSync`.

1. **The native line has NO floor stop, and this ticket's own description of today's behaviour is wrong.**
   Its whole update is `this.timeGone += Time.deltaTime * (this.body.conscious ? 1f : 0.2f) * 1.5f;` followed by
   `base.transform.position = Vector3.up * this.world.halfHeight + Vector3.down * this.timeGone + Vector3.down *
   Mathf.Sin(this.timeGone * 0.66f) * 3f;` — `timeGone` is the only input and NOTHING caps it, so the line
   passes the layer's floor and keeps descending. So "the line keeps descending until it reaches the floor" is
   not the native behaviour; the clamp is a NEW upper bound rather than a restored one, and the switch has to
   be described that way or a reader will look for the native rule it replaces. KrokMP's bound is
   `WorldGeneration.world.height - 8U`, and because the position is `halfHeight - timeGone` (plus the wave), a
   cap of `height - margin` parks the line `margin` world units above the floor — which is the behaviour the
   user asked to align with.
2. **`timeGone` is the only thing to clamp, and clamping it changes nothing but the final height.** The
   damage, the eye effect and the irradiate intensity are all computed from `body.position.y -
   line.position.y` AFTER the position is set, so every one of those rules survives untouched; they simply
   stop getting worse once the line stops.
3. **Both sides must clamp, and they can do it independently — no new wire field.** `RadiationLineSync`'s own
   note says a guest keeps running its local `RadiationLine.Update` between the host's 5 Hz broadcasts, so a
   host-only clamp would be outrun locally. The position formula is PURE (no roll, no per-client input), so the
   same rule value applied on both sides converges; the host's rule surface already reaches every member, which
   is what makes the value the same everywhere including a late joiner.
4. **Still open, and it decides what the switch must say**: whether the layer's end or any other progression
   step is keyed to "the line reached the floor". `RadiationLine` answers no part of it — it carries no floor
   logic and no completion hook — so that reading belongs to the layer progression (`WorldGeneration` and
   whatever ends a layer), and it is the next step before the switch's own consequences can be stated.