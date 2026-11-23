# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: row 19's claim, the game's own screen-shader and wound-view ownership, and our spectator entry are written in the rebuild ticket's row 19 section with `PlayerCamera.cs`/`WoundView.cs` lines.
- [x] Native parity — evidence: the screen effects follow the body the camera holds (`PlayerCamera.HandleScreenShaders`), the strip is drawn from the wound view's body (`WoundView.cs:437-502`), and the entry is our console command over the game's own console service.
- [x] Native parity probe — evidence: `.acceptance/spectate-4` PASS 2.3 s — strip at limb 3 is the member's steelprosthesis while the host's own control is limb 4, shader pain follows the member, and the camera sits within 6 units of the body it holds.
- [x] Entry mapping — evidence: `/gamemode spectator` then `/spectate <member>` typed through `ICommandControl.TryExecute`, the same call the overlay's Enter key makes.
- [x] Mechanism inventory — evidence: `console-run` (typing), `spectator-read` (camera body, strip, shader, ECG, camera/body positions), `wound-seed`/`limb-prosthetic4` (the member's real wound and prosthetic), `body-motion`/`stage-bind` (the spectator's own keys).
- [x] Whole-family audit — evidence: the "the watched body cannot walk while watched" question is named in the ticket rather than asserted; the strip/shader/ECG family is unchanged and still reads the game's own objects.
- [x] Self-check table — evidence: three questions answered in the ticket's row 19 section (a player entry, a player-visible follow distance, and the unjudged walk as a named question).
- [x] Verification design — evidence: failures name both sides — the camera-to-body distance, the watched body's drift under the spectator's key, and the strip item against the limb that only the member carries.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the recipe split keeps typing (`console-run`), reading the presented screen (`spectator-read`) and driving bodies (`stage-bind`/`body-motion`) separate, so no probe both stages and judges.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-and-recipes change only, nothing to deploy; `.acceptance/spectate-4` is the acceptance reading and 48 frames are the picture.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
