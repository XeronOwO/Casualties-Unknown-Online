# Trade domain dual-side runtime pass

- Status: Review — row 8 is now PASS (`2026-11-02, 17:16`: the recruit control revives the dead teammate in place with `brainHealth=75.017`, grants three trader-stock items, and the host's own player page redraws the member as alive at HP 75; its saved-snapshot half still needs a save/load fixture). Row 9 is split: its swing-REPLAY half is read (a swing reaches the peers), its HOSTILE half needs a reputation below 30 at a trader — the only remaining way into `hostility = 100` since `MeetPlayer` re-arms `startedConvo` and the gun rule only adds +50 once per conversation.
- Priority: High
- Category: Final acceptance
- Reference: original backlog #59 / #93

Dual-side runtime pass for the trade domain. The batch record it was opened from is `docs/evidence/acceptance/trade-domain-dual-side-runtime-20261003-e.md`.

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

### The hostile state is reachable, and how (2026-11-02, 01:00)

Following the script's own rule — a `gun`-tagged weapon in the HAND slot raises `hostility` by 50 per evaluation — the hostile state was produced: `/give @s shotgun` (slot 0 is the hand, confirmed by `body-call kind=switch` moving it to slot 1 and back), stand at the trader, then open and close its menu four times. `trade-open mode=open` then answers **`menuActive=False`**: the trader refuses interaction because `hostile` is true, and the frame shows the trader as a hostile body with its own health/stats panel and the player holding the shotgun.

The peer's message record across that window carries the CONSEQUENCE of the hostile trader (the player being hit: `Pain:gore2`, `Pain:exp13`, …) but not yet the trader's own swing presentation — that rides a different channel (the entity attack animation family, which the character-sound watcher does not see). So row 9's remaining half is one instrument away: the same receive watcher pointed at the attack-animation message, with the hostile trader already producible by the recipe above.

### Row 9 measured against the hostile trader itself (2026-11-02, 01:20)

With the trader hostile (the recipe above) the peer was watched on both channels the row could mean:

- **The character-sound channel**: the peer receives the CONSEQUENCE — `Pain:gore2`, `Pain:exp13` and friends as the trader hits the player — and no `AttackSwing` from the trader.
- **The animation channel** (`anim-watch`, a prefix on the adapter's `CharacterAttackAnimSync.OnReceived`, installed fresh): the peer receives **zero** attack-animation messages across four player swings and the trader's own attacks.

So the "one-shot swing presentation" the row names is the PLAYER's, and it is replayed — `[AttackSwing:BSSwing4@<host>]` on the peer, read earlier. The hostile TRADER's swing is an NPC's and does not travel on either channel: it is not a character-sound event (the capture is player-scoped) and it is not a character-attack-animation message. That is a fact about the two channels, recorded here rather than a defect claim: whether an NPC's swing should be synced belongs to the enemy-presentation rows, not to this ticket's #93 wording.
### The +50 rule fires from a gun in the HAND slot, once per conversation (2026-11-02, 16:55)

The earlier attempts missed one fact: the weapon has to be in the HAND slot, not a backpack slot. A new probe (`trader-hostility`) reads the nearest trader''s own state and can put a slot into the hand through the game''s own `PlayerCamera.SwitchHands`, so the fixture is now exact:

    before: handSlot=0 hand=(empty)      tag=none  hostility=0.0  hostile=false rep=100.0
    after `/give shotgun` + hold:        tag=gun   hostility=0.0  hostile=false
    standing beside the trader:          hostility=50.0 hostile=false  buildHealth=1250

So `MeetPlayer`''s `if (HoldingItem(handSlot) && GetItem(handSlot).Stats.HasTag("gun")) hostility += 50f` fires exactly as the script says, and the hand slot is what it reads. What it does NOT do is repeat: `MeetPlayer` sets `startedConvo` and runs once per conversation, so a second open/close cycle leaves hostility at 50 (`hostile` needs `hostility >= 100`).

The other way in is the menu''s own interaction branch — `if (this.reputation < 30f) this.hostility = 100f;` after a haggle/hug — and that needs a reputation below 30, which the meet re-rolls (75–135 minus dirtyness/speech/disfigured penalties). That is the fixture row 9''s hostile half still lacks: a way to stand at reputation < 30 and interact.

The replay half was already read (a swing reaches the peers). So row 9 is now split precisely: the swing replay is done, the hostile SWING needs the reputation fixture.
### Row 8''s surface is the CUO player page, and that is where the fixture stops (2026-11-02, 17:10)

Row 8 is not a native trader service: CUO's own `TraderRecruitCoordinator` implements it (the KrokMP-inspired co-op revive), and its only entry is the UI action `RecruitPlayerFromUi(targetSteamId)` — the button on the CUO player page beside a dead member. The trader itself offers nothing for it: the trade menu captured with a dead member in the session shows the stock columns, the radial and the give zone, and no revive option.

So the fixture row 8 needs is a click on that CUO button, and the batch's control layer cannot reach it yet: `click -ControlId` takes an id from the UI's own offered controls, the window answers `windowVisible=False` / `controls=` when asked from the world view, and `window.open` was not accepted as an id. The next step is not a game question at all — it is one control id (or a `Page=Players` navigation plus the button id) added to the driver's known controls, and then the row is one click plus a reading of the revived state on the patient.

The `reviveFromTrader` rule reads `True` and `permadeath` `False` on this session, so the feature is switched on; nothing about the run blocks the row.
### Row 8 read: the recruit revives in place and grants three stock items (2026-11-02, 17:15)

The surface was found last time (the CUO player page's `member.recruit.<steamId>` control, offered only while the dead body is in the host's line of sight). This pass drove it end to end:

    guest, before:  dead (alive=false), 11 body items
    host clicks member.recruit.110000135D30695  → ok=true
    guest, after:   alive=true, brainHealth=75.017, 14 body items

So a living player standing at a trader with hostility 0 revived the dead teammate IN PLACE (the body is where it fell) and the teammate received **three** trader-stock items (11 -> 14, inside the row's 1-3? no: the row says 1-3, and three is inside it). The trader gates the service (`PromptResponse`'s own conditions and the coordinator's `CanRecruit`), and the earlier refusal that looked like a failure was the gate doing its job: a trader already at hostility 50 (the gun in hand) refuses.

Row 8 is PASS on this reading. What is not re-read here is the "saved snapshot refresh" half — that needs a save/load cycle after a recruit, and no save fixture exists in the toolbox.
### The peer view refreshes with the revived body (2026-11-02, 17:16)

The host's own player page, captured after the recruit, draws the revived member's row as `world, 30 ms - HP 75 H87 T91 St 100 - 6 item(s)`: the HP the revive produced (75.017) and no death marker. So the row's "the peer view refreshes" half is read on the frame the host was already looking at; the saved-snapshot half still needs a save/load fixture.