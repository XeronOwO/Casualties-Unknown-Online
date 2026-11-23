# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the row's claim, the game's own settings catalogue and the preset it applies are written in the rebuild ticket's row 26 section, with `RunSettings.cs` lines for each.
- [x] Native parity — evidence: the row reads `RunSettings.settingTypes` (57 entries), applies `RunSettings.presets`' `normal` entry the way the pre-run screen does, and reads the run's own table `WorldGeneration.runSettings`.
- [x] Native parity probe — evidence: `.acceptance/runsettings-5` PASS 2.2 s — eight sampled settings match the preset's own name→value map, and `healingrate` 20 heals the wounded limb faster than 0.1 over equal three-second windows.
- [x] Entry mapping — evidence: the settings are applied through the game's own preset object (`run-preset`), and the effect is read through the game's own limb (`limb-read`, source local) after its own damage entry (`wound-seed` → `Limb.ImpactDamage`).
- [x] Mechanism inventory — evidence: three recipes carry the row — `run-settings-list` (catalogue and preset names), `run-preset-map` (the preset's own values), `run-preset` (apply) — plus the existing `run-setting`/`run-setting-read`/`limb-read`/`wound-seed`.
- [x] Whole-family audit — evidence: the save half is named as this row's residual in the ticket, with the reading that the pre-run copy is what the save writes; the same pre-run entry is row 2's parked item.
- [x] Self-check table — evidence: the three questions are answered in the ticket's row 26 section (a count is no longer the judge; the write is no longer the judge; persistence is named as not yet judged rather than claimed).
- [x] Verification design — evidence: failures name both sides — the run's value versus the preset's value, or the two healing gains and the skin readings they were measured from.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the recipe split keeps reading (list/map/read), writing (preset/setting) and the effect probe (limb) in separate one-call recipes, so no recipe both stages and judges.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-only change, so nothing to deploy; `.acceptance/runsettings-5` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
