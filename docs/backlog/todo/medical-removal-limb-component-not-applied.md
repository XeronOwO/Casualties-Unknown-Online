# A removed limb component is never taken off the target's own body

- Status: Todo — **found 2026-10-11 by the acceptance run of batch `20261011-b`**, as
  `todo/medical-state-names-no-kind.md`'s row 4 (the Stage-3 family regression that ticket was checking).
  The run drove a real splint removal on a guest's limb and read the outcome on all three surfaces: the
  limb's own flag was written, **the component was not**, and the target's next report carried it straight
  back into the authority's copy — so the removal never actually happened, and driving it again awards a
  second splint item.
- Priority: Critical — the operation's outcome never lands on the target (the game's own wound view still
  draws the splint, and the component's `Update` keeps its healing ticks), the authoritative copy ends
  INCONSISTENT (`Splinted=false` beside a live `SplintLimb`), and the same limb can be "removed" again and
  again: the run drove it twice and the operator received two `splint` items for one splint.
- Category: Players / cross-player semantics (remote medical, Stage 3 removals)
- Related: `todo/display-clone-limb-component-runs-native-start.md` (the display half of the same
  component-state plumbing), `review/remote-medical-stage-3-other-actions.md` (the family design),
  `todo/medical-state-names-no-kind.md` (the ticket whose row 4 found it),
  `docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md` (this run's record).
- Source: the agent's own three-client run, batch `20261011-b`.
- Attribution: **not introduced by the fix under acceptance** (`87b3643a` touches neither file named below,
  nor `CharacterDataSync.ApplyHealState`, nor `MedicalTargetBodyValidator`). `OtherMedicalRemovalApplier`
  landed in `a1a6a970` ("add remote medical Stage 3 native minigames/actions") and `LimbComponentStateCodec`
  has exactly one commit, `4bcaf65a`, so its `Apply` has been additive-only since it was written.

## What the run showed

A splint was placed on the guest's limb 2 (a declared setup substitution, the game's own application
shape). The host then drove the native WoundView special action on that limb, and the family's own log
records the whole operation (`host-removal-operations-4-and-5.log.txt` in the batch artifact directory):

```
[MedicalOps3] started SplintRemoval operation 4: <host> -> <guest>, item - (id 0), limb 2.
[MedicalOps] awarded removed splint (id 9223372037175756739) to <host> in slot 0.
[MedicalOps3] operation 4 terminal Completed: progress 0.000.
```

The target's own client logged the state write it received — `[MedicalOps] local target body updated
(terminal=True)` at 12:33:51.563 (`guest-slip-and-removal.log.txt`) — and its own limb then read
(`run2-guest-local-limb2-after-removal.json`):

```
limb 2: splinted=false, components=SplintLimb
```

The authority's stored copy of the same limb, read after the next report, reads the same way
(`run2-host-saved-guest-limb2-after-removal.json`): `splinted=false, components=SplintLimb`.

**The authoritative removal was undone by the target's own report, and the item was still awarded.** The
run then drove the same removal a second time: the target's body check accepted it again (`request 5
answered by <guest>: this body allows it.`) and the operator was awarded a second splint
(`awarded removed splint (id 9223372041470724035) to <host> in slot 1.`).

## Root cause

The removal is host-side only, and the receiving path has no removal half. Call sites, with the code's own
wording:

- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/OtherMedicalRemovalApplier.cs` — the host
  clears the component **from its own stored character data**: `limb.Components.RemoveAll(c => c.TypeName ==
  componentType);` and, for a splint, `limb.Splinted = false;`, then
  `_access.SaveCharacterData(session.Target, newTarget);` and `GrantRemovedItemToOperator(...)`.
- The target learns about it through
  `src/CasualtiesUnknownOnline.GameAdapter/MedicalOperationApply.cs`'s generic branch — `else if (targetLocal
  && health is { } targetHealth)` → `domains.CharacterDataSync.ApplyHealState(body, targetHealth, limbs);`
  — which maps the limb FIELDS (so `Splinted` really does land) and then calls
  `LimbComponentStateCodec.Apply(body.limbs[limbData.Index], limbData.Components);`.
- **`src/CasualtiesUnknownOnline.GameAdapter/Character/LimbComponentStateCodec.cs`'s `Apply` is
  additive-only**: it walks the components the message carries (`foreach (var state in states)`) and adds or
  updates each one; there is no branch for a component the message does NOT carry. The removal's message
  carries the limb with an empty component list, and nothing acts on that absence.
- So the local `SplintLimb` survives, and because a guest body is report-driven, the target's next report
  puts the component back into the authority's copy: `CharacterDataCapture` sets
  `limbMsg.Components = LimbComponentStateCodec.Capture(body.limbs[i]);` and the character handler stores
  each report wholesale (`ctx.CharacterData.SaveCharacterData(sender, msg);`).
- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/MedicalTargetBodyValidator.cs`'s removal
  rule reads exactly that component list — `reason = limb.Components.Any(c => c.TypeName == "SplintLimb") ?
  "" : "Target limb has no splint.";` — so the unit is free again, which is how a second removal was
  accepted and a second item awarded.

## What the fix has to do

Give the receiving path a removal half, decided once and owned by one place:

- the state/terminal message already carries the limb's **complete** component list, so a receiver can treat
  that list as the truth for the component types the codec knows (`SplintLimb`, `TourniquetScript`,
  `ChilledLimb`) — remove what is absent, add or update what is present; or
- the removal family gets its own explicit fact ("these components were removed") if treating an absent
  entry as a removal is judged too broad for the other families that share the codec.
- Whichever way it goes, the destructive half must NOT run on a display clone: the display path
  (`RemoteMedicalCoordinator`) applies the same component state to a body CUO only renders, and the game's
  own component `Start` does real work there — see `todo/display-clone-limb-component-runs-native-start.md`,
  which the run found in the same scenario.
- The family's own tourniquet kind (`TourniquetScript`, same applier) is the sibling and takes the same fix.

## Acceptance rows

| # | Row | Fails when |
|---|---|---|
| 1 | a driven splint removal leaves the target's OWN limb without the component, read on the target's client | the local component survives the state it received |
| 2 | one report interval later the AUTHORITY's copy still shows the limb without it | the target's own report carried the component back |
| 3 | the operator receives the removed item exactly once, with the component's own condition | no item, or an item per repeat |
| 4 | a second removal on the same limb is refused by the target's own body check | it is accepted and awards another item |
| 5 | a driven tourniquet removal behaves the same on both surfaces | the sibling kind diverges |
| 6 | the display surfaces lose the removed component without running native behaviour on a clone | a display client throws (the sibling ticket's own rows cover this) |

## Limits

- The run proved the splint kind end to end and only inferred the tourniquet's (same applier, same codec,
  same validator shape); row 5 is unrun.
- The limb's `Splinted` flag DOES land, so a test that reads only the flag passes while the removal has not
  happened — the rows above read the component, not the flag.
