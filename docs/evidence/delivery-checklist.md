# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: row 27's claim and the game's own tourniquet chain, its removal button and the body's blood volume are written in the rebuild ticket's row 27 section with `TourniquetScript.cs` and `PlayerCamera` lines.
- [x] Native parity — evidence: the wound is the game's own damage, the blocked set is `GetLowerLimbs` read through `limb-bleeding`, the removal is the wound view's own `WoundSpecialAction`, and the stop is the body's own `bloodVolume`.
- [x] Native parity probe — evidence: `.acceptance/tourniquet-1` PASS 27.6 s — the chain is blocked and nothing outside it, the volume holds over three seconds, and the removal opens the set again.
- [x] Entry mapping — evidence: the row applies through the same `PlayerCamera.ApplyWoundItem` the wound view's drop runs and removes through the wound view's own button; the drag itself is named as not drivable because it reads the native cursor.
- [x] Mechanism inventory — evidence: `wound-seed` (game damage), `item-provide` (fixture item), `select-limb`, `limb-use mode=use` (the apply seam), `limb-use mode=special` (the wound view's removal), `limb-bleeding` and `body-effects`.
- [x] Whole-family audit — evidence: the mid-row fresh run was removed and replaced by a clear failure, because manufacturing a body is not a player operation; the item-creation residual is named.
- [x] Self-check table — evidence: three questions answered in the ticket's row 27 section (the set alone was judged before; the bleeding and the removal were not judged at all; the apply was not a player's drag).
- [x] Verification design — evidence: failures name the limb index and its row, the chain that changed, the volume before and after, and the blocked flag after the removal.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: applying, removing and reading are separate recipe modes, so no recipe both stages and judges.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests change only, nothing to deploy; `.acceptance/tourniquet-1` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
