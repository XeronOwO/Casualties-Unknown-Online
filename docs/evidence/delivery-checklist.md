# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: row 20's claim, the trader's own use call, its credit field and its purchase gate are written in the rebuild ticket's row 20 section with `TraderScript.cs` lines.
- [x] Native parity — evidence: the screen opens through the game's own `TraderScript.OnUse` → `PlayerCamera.ToggleTradeMenu`, prices come from `TraderScript.ItemPrice`, credit from `valueGiven`, purchases from `TryPurchase`.
- [x] Native parity probe — evidence: `.acceptance/trader-9` PASS 1.9 s — an unaffordable purchase is refused with bag and stock unchanged, a give raises the credit 0 → 15, and the purchase spends exactly the price (15 − 11 = 4) while taking the entry out of stock (13 → 12) and adding one to the bag (4 → 5).
- [x] Entry mapping — evidence: the row takes the game's screen state (`tradeMenu.activeSelf`) as the entry, and the recipe sets the same `currentTrader` field the pointer path sets, because the pointer path reads the native cursor the driver cannot move.
- [x] Mechanism inventory — evidence: `trader-use` (open the screen through the game's call), `trade-drive` `mode=list/give/buy` (stock, price, credit, purchase), plus the existing `stage-bind` used by the row's neighbours.
- [x] Whole-family audit — evidence: the pointer half and the missing screen capture are named as residuals in the ticket; no other trader row exists to align.
- [x] Self-check table — evidence: three questions answered in the ticket's row 20 section (real screen state, real prices and credit, real refusals).
- [x] Verification design — evidence: failures name both sides — the credit against the price, the bag count before and after, and the stock count before and after.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: opening the screen (`trader-use`) and trading (`trade-drive`) are separate one-call recipes, so no recipe both stages and judges.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-and-recipes change only, nothing to deploy; `.acceptance/trader-9` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
