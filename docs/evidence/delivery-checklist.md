# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [ ] Design comprehension
  (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:2787-2792`) and tracks what it showed in
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/RemoteDragPredicatePatches.cs:206-211`; read with
  `docs/design/model/item-custody.md` and `docs/design/items/README.md`.
- [ ] Native parity
  (the `item-provide` recipe), the same call the native starting-supply grant makes.
- [ ] Native parity probe
  (`currentContainer` marker id and `containerUnloadButtons`) and PASSes the row.
- [ ] Entry mapping
  (`src/CasualtiesUnknownOnline.GameAdapter/RemoteBackpackView.cs:110`) followed by the game's own `OpenContainer`; the row
  drives it through the `remote-view-items` recipe, named in the ticket section of the same title.
- [ ] Mechanism inventory
  backpack window had no container to show (2026-11-26 02:08)" with the failing reading and the fix.
- [ ] Whole-family audit
  by every failed row on both ends (`.acceptance/budget-1` run-settings was the one whose exception it recovered).
- [ ] Self-check table
  emergencylight and nothing else; (2) line: the open loop in
  `tests/CasualtiesUnknownOnline.AcceptanceSuite/Cases/RemoteBackpackRefreshCase.cs`; (3) chain: fresh run → no container
  item on the host → the clone has no marked container → the window binds to nothing; (4) the grant lands first, so the
  cause (a missing container) is removed rather than the symptom.
- [ ] Verification design
  and `.acceptance/budget-1` where it failed.
- [ ] Plan approved by the user
  measured evidence without asking.
- [ ] Structure review done
  and its comment.
- [ ] Build + dotnet format + dotnet test normative gates pass
  `dotnet format CasualtiesUnknownOnline.slnx whitespace --verify-no-changes` exits 0.
- [ ] Release-cycle deployment/acceptance
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
