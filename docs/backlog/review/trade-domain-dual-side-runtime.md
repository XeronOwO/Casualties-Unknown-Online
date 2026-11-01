# Trade domain dual-side runtime pass

- Status: Review (batch `20261003-e`: the row table written in the scope page was judged — rows 1–7 pass, the whole interactive trade family including the refused-purchase rollback and the 5 s fallback heal; rows 8 (recruit) and 9 (hostile swing) stay `unproven` with their fixtures named — the ticket stays open)
- Acceptance record: `docs/evidence/acceptance/trade-domain-dual-side-runtime-20261003-e.md` (batch scope: `docs/evidence/acceptance/20261003-e-scope.md`)
- Priority: High
- Category: Final acceptance
- Reference: original backlog #59 / #93

Dual-side runtime pass for the trade domain.

## Rows 8 and 9: what the batch learned about the menu (2026-11-01, 14:45)

`trade-open mode=open` answered `menuActive=True` on a world with twenty traders (`traderCount=20`, `buildHealth=1250`), which is the state rows 8 and 9 need. The FRAME taken straight after showed the player's item wheel instead of the trade menu — and reading the game's own toggle explains it without a defect: `PlayerCamera.ToggleTradeMenu` (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs`) only flips `tradeMenu.SetActive(...)`; the wheel is a different surface, drawn while the interact key is held, and the batch's own earlier staged press had left it up. So the menu was open behind the wheel, and the reading to take next is a frame taken with nothing held, then the menu's own buttons.

That makes rows 8 and 9 reachable with the instruments that now exist: `ui-click` dispatches a pointer click on the game's own UI (the recruit/haggle buttons are buttons), and the hostile-swing row needs the trader hit — `trade-open` already answers the trader's coordinates and the player's own attack is `Body.Attack`/`UseItemInHand`. Neither was run: the machine lost its Steam connection at this point (`Lobby creation failed: k_EResultNoConnection`), so no session could be created for the reading. That is recorded as the blocker this row actually has, not as a conclusion about the trade domain.

## The trade menu IS drawn, read from a frame (2026-11-01, 16:30)

The earlier note ("the frame showed the item wheel") explained the wheel but not whether the menu renders. It does: with the staged pointer's button released and `trade-open mode=open` answering `menuActive=True`, the frame shows the trader's panel — the seller's two lists (its normal stock with prices and weights, and its random stock below), the portrait action column (speech, heart, pen, a nap action, refuse, return), the player's money, the "give to the trader" drop area, and the player's own inventory on the right. The item wheel is drawn OVER the right half because the batch's own staged interaction key is still held; it is a different surface, not a replacement.

So rows 8 and 9 have their surface: the recruit action belongs to that portrait column or the menu's own option list, and the hostile swing is a hit on the trader after the menu is closed. The next reading is which of those controls carries the recruit action (a click through `ui-click` at its rect, then the trader's own state) and the swing itself.