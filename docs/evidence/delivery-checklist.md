# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the cycle records the live reading on the ticket: what the wire carries, what the two clients' logs said, and which half of the row is still open
- [ ] Native parity — the reading quotes the game's own behaviour that blocked the fixture: `PlayerCamera.Awake` writes `PlayerPrefs["lastchar"]` from `PreRunScript.characterSelected` instead of reading it (decompile), and the run start re-initialises the field
- [ ] Native parity probe — the probe recipe sets the field the pre-run screen's portrait writes and reports the prefab the game's spawn table names (`ok, index 2, spawn Special/Characters/Dune`)
- [ ] Entry mapping — the reading names each entry it used: the capture (`body.transform.root.name`), the clone factory (`RemoteBodyFactory.PrefabFor`), the renderer's line, the run start (driver `start-run`)
- [ ] Mechanism inventory — the two earlier misreadings are both named with their evidence (the Body child name; the pref being written, not read), so the next attempt does not repeat them
- [ ] Whole-family audit — the row's remaining blocker is a fixture capability, not a product defect: the pre-run screen's portrait is the game's own entry, and the fixture does not drive it yet
- [ ] Self-check table — the code slice it verifies is committed and green (`42f51c708`, `fdacf27fc`: build 0 errors, gates 834/0, focused 4/4) and this cycle changes only the ticket and one recipe
- [ ] Verification design — the next reading is named: drive the pre-run character screen, start the run, and each client's log must name the other's character
- [ ] Plan approved by the user — the user ordered the feature and the queue continues on the same item
- [ ] Structure review done — no source change in this cycle; the recipe is a fixture file the driver gate accepts (header, args, a returned value)
- [ ] Build + dotnet format + dotnet test normative gates pass
- [ ] Release-cycle deployment/acceptance: row 1 stays open on the pre-run screen fixture

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
