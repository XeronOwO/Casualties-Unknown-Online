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
