# Acceptance record — Remote inventory operations: run the native path end to end (matrix rows 7 and 8)

- Ticket: `remote-inventory-native-parity-rework` — verdict: **back to `todo/`** (row 7's favourite half
  `unproven`; row 7's combine, battery-LOAD and worn-item halves and row 8 all `pass`)
- Batch: `20261011-d` — this ticket is the batch's only ticket; scope and the pre-declared row criteria:
  `docs/evidence/acceptance/20261011-d-scope.md`
- Commit under test: `7c5646bd` (unchanged — no `src/` edit since) · Deployed artifact:
  `CasualtiesUnknownOnline.dll`, `ProductVersion` `0.1.0+7c5646bd…`, verified against `<game-dir>`
  (`tools/verify-deploy.ps1`, exit 0; `verify-deploy.txt`). The tree also carries this batch's fixture
  recipes (`tools/acceptance/recipes/item-census.cs` — new — and the `remote-gesture` /
  `item-provide` extensions), which the run used and which are committed with this record; the plugin
  identity is untouched by them.
- Run: 2026-10-11 13:49 → 14:17 · Host: physical machine (<owner> — the OWNER of the operated
  items) · Guest: first sandbox (<operator> — the OPERATOR) · Third client: alternate sandbox
  (<third peer> — in the world; rows 7 and 8 need no third-party view)
- Dependencies: `preflight.txt` (11 present, "a full two-client run is possible; the alternate third
  client is configured") and `session-environment-status.txt` (`active=cuo`, `game-running=false`,
  `swap-needed=false`), both captured before the clients started
- Artifacts: the batch directory under `acceptance-artifacts-dir`; every file cited below lives there

| # | Row (half) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 7a | combine — the two-item merge branch | machine | **pass** | the owner's two `bandage`s (`1117012477891` receiver, `1121307445187` dragged) were created by `item-provide mode=create` (`f6-host-create-bandage-1.json`, `…-2.json`) and released onto each other with the ring's own button list read first (`g17-guest-list-before-combine.json`: button 3 = slot 0, button 5 = slot 1). The operator logged `[RemoteIntent] CombineItems captured for item 1121307445187 of <owner> (… target item 1117012477891 …)` and the owner `[RemoteIntent] … CombineItems (item 1121307445187, … target item 1117012477891 …)` + `replayed native CombineItems on item 1121307445187` (`ev-guest-combine.txt`, `ev-host-combine.txt`; release answer `g18-guest-combine-bandage.json`, `calls: "none"`) |
| 7a′ | combine — the ammunition branch, i.e. a combine with an observable result | machine | **pass** | swapped the pair for a `smallmagazine` (`1159962150851`) and a `9mmround` (`1164257118147`) (`g20-*`); before the release the owner read `smallmagazine … ammo=Magazine/Pistol rounds=0/17` and the round sat in slot 1 (`g22-host-body-before-combine-ammo.json`). The operator captured `CombineItems (item 1164257118147 … target item 1159962150851 …)`, the owner replayed it and the round was destroyed — `[ItemTrace] op=18 item=1164257118147 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]` — and the owner's own after-reading is `rounds=1/17` with the round gone from the body (`g26-host-body-after-combine-ammo.json`, `ev-host-combine.txt`) |
| 7b | battery LOAD (R3) | machine | **pass (2/2)** | fixture: an `aed` (`1125602412483`) whose slot is emptied by the native unload and the `mediumbattery` the unload ejects (`1151372216259`, then `1155667183555`). Round 1: `LoadBattery captured for item 1151372216259 … target item 1125602412483` (`g8-guest-load-battery.json`), owner `the native load charged item aed from battery mediumbattery (the battery item was consumed).` + `replayed native LoadBattery on item 1151372216259` + `[ItemTrace] … origin=OnItemDestroyed … events=[Destroyed]`, and the aed reads `batt=True/mediumbattery` afterwards with the battery item gone (`g9-host-body-after-load.json`). Round 2 carries the unloaded state as its own reading — the owner's census between the two gestures is `aed … cond=0 batt=True/` (batteryType EMPTY) with the ejected `mediumbattery` in slot 4 (`g13-host-body-mid-unload-2.json`) — and then the same load chain, ending `aed … cond=1 batt=True/mediumbattery` (`g15-host-body-after-load-2.json`, `ev-host-battery.txt`, `ev-guest-battery.txt`) |
| 7c | worn-item path (the checklist's confirmation) | machine | **pass** | the owner's worn `autopump` (`1142782281667`, `item-wear slot=3` → `f9-host-wear-autopump.json` `worn: true`) renders in the operator's ring as its own button holding the proxy (`w12-list.json` button 0, `itemId 1142782281667`). Dropped into the world: operator `DropWearable captured for item 1142782281667 …`, owner `the wearable autopump left the owner's body into the world.` + `replayed native DropWearable …` + `[ItemDropped] autopump (id 1142782281667) at (3.0,480.3)` + `[ItemTrace] op=41 … FlushPendingDrop result=Committed(1)`, and the owner's body no longer carries it (`w14-drop-worn.json`, `w15-host-after-drop.json`, `ev-host-worn.txt`). Re-worn: the owner picked it back up natively (`w16-host-pickup-autopump.json`, slot 1) and the operator dragged it onto the ring's CENTRE — `WearItem captured for item 1142782281667 …`, owner `[SlotMoved] autopump (id 1142782281667) → slot -3 (Wear) — host fact broadcast.` + `replayed native WearItem`, and the census shows it on a limb again (`w19-rewear.json`, `w20-host-after-rewear.json`, `ev-guest-worn.txt`) |
| 7d | the favourite key over an item | machine | **unproven** | the store's own gate is `Input.GetKeyDown(favourite)` on the first `InvButton` the FRAME's UI casts contain, inside `HandleWhileDragging` (`reversing/…/PlayerCamera.cs:1745`), and in the remote view the ring is anchored to the displayed clone's screen position (CUO's own anchor), while the game turns the ring off when its x sits more than `600 * uiScale` from the pointer's x. The run's key hold and the ring's geometry were both driven and read (`g29-guest-hover-favourite.json`: ring staged so button 3 sits under the pointer, `castUnderPointer: 0`; `g31/g32`: `key=F`, `posted: true`, `heldAtEnd: true`), and the store path itself is proven live: at 14:06:17.934 the operator logged `[RemoteIntent] refused the favourite toggle on paincream: that display proxy is not the item this client is operating (no authoritative identity, or no open bracket) — the native call was skipped, no proxy was mutated.` — the game's own frame DID store `favourited` on the display proxy and CUO put it back and refused it, because that frame had no display-proxy drag bracket open (the staging error this run then fixed: `fav17-hover.json` stages a live proxy drag with the same button under the pointer). From then on the physical cursor sat outside the client window for every attempt (`fav52-list.json`: `mouse=(251,-168)` with the ring open at `(680,400)`; `fav22-*-list.json`: `mouse=(-374,620)`, `(-1137,-393)`), and the acceptance tooling deliberately never takes the pointer (`input` dependency, decision 237; `AcceptanceDriverGateTests` bans the cursor and `SendMessage` families), so the store frame could not be arranged. `fav7-host-before.json` records the owner's `paincream … favourited=False` as the intended starting value |
| 8 | held remote item, backpack closed, used from the medical panel | machine | **pass** | the operator's paincream proxy survived closing the ring (`r8-1-close.json` `radialOpen: false`; `r8-2-guest-after-close.json` still lists the proxy), the operator's own wound view was opened through the game's own `ToggleWoundView` and pointed at limb 2 by the game's own hit entry (`r8-3-wound-view.json`, `r8-4-wound-view-again.json` `limbLookingAt: 2`, `selectedLimbIndex: 2`, `selectedIsLocal: true`), and the release onto that limb's `WoundViewLimb` ran the game's own `HandleReleaseDragging` → `TryPerformSpecialUIAction` → `ApplyWoundItem(dragItem)` (`r8-5-medical.json`, `calls: "none"`, `dragAfter: "none"`). Operator: `[ItemUse] measured 10.000 ml from paincream's own native limb action.` + `ApplyToLimb captured for item 1134192347075 of <owner> (… limb 2 …)` + `[ItemUse] local body received 1 topical stack(s) from <owner> (limb 2).` Owner: `[RemoteIntent] <operator> → <owner> ApplyToLimb (item 1134192347075 … limb 2 …)` + `[ItemUse] <owner> used paincream (id 1134192347075) on <operator>; destroyed=False.` + `[ItemUse] local item 1134192347075 condition set to 0.90 by cross-player use.` — and the owner's own census reads `paincream … cond=0.9 liq=90/100` where it was `cond=1 liq=100/100` before (`r8-host-body-before.json` → `r8-6-host-after.json`; `ev-host-medical.txt`, `ev-guest-medical.txt`) |

## Limits

- **Post-batch ruling (2026-10-11, the user, after this record was written).** The favourite mark belongs
  to the LOCAL player: it is never an action applied to a remote player's item, and it must survive a save.
  That changes row 7d's criterion — the criterion this run judged against ("the favourite star follows the
  owner's own value") is superseded, and the landing cycle is
  `docs/backlog/review/favourite-marks-are-local-and-saved.md`. What this record contributes to that cycle is
  the mechanism reading below: where the native store is, why the run could not arrange its frame, and the
  proof that the store path itself fires.
- **Row 7d was not driven to its outcome, and the reason is a run capability, not a code doubt.** The
  native store needs the physical cursor over a ring button in the key's frame; the remote view's ring is
  anchored to the displayed clone (CUO's own anchor), and the cursor was outside the client window for
  every attempt after the one that proved the store fires. A user at the mouse would settle it in
  seconds; the acceptance tooling must not take the pointer, so this run could not.
- **Two fixture items were lost mid-run and the loss is this run's own doing.** While the row-7d
  attempts moved the owner's body to reachable spots, the owner was placed ~24 world units away and fell
  ~45 units; the game then dropped two of its carried items on its own —
  `[ItemTrace] op=65 begin item=1159962150851 origin=OnItemDropped` and `op=66 … item=1125602412483 …`
  at 14:05:37.015/.016, with `[ItemDropped] smallmagazine … at (29.7,440.6)` and `aed … at
  (28.7,437.5)` (`ev-host-lost-items.txt`), and no CUO intent involved (`ev-guest-drops.txt` holds no
  `DropItem captured` for either id in this session). Every row judged above was driven and read BEFORE
  that moment, and the later readings say so: the operator's clone of the owner fell to
  `host char data (3 items)` (`ev-guest-clone-counts.txt`).
- **Row 7c's worn-item button is rebuilt, not persistent.** The ring shows the owner's worn item only
  after the ring is (re)activated — the game populates the wear buttons in its own `UpdateWearables`,
  which runs when the ring object becomes active. A ring that was never closed shows the item's proxy
  without its button (`w6-list.json` has no autopump button; `w12-list.json`, taken after a close and
  reopen, has it). The row's own expectation — "a worn item of the owner shows up in the ring's wear
  slots" — is read on the rebuilt ring.
- **Row 8's limb effect is not a number.** The treated limb was healthy (`r8-7-guest-limb2-after.json`:
  `skinHealth 100, bleed 0, pain 0, components ""`), so the topical's effect shows as the operator's own
  receiving line (`local body received 1 topical stack(s) … (limb 2)`), not as a changed limb value; the
  dose side is numeric (10 ml measured, the owner's item 100 → 90 ml).
- **The fixture items were created, not found.** Every item the operated inventory holds — the bandages,
  the magazine and its round, the `aed`, the `mediumbattery`, the `paincream`, the `autopump` — was put
  on the owner's body by `item-provide` (the game's own `Utils.Create` + `Body.PickUpItem(force:true)`,
  or the native pickup), and two of them were taken out again by `item-provide mode=drop` to free the
  body's six slots. The gestures under test are the native ones; the wardrobe is scene setup.
- **One client was restarted mid-run.** The operator's client was quit and relaunched at 14:12 (its
  first session's log is `2026-10-11-13.log.gz` on that sandbox). No row was judged from the second
  session: rows 7a-7c and 8 are from the first, and row 7d's evidence is the first session's plus the
  second session's geometry readings, which are named as such.
- The third client was in the world for the whole run but was not used: rows 7 and 8 carry no
  third-party expectation (row 10 was judged in earlier batches), and this record makes no claim about
  its view.
