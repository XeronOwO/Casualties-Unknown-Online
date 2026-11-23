# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the radiation row's claim, the world's own layer limit and the line's activation are written in the rebuild ticket's row 40 section.
- [x] Native parity — evidence: the row reads the game's own line object on both clients (`radiation-line-state`: `active`, `timeGone`, `lineY`) and writes only the world fields a player cannot.
- [x] Native parity probe — evidence: `.acceptance/rad-1` PASS 2.1 s — host fell 3.75 and member 3.92 over 1200 ms.
- [x] Entry mapping — evidence: `WaitForHostBodyAsync` → `layer-limit-set` 0.5 s → `layer-time-set` 600 s → both lines active → two reads 1200 ms apart on each side → the limit restored to 10800 s.
- [x] Mechanism inventory — evidence: `regulation-line-state` readings on both clients, `layer-limit-set`, `layer-time-set`.
- [x] Whole-family audit — evidence: the absolute-height gap (`< 40`) and the `timeGone > 0` check are the same family of "a number exists"; both are replaced by deltas over one window, and the fixture shortcut is declared in the ticket.
- [x] Self-check table — evidence: three questions answered in the ticket's row 40 section (height gap, clock, declared shortcut).
- [x] Verification design — evidence: a red names both sides' before/after readings and the two falls.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the row only reads the line; the two field writes are separate recipes, so no recipe both stages a state and judges it.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-and-recipes change only, nothing to deploy; `.acceptance/rad-1` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
