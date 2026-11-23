# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: row 21's claim and the game's own drinking loop, water amount, illness and refuse branch are written in the rebuild ticket's row 21 section with `Body.cs` and `FluidManager.cs` lines.
- [x] Native parity — evidence: the liquid is the game's own fluid grid (`SetLiquid`), the cadence is the body's own two-second loop, the amount is `Drink(12f)` and the refuse branch is the game's scream.
- [x] Native parity probe — evidence: `.acceptance/drink-1` PASS 24.1 s — thirst rises by a positive multiple of 12 with sickness rising and temperature falling, and refuse leaves thirst untouched.
- [x] Entry mapping — evidence: the row takes no key at all (drinking is automatic) and stages only the liquid in the cell the body already stands in.
- [x] Mechanism inventory — evidence: `liquid-fill` (write the fluid, do not drink) and the extended `body-effects` (thirst, sickness, temperature), plus the existing `body-effects` fields.
- [x] Whole-family audit — evidence: the old `world-drink` echo recipe is no longer called by this row; the ticket records that the fixture places the liquid instead of walking to a lake.
- [x] Self-check table — evidence: three questions answered in the ticket's row 21 section (the call's echo is no longer the judge; the body's own numbers are; the refuse branch is now judged).
- [x] Verification design — evidence: failures name the before and after thirst, the gain and the game's own 12, the sickness and temperature before and after, and the refuse comparison.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: staging the liquid (`liquid-fill`) and reading the body (`body-effects`) are separate one-call recipes, so no recipe both stages and judges.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-and-recipes change only, nothing to deploy; `.acceptance/drink-1` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
