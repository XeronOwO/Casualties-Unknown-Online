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

## The menu, read on a frame with the staged input cleared (2026-11-02, 00:05)

Clearing the staged pointer and buttons (`virtual-input mode=clear-buttons` then `mode=clear`) before opening the menu removes the item wheel that had been drawn over it, and the frame then shows the whole surface: the trader's two lists with prices and weights, the portrait action column (speech, heart, pen, a nap action, refuse, back), the player's money, the give-to-trader drop area, the trader's name, and the player's own inventory beside it. Rows 8 and 9 therefore have their surface confirmed by a frame, not only by the `menuActive` flag.

What each row still needs, read from the scope page's own definitions:

- **Row 8** is "a living player at a friendly trader revives a dead teammate in place and grants 1–3 trader-stock items; the peer view and the saved snapshot refresh" — so it needs a DEAD teammate beside a friendly trader, plus a snapshot read on the peer. That is a fixture this session did not build.
- **Row 9** is "the one-shot swing presentation is replayed on the clients that did not run it" — it needs the player to swing at the trader and a peer-side reading of the swing message. The swing does not exist as a recipe yet (`body-call` has no attack kind; the game's own entry is `Body.UseItemInHand` with an empty hand, which falls through to `Attack`), so the next pass adds that kind and a count of the swing messages on the peer.

### Row 9's replay half read: a swing reaches the peers (2026-11-02, 00:20)

`body-call` gained an `attack` kind that calls the game's own `Body.UseItemInHand` with an empty hand — the path a player's left-click takes, and the one `entity-attack` refuses to fabricate. With the peer's receive watcher running (`receive-watch3`, which records the kind, the clip and the owner of every character-sound message), one swing on the host produced on the peer:

    [AttackSwing:BSSwing4@<host>] [AttackSwing:BSSwing4@<host>]

So the one-shot swing presentation IS reported and replayed on a client that did not run it — row 9's mechanism, read directly. What the row still needs is the HOSTILE half: hitting the trader until it turns hostile and reading that swing the same way, which is one more gesture after this instrument (the trader's own state is already exposed by `trade-open`'s `hostile` field).

### What makes a trader hostile, read from its own script (2026-11-02, 00:45)

Row 9's hostile half needs one specific state, and `TraderScript` says exactly which: `hostile` is `hostility >= 100f`, and `hostility` moves only two ways — the trader's own evaluation adds 50 when **the player is holding a weapon tagged `gun` in the HAND slot** (`if (this.body.HoldingItem(this.body.handSlot) && …HasTag("gun")) this.hostility += 50f;`), and the hug/refuse interaction sets it to 100 outright when `reputation < 30f`.

Two attempts in this session show what is NOT enough: punching the trader (`body-call kind=attack`, six swings with the body aimed at it by `targetLookPos` and the trade menu closed) leaves its `buildHealth` at 1250 and the menu opening normally, and giving a `shotgun` (`/give @s shotgun` answering `placed in slot 0`) while standing beside the trader changes nothing either — the check reads the HAND slot, and the given weapon sits in a backpack slot.

So the next pass is precise: put a gun-tagged weapon into the hand (the game's own switch/hand slot), stand at the trader, and read `hostility`/`hostile` until the menu refuses; then the swing is already covered by the replay reading above (`[AttackSwing:BSSwing4]` arriving on a peer).