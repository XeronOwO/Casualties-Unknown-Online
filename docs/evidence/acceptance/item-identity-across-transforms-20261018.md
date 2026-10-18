# Acceptance record — an item's identity across a transform

- Ticket: `item-identity-across-transforms` — verdict: **back to `todo/` with `- Status: Todo — Rejected
  (batch `20261018-u`: row I1 fails — a cross-player apply ends the item row 21 ms after the applied row is
  committed; row I4's live object is lost although its row says carried)`**. Rows I2, I3, I5, I6 and I7 pass;
  I1 fails; I4's row passes on the id/location and its runtime half fails. The batch's shared setup is
  [`20261018-u-scope.md`](20261018-u-scope.md).
- Batch: `20261018-u` — tickets `heal-cheat-command-state-reset`, `item-identity-across-transforms`.
- Commit: `a3dff483` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+a3dff4838f2b8abab19d2b56e66906148acbba65` (`tools/verify-deploy.ps1`, exit 0 before the clients
  started).
- Run: 2026-10-18 12:57 → 13:17 (+08:00), by the clients' own log clock · Host: physical machine · Member:
  `Steam1` sandbox · Bystander: `Steam2` sandbox.
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl` (three endpoints),
  `input`, `logs`, `artifacts`, plus this batch's fixture mod (below).
- Artifacts: the `i*`/`j*` readings, `x-*.txt` extracts and `fixture-mod/` in the batch directory named by
  `acceptance-artifacts-dir`; ids are written `<host>`/`<member>`/`<bystander>` here.

**The row table was written before the run** (the ticket states its expectations in prose): the claims below
are the ticket's ruling — an applied item keeps its instance id and gains a location on the limb, a removal
hands the same item back, a favourite mark follows it, a mod finds it by id, and the row survives a
save/reconnect — turned into rows so a verdict cannot be reverse-engineered from the result.

**The reader.** The deployed example mod declares no `ReadGameState` and never calls `TryGetItem`, so the
batch deployed its own fixture mod — `fixture-mod/ItemReadMod.cs`, a real CUO mod declaring
`ReadGameState | RegisterCommand` — whose `/idread <instanceId>` answers from its **own**
`IModContext.GameState.TryGetItem`. It was on all three clients (the `/idread` smoke ran on each before the
rows) and was removed at the close. This is a declared substitution, named in the scope page.

