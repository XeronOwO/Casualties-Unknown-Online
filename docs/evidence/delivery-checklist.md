# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the Worlds page's claim, its two label templates and the save that fills its rows are written in the rebuild ticket's row 38 section with the `zh.json` entries.
- [x] Native parity — evidence: the row asks the game for a save through the console `/save`, opens the window with `open-window`, clicks the game's own `tab.worlds`, and reads the page's own dump.
- [x] Native parity probe — evidence: `.acceptance/worlds-1` PASS 26.1 s — the page named 1 save point after the row's own cut, and the retention line carried its ceiling.
- [x] Entry mapping — evidence: `console /save` → cut commits → `open-window` → `click tab.worlds` → the window's page state says `World` → `cuo-ui-dump`.
- [x] Mechanism inventory — evidence: `cuo-ui-dump` (the page's own text), `virtual-key` (the player's Escape to close), plus the existing `open-window`/`click` actions.
- [x] Whole-family audit — evidence: the retention ceiling and the save-point count are the same family of "label carries no number"; both are parsed now, and the residual (no frame of the tab) is named in the ticket.
- [x] Self-check table — evidence: three questions answered in the ticket's row 38 section (a page with no run, the unread ceiling, the player's own operations).
- [x] Verification design — evidence: failures name the page tail, so a red reading says what the player saw.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the row takes its cut, opens the page and reads it; no recipe both stages a state and judges it.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-and-recipes change only, nothing to deploy; `.acceptance/worlds-1` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
