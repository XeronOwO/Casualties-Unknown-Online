# A removed limb component is never taken off the target's own body

- Status: Done — fixed 2026-10-11 (`7c5646bd`), accepted by batch `20261011-c`: rows 1-6 pass
  (`docs/evidence/acceptance/medical-removal-limb-component-not-applied-20261011-c.md`).
  **Found 2026-10-11 by the acceptance run of batch `20261011-b`**, as `done/medical-state-names-no-kind.md`'s
  row 4 (the Stage-3 family regression that ticket was checking). The run drove a real splint removal on a
  guest's limb and read the outcome on all three surfaces: the limb's own flag was written, **the component
  was not**, and the target's next report carried it straight back into the authority's copy — so the removal
  never actually happened, and driving it again awarded a second splint item.
- Priority: Critical — the operation's outcome never landed on the target (the game's own wound view still
  drew the splint, the component's `Update` kept its healing ticks), the authoritative copy ended
  INCONSISTENT (`Splinted=false` beside a live `SplintLimb`), and the same limb could be "removed" again and
  again: the run drove it twice and the operator received two `splint` items for one splint.
- Category: Players / cross-player semantics (remote medical, Stage 3 removals)
- Related: `done/display-clone-limb-component-runs-native-start.md` (the display half of the same
  component-state plumbing), `review/remote-medical-stage-3-other-actions.md` (the family design),
  `done/medical-state-names-no-kind.md` (the ticket whose row 4 found it),
  `docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md` (the run that found it).
- Source: the agent's own three-client run, batch `20261011-b`; reproduced in one client by the probe of
  batch `20261011-c` (below).
- Attribution: **not introduced by the fix under acceptance** (`87b3643a` touches neither file named below,
  nor `CharacterDataSync.ApplyHealState`, nor `MedicalTargetBodyValidator`). `OtherMedicalRemovalApplier`
  landed in `a1a6a970` ("add remote medical Stage 3 native minigames/actions") and `LimbComponentStateCodec`
  has exactly one commit, `4bcaf65a`, so its apply has been additive-only since it was written.

## What the run showed

A splint was placed on the guest's limb 2 (a declared setup substitution, the game's own application
shape). The host then drove the native WoundView special action on that limb, and the family's own log
records the whole operation (`host-removal-operations-4-and-5.log.txt` in batch `20261011-b`'s artifact
directory):

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

The authority's stored copy of the same limb, read after the next report, read the same way
(`run2-host-saved-guest-limb2-after-removal.json`): `splinted=false, components=SplintLimb`.

**The authoritative removal was undone by the target's own report, and the item was still awarded.** The
run then drove the same removal a second time: the target's body check accepted it again (`request 5
answered by <guest>: this body allows it.`) and the operator was awarded a second splint
(`awarded removed splint (id 9223372041470724035) to <host> in slot 1.`).

## Root cause

The removal was host-side only, and the receiving path had no removal half:

- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/OtherMedicalRemovalApplier.cs` — the host
  clears the component **from its own stored character data**: `limb.Components.RemoveAll(c => c.TypeName ==
  componentType);` and, for a splint, `limb.Splinted = false;`, then
  `_access.SaveCharacterData(session.Target, newTarget);` and `GrantRemovedItemToOperator(...)`.