| # | Row (derived from the ticket's ruling) | Class | Verdict | Evidence |
|---|---|---|---|---|
| I1 | Cross-player apply: a carried splint (id X) applied by the host to the member's limb keeps X and becomes `Applied` on that limb | machine | **fail** | the host carried splint X (id `1086947706819`, slot 0, `/data get` carried by `<host>` revision 3, `i01`-`i03`) and applied it through the remote medical view release onto the member's limb 9 (`i07`-`i09`: `opened=true`, display limb `SplintLimb`, release `ok=true dragAfter=none`); the member's limb and the host's display copy carry `SplintLimb/splinted=true` (`i10`, `i10b`), but the item row is **not** applied: `/data get id X` on the host, the member and the bystander reads `terminal (destroyed or consumed)` revision 5, condition 0 (`i11`-`i13`), and `/idread X` on all three reads `"location":"Terminal","owner":0,"limb":-1` (`i14`); the host's inventory no longer holds X (`i16`) and its mark ledger dropped the mark (count 0, `i15`). The log (`x-i1-apply-lines.txt`) carries the applied commit and, **21 ms later**, the item's own end: `13:07:51.815 [ItemUse] <member> installed splint (id X) of <host> on limb 9 — the item keeps its id…` then `13:07:51.816 [ItemUse] local item X destroyed by cross-player use.` and `13:07:51.836 [ItemTrace] op=2 … origin=OnItemDestroyed result=Committed(1) events=[Destroyed]` |
| I2 | Local apply: the member applies a carried splint to its own limb through the native wound-view release; the id is kept and the item becomes `Applied` | machine | **pass** | the member created splint Y (id `9492956821`, slot 0, `i30`), opened its own wound view at limb 12 (`i34`: `limbLookingAt=12 selectedLimbIndex=12`), released `local0` onto it (`i35`: `ok=true limb=12 selectedIsLocal=true dragAfter=none`), and the limb reads `SplintLimb/splinted=true` (`i36`); `/data get id Y` on the host, the member and the bystander reads `applied to <member>, limb 12` revision 3, condition 1 (`i37`), and `/idread Y` on all three reads `"location":"Applied","owner":<member>,"limb":12` (`i38`); the member's log: `[LimbItem] the local limb action of splint (id Y) installed the item on limb 12; it keeps its id and now lives on that limb.` (`x-i2-apply-lines.txt`) |
| I3 | Cross-player removal: the host takes a properly applied splint off the member's limb through the remote medical special; the item becomes `Carried` by the host, same id, condition carried over | machine | **pass** | a locally applied splint Z (id `13787924117`) was on the member's limb 13 (`i61`-`i63`: the host's `/data get` reads `applied to <member>, limb 13` revision 3); the host opened the view on the member, read the display limb 13 `SplintLimb`, dispatched the removal (`i70`-`i72`) and the member's limb cleared (`i73`: `components:"" splinted:false`); `/data get id Z` on the host, the member and the bystander reads `carried by <host>` revision 4, condition 1 (`i74`), the host's item census holds Z in slot 0 with condition 0.9941 (`i75`), and the host's log: `[MedicalOps] handed removed splint (id Z) to <host> in slot 0 — the item kept its id across the round trip.` (`x-i3b-removal-lines.txt`) |
| I4 | Local removal: the member takes the splint off its own limb through the native `WoundSpecialAction` call site; the item becomes `Carried` by the member, same id | machine | **pass on the row; its live-object half fails (finding 3)** | the member's wound view pointed at limb 12 (`i40`), the native call site ran (`i41`: `splintBefore=true splintAfter=true splintedAfter=false`, the component object gone in the next frame) and the limb reads `components:""` (`i42`); `/data get id Y` on all three reads `carried by <member>` revision 5, condition 0.9897625 — the same id and the condition the component held (`i43`); `/idread Y` on all three reads `"location":"Carried","owner":<member>,"limb":-1` (`i44`); but the member's own carried inventory does not hold the item (`i45`, `i46`) and an instance-id scan of the scene does not find it (`i50`), while the log shows the game's own pickup of the taken-off item refused by the host and the copy killed by the world-item reconcile (`x-i4-takeoff-lines.txt`: `ItemPickup … was refused by the host — …`, `Kernel command rejected … ItemPickup refused (InvalidTransition)`, `[Reconcile] 252 items: killed 1 …`) |
| I5 | A favourite mark follows the item across both transforms; the item's own flag is the same fact when it becomes the marker's own | machine | **pass** | the host's mark on the member's splint Z (id `13787924117`, owner `<member>`) is held while the item is carried by the member (`i48`/`i49`: count 1, the mark names Z) and while it is applied on the member's limb 13 (`i65`: count 1, same mark); after the cross-player removal the item is carried by the host and the mark is spent by the designed adoption: `[FavouriteMark] item splint (id Z) is the local player's own now — the local mark became its own favourite flag.` (`x-i3b-removal-lines.txt`), the ledger reads count 0 (`i76`) and the item's own flag reads `favourited:true` in the host's census (`i75`). The member's own mark on its own item was spent the same way the moment the item was already the member's (`x-i2-apply-lines.txt`, 13:09:46) — the ruling's own adoption, not a loss |
| I6 | A mod reads the applied item by id on every role: the operator, the patient and the bystander that did nothing | machine | **pass on the read surface; it reads `Terminal` for I1's transform** | the fixture mod's `/idread` answered on all three roles at every stage: `Applied` with owner and limb for the local apply (`i38`, and again `i64` on the bystander for the second apply), `Carried` with the same id after the take-off (`i44`), and `Terminal` after the cross-player apply (`i14`) — the read follows the row the framework holds, which is exactly what makes I1's failure visible; after the reconnect the rejoined member and the bystander read `Applied` for the saved splint (`j23`, `j25`) |
| I7 | The applied row survives a save/reconnect with its id and location | machine | **pass** | a splint W (id `18082891413`) was applied on the member's limb 14 (`j01`-`j06`: `/data get` applied to `<member>, limb 14` revision 3, the host's stored copy limb 14 `SplintLimb`); `/save` committed a mid-run snapshot (`x-save-lines.txt`) and the archive's own `items.json` carries the row — `instanceId 18082891413, revision 3, location.kind 5 (Applied), owner <member>, limbIndex 14, condition 1` (`x-save-items-applied.txt`) — while the archive's character file carries the healthy character and the limb 14 component (`x-save-character.txt`); the member quit, relaunched, rejoined the running lobby and re-entered (`j11`-`j15`); after the restore its own client reads limb 14 `SplintLimb/splinted=true` (`j21`), `/data get id W` on the host and the rejoined member reads `applied to <member>, limb 14` revision 4 (`j22`, `j24`), `/idread W` on the rejoined member and the bystander reads `"location":"Applied"` (`j23`, `j25`), and the host's stored copy still carries the component (`j27`) |

## Findings

**1 — The cross-player apply ends the item row right after committing it (I1).** The applied relocation is
committed by the host's own outcome handler (`PlayerItemActionOutcomeService.HandleOutcome`, the
`BecameLimbComponent` branch → `PlayerItemUseCommit.CommitItemAppliedToLimb`) — the log's
`installed … the item keeps its id` line is that commit — but the operator's local item object is also
destroyed by the cross-player result (`PlayerInteractionApply.OnPlayerItemUseReceived`:
`[ItemUse] local item … destroyed by cross-player use`), and the generic `Item.OnDestroy` hook reports that
destroy as the item's END. `ItemPatches.ItemOnDestroyPatch` silences a destroy only for the craft scope, a
framework claim, or `LimbAppliedItemOrigin`, and only the LOCAL apply adds that marker
(`LimbItemTransform.OnItemAppliedToLimb`, the `PlayerCamera.ApplyWoundItem` choke point) — the cross-player
apply adds nothing, so its destroy commit lands 21 ms after the applied row and overwrites it with
`Terminal`. The target's limb keeps the component (I1's `SplintLimb` read), so the two carriers diverge.

**2 — The rowless component then refuses its own removal (the consequence of finding 1).** The member's
limb 9 carries `SplintLimb` while the item row is `Terminal`; the host's cross-player removal is started and
then refused by name:
`[MedicalOps] refused the SplintRemoval on limb 9 of <member>: the limb carries a component whose template is
splint, but no applied item row names that limb and definition — the item's id is unknown, so nothing was
moved and the component stays.` (`x-i3-removal-lines.txt`, `i20`-`i23`: the limb still `SplintLimb`, the row
still `Terminal`). The save carries the divergence too: limb 9 `splinted=true, components=1` in the archive's
character file (`x-save-character.txt`).

**3 — The local take-off relocates the row but loses the live item (I4's runtime half).** The component's
own `TakeOff` instantiates the item, stamps the applied id and reports the removal
(`[LimbItem] … came off as item Y; the item kept the id it had while installed`), and the row becomes
`Carried` by the member — but the game's own `AutoPickUpItem` inside `TakeOff` also reports a pickup of that
id, which the host refuses (`ItemPickup refused (InvalidTransition)`: the row is already carried by the same
owner), and the world-item reconcile then kills the local copy
(`[Reconcile] 252 items: killed 1 …`) — the member's carried inventory does not hold it (`i45`, `i46`) and an
instance-id scan finds no live object (`i50`). The divergence is durable: the later restore's transfer-table
merge drops one entry with no placement (`cannot place 1 transfer-table item(s) … They are dropped by name`,
`x-restore-lines.txt`). The row's id and location are right; the player does not hold the item.

**4 — What the run proves about the model itself.** The local apply and the local take-off keep the id and
move the location (`Applied` → `Carried`), every role's `/data get` and the deployed mod's `/idread` agree,
the cross-player REMOVAL of a properly applied row hands the same id to the operator with the condition, the
favourite mark follows the item and is adopted exactly when it becomes the marker's own, and the applied row
survives both the save archive and a member reconnect. The failure is the cross-player apply's own destroy
report, not the model, the wire or the read.

## Residuals for the user

None. Every judged fact is a console answer, a client read, an archive file or a log excerpt.

## Limits

- **One run, and the fixture is this batch's own mod**, not the deployed example mod: the row's "a mod that
  recorded the id still finds it" is proven by a real mod calling the public `IModGameState.TryGetItem`, but
  that mod is the acceptance's, not a third-party sample. Its source is the record's artifact directory.
- **The cross-player apply's ordering is read on the operator's client** (the host's log carries both the
  commit and the destroy); the target's side is the applied limb and the row it receives.
- **The taken-off item's scene fate is read through the carried census, an instance-id scan and the
  reconcile/restore log lines**, not by a frame; the log line that killed it is named.
- **The save/reconnect is a rejoin of the running session**; the archive was read directly, not reloaded by a
  Continue. The archive's contents and the rejoin's restored row are both read.
- **The item-based medical path judged is the limb-tool family** (apply/removal); the bandage's native
  minigame was not driven.
- **The third client's own kernel read is the bystander evidence**; its `CharacterDataStore` carries no
  member's saved character, so no third-peer saved-copy claim is made.