- The target learns about it through
  `src/CasualtiesUnknownOnline.GameAdapter/MedicalOperationApply.cs`'s generic branch — `else if (targetLocal
  && health is { } targetHealth)` → `domains.CharacterDataSync.ApplyHealState(body, targetHealth, limbs);` —
  which maps the limb FIELDS (so `Splinted` really landed) and then called
  `LimbComponentStateCodec.Apply(body.limbs[limbData.Index], limbData.Components);`.
- That apply was **additive-only**: it walked the components the message carried (`foreach (var state in
  states)`) and added or updated each one; there was no branch for a component the message does NOT carry.
  The removal's message carries the limb with an empty component list, and nothing acted on that absence.
- So the local `SplintLimb` survived, and because a guest body is report-driven, the target's next report put
  the component back into the authority's copy: `CharacterDataCapture` sets
  `limbMsg.Components = LimbComponentStateCodec.Capture(body.limbs[i]);` and the character handler stores
  each report wholesale (`ctx.CharacterData.SaveCharacterData(sender, msg);`).
- `MedicalTargetBodyValidator.cs`'s removal rule reads exactly that component list — `reason =
  limb.Components.Any(c => c.TypeName == "SplintLimb") ? "" : "Target limb has no splint.";` — so the unit was
  free again, which is how a second removal was accepted and a second item awarded.

**The receiver re-reports in the SAME call that applies the message**, which the fix has to answer for:
`MedicalOperationApply.ApplyProgress` ends with `domains.CharacterDataSync.ReportInventoryChanged(body)`
(`CharacterDataSync.ReportInventoryChanged` → `CharacterDataCapture.Capture`, unthrottled). A removal that
only *scheduled* its destruction would still be captured by that report — Unity destroys at the end of the
frame — and carried straight back to the authority, i.e. the same defect one frame later.

## The fix

The receiving half exists now, decided once and owned by one place —
`src/CasualtiesUnknownOnline.GameAdapter/Character/LimbComponentStateCodec.cs`:

1. **The two halves are named at every call site.** The single `Apply` became `ApplyAuthoritative` (the body
   that OWNS the limb) and `ApplyDisplay` (a body CUO only renders), both keeping the same signature, and no
   unnamed entry point remains. `CharacterDataSync.ApplyHealState` and `CharacterRestoreApplier` take the
   authoritative one; `RemoteMedicalCoordinator` takes the display one. A caller can no longer pick a half by
   accident, which is how the display half acquired its promise in the first place (see the sibling ticket).
2. **The message's list is the truth for the three types this codec owns** (`SplintLimb`,
   `TourniquetScript`, `ChilledLimb`): what it names is added or updated, and what it omits is REMOVED. A null
   list — which only a direct caller can pass, since the wire's list is initialized empty and every CUO-side
   producer of a limb row fills it from a capture — claims nothing and therefore removes nothing.
3. **The removal is not the game's own teardown.** `SplintLimb.TakeOff` / `TourniquetScript.TakeOff`
   instantiate the removed item and hand it to the limb's OWNER (`AutoPickUpItem`), while the design gives the
   removed item to the OPERATOR (the authority grants it); running them here would hand the target a second
   splint. The component is destroyed instead, and the limb's own flags (`Splinted`, `BlockedBleeding`, and
   the lower-limb tourniquet block) arrive as the message's limb fields, which the mapper writes.
4. **The destruction is immediate**, not frame-deferred, for the same-call re-report above:
   `Object.DestroyImmediate`. The three components have no `OnDestroy`, so nothing else runs.
5. **The producer audit** (the list's completeness is what makes "absent means removed" safe): every builder
   of a limb row copies the complete list — `CharacterDataCapture.Capture`,
   `PlayerCharacterAccess.CloneLimb`, `PlayerInteractionKernelCodec`, `KernelLimbWireMapper`,
   `EnemyCombatKernelCodec`, `TraderRecruitPolicy` all assign `Components` from the source limb's list — and
   `CharacterLimbMsg.Components` is initialized empty, so no construction site can leave it unset by omission.
   A wire that cannot distinguish "omitted" from "empty" is the residual risk, and it is bounded by that audit
   (see *Limits*).

## What the code half proves

- The reflective surface case pins the two modes, their shared signature, and the absence of an unnamed
  `Apply` — `tests/CasualtiesUnknownOnline.Tests/Patching/LimbComponentStateCodecTests.cs`.
- The source gate `LimbComponentApplyGateTests` pins what no behavioural case can reach: which file may name
  which mode, that the removal covers EXACTLY the types the capture writes, that the game's own teardown is
  never called from the codec, that the destruction is immediate, and — for the sibling ticket — that the
  display half refuses a body CUO does not own and disables every instance it creates.
- **The behaviour is proven at runtime, in one client, through the production call**:
  `tools/acceptance/recipes/limb-component-apply.cs` captures the caller's own body, edits ONE limb row to the
  shape the authority sends (no components, `Splinted=false`) and applies it through
  `CharacterDataSync.ApplyHealState`. On the unfixed build (batch `20261011-c`, artifact
  `red-message-clear-limb3.json`) it read

  ```
  componentsBefore=SplintLimb  componentsAfter=SplintLimb   (the component survived)
  splintedBefore=true          splintedAfter=false           (the flag landed anyway)
  messageComponents=0                                        (the message carried nothing)
  ```

  i.e. the whole defect in one call, with no session: the flag and the component disagree.

## Acceptance rows

| # | Row | Fails when |
|---|---|---|
| 1 | a driven splint removal leaves the target's OWN limb without the component, read on the target's client | the local component survives the state it received |
| 2 | one report interval later the AUTHORITY's copy still shows the limb without it | the target's own report carried the component back |
| 3 | the operator receives the removed item exactly once, with the component's own condition | no item, or an item per repeat |
| 4 | a second removal on the same limb is refused by the target's own body check | it is accepted and awards another item |
| 5 | a driven tourniquet removal behaves the same on both surfaces | the sibling kind diverges |
| 6 | the display surfaces lose the removed component without running native behaviour on a clone | a display client throws, or the panel keeps showing the removed component (the sibling ticket's rows cover this) |

Rows 1-4 are the three-client rows batch `20261011-b` failed, and row 5 is the sibling kind the fix covers
by construction.

## Limits

- **The wire cannot say "I did not mean to carry a component list".** `CharacterLimbMsg.Components`
  initializes empty, so a hypothetical future producer that fills a limb row without a capture would look
  exactly like a removal. The audit above is what bounds it today; a producer-side guard (a named "the list is
  complete" fact) would close it, and no such fact exists yet.
- The run proved the splint and tourniquet kinds end to end (rows 1-5, batch `20261011-c`); the chilled
  kind has no removal path in the game (an icepack's component expires on its own), so it is covered by the
  sibling ticket's display rows only.
- The one-client probe proves the RECEIVING half in isolation, red and green
  (`red-message-clear-limb3.json` before the fix, `c-green-message-clear-limb2.json` after it). Row 2's
  authoritative half — that no later report wins the race — is the three-client run's own reading, and it
  passed: the authority's copy held `components=""` after the target's reports.
