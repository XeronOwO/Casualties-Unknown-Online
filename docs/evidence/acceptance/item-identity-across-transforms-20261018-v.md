# Acceptance record — an item's identity across a transform

- Ticket: `item-identity-across-transforms` — verdict: **moved to `done/`**. All seven rows pass on the
  fixed artifact (`0.1.0+ef05371c`); the batch's shared setup is [`20261018-v-scope.md`](20261018-v-scope.md).
- Batch: `20261018-v` — tickets `item-identity-across-transforms`, `heal-command-limb-and-hollow-restore`.
- Commit: `ef05371c` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+ef05371cc1e0996aa8bab308f066a5e6faa9e773` (`tools/verify-deploy.ps1`, exit 0 before the clients
  started).
- Run: 2026-10-18 13:59 → 14:12 (+08:00), by the clients' own log clock · Host: physical machine · Member:
  `Steam1` sandbox · Bystander: `Steam2` sandbox.
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl` (three endpoints),
  `input`, `logs`, `artifacts`.
- Artifacts: the `i*`/`j*` readings, `x-*.txt` extracts and `fixture-mod/` in the batch directory named by
  `acceptance-artifacts-dir`; ids are written `<host>`/`<member>`/`<bystander>` here.

**The row table was written before the run** (the ticket states its expectations in prose): the claims below
are the ticket's ruling — an applied item keeps its instance id and gains a location on the limb, a removal
hands the same item back, a favourite mark follows it, a mod finds it by id, and the row survives a
save/reconnect — turned into rows so a verdict cannot be reverse-engineered from the result. The ticket's own
record of the fix (its "Fix landed" section) names the two defects this run re-tests and the third defect the
review found.

| # | Row (derived from the ticket's ruling) | Class | Verdict | Evidence |
|---|---|---|---|---|
| I1 | Cross-player apply: a carried splint (id X) applied by the host to the member's limb keeps X and becomes `Applied` on that limb | machine | **pass** | the host created splint X (id `1099832608707`, slot 0, `i01`) and read it carried (`i03`: `carried by <host>, slot 0`, revision 3); it marked X (`i04`: countAfter 1) and opened the remote medical view on the member's limb 9 (`i07`: `opened=true viewOpen=true`, display limb no splint in `i08`), releasing X onto it (`i09`: `item=splint limb=9 ok=true dragAfter=none`); the member's limb 9 reads `SplintLimb/splinted=true` (`i10`); **the item row is applied on all three**: `/data get id X` reads `applied to <member>, limb 9` revision 5, condition 0.9997316, `Favourited:true` (`i11-*`), and `/idread X` reads `"location":"Applied","owner":<member>,"limb":9` (`i14-*`); the host's inventory no longer holds X (`i16`, count 3) and its mark ledger reads 0 (the mark became the item's own flag, `i15`). The host log (`x-i1-apply-lines.txt`) carries `installed splint (id X) … the item keeps its id and now lives on that limb` and then `local item X destroyed by cross-player use — the item did not end: it is installed on the target's limb and keeps its id.`, and **no** `OnItemDestroyed … events=[Destroyed]` trace after it — the pre-fix failure (a Terminal row 21 ms after the commit) is gone |
| I2 | Local apply: the member applies a carried splint to its own limb through the native wound-view release; the id is kept and the item becomes `Applied` | machine | **pass** | the member created splint Y (id `9492956821`, `i30`), read it carried (`i33`), marked it (`i31`: countAfter 1), opened its own wound view at limb 12 (`i34`: `viewOpen=true limbLookingAt=12 selectedIsLocal=true`) and released `local0` onto it (`i35`: `ok=true limbLookingAt=12 selectedLimbIndex=12 dragAfter=none`); the limb reads `SplintLimb/splinted=true` (`i36`); `/data get Y` on all three reads `applied to <member>, limb 12` revision 3 (`i37-*`) and `/idread Y` reads `"location":"Applied"` (`i38-*`); the member's log: `[LimbItem] the local limb action of splint (id Y) installed the item on limb 12; it keeps its id and now lives on that limb.` (`x-i2-apply-lines.txt`) |
| I3 | Cross-player removal: the host takes a properly applied splint (X) off the member's limb through the remote medical special; the item becomes `Carried` by the host, same id, condition carried over | machine | **pass** | the host opened the view on the member and the display limb 9 read `SplintLimb` (`i71`); the special dispatched (`i72`: `started=true`), the member's limb 9 cleared (`i73`: `components:"" splinted:false`); `/data get X` on all three reads `carried by <host>, slot 0` revision 8, condition 0.9792694 (`i74-*`) and `/idread X` reads `"location":"Carried","owner":<host>` (`i74-*` idread); the host's census holds X at condition 0.9767 (`i75`), and the host log: `[MedicalOps] handed removed splint (id X) to <host> in slot 0 — the item kept its id across the round trip.` (`x-i3-removal-lines.txt`) |
| I4 | Local removal: the member takes the splint off its own limb through the native `WoundSpecialAction` call site; the item becomes `Carried` by the member, same id, **and is a live object in the member's own inventory** | machine | **pass** | the member's wound view pointed at limb 12 (`i40`), the native call site ran (`i41`: `splintedBefore=true splintedAfter=false`, the component gone), the limb reads `components:""` (`i42`); `/data get Y` on all three reads `carried by <member>, slot 0` revision 6, condition 0.9792261 (`i43-*`, the removal report now carries the revived object's own slot and condition) and `/idread Y` reads `"location":"Carried","owner":<member>` (`i44-*`); the member's carried inventory holds Y (`i45`, `i46`: condition 0.9792) after several reconcile cycles (more than one 5 s keyframe), and the member log carries the `[LimbItem] … came off as item Y` line with **no** refused pickup, no `surfacing item reject` and no reconcile `killed` line (`x-i4-takeoff-lines.txt`) |
| I5 | A favourite mark follows the item across both transforms; the item's own flag is the same fact when it becomes the marker's own | machine | **pass** | the host's mark on the member's splint Z (id `13787924117`, owner `<member>`) is held while Z is the member's (`i48`: countAfter 1, `i49`: count 1) and while Z is applied on the member's limb 13 (`i65`: count 1; the rows read applied on all three, `i63`/`i64`); after the cross-player removal Z is carried by the host (`i74-*`), the mark is spent by the designed adoption — the host log: `[FavouriteMark] item splint (id Z) is the local player's own now — the local mark became its own favourite flag.` (`x-i3b-removal-lines.txt`) — the ledger reads 0 (`i76`) and the item's own flag reads `favourited=True` in the host's census (`i75`). The host's own mark on its own X was adopted the same way at the apply: the applied row reads `Favourited:true` (`i11-*`) and the ledger 0 (`i15`) |
| I6 | A mod reads the applied item by id on every role: the operator, the patient and the bystander that did nothing | machine | **pass** | the fixture mod's `/idread` answered on all three roles at every stage: X `Applied` (`i14-*`), Y `Applied` (`i38-*`), X `Carried` after the cross-player removal (`i74-*` idread), Y `Carried` after the local take-off (`i44-*`), and W `Applied` after the reconnect (`j23-*`) — always the same id, owner and limb the `/data get` row names, read from the deployed mod's own `IModContext.GameState.TryGetItem` |
| I7 | The applied row survives a save/reconnect with its id and location | machine | **pass** | a splint W (id `18082891413`) was applied on the member's limb 14 (`j01`-`j05`: `/data get` applied to `<member>, limb 14` revision 3, the member's limb 14 `SplintLimb`), `/save` committed a mid-run snapshot (`x-save-seam.txt`: world `w-20261018-c0d5` at revision 2222, archive `mid-run-20261018-061053.cuoz`) and the archive's own `items.json` carries the row — `instanceId 18082891413, revision 4, location.kind 5 (Applied), owner <member>, limbIndex 14, condition 0.98999393` (`x-save-items-applied.txt`) — while the archive's character file carries limb 14 `splinted=true` with the `SplintLimb` component (`x-save-character.json`); the member quit (`j11`), relaunched (`j12`), rejoined the running lobby (`j13`) and re-entered (`j14`/`j15`); after the restore its own client reads limb 14 `SplintLimb/splinted=true` (`j21`), `/data get id W` on all three reads `applied to <member>, limb 14` revision 5 (`j22-*`) and `/idread W` reads `"location":"Applied"` (`j23-*`) |

## What the run established beside the rows

- **The two defects the batch recorded are gone at their cause, not hidden.** The host's log shows the
  applied commit and the new `destroyed by cross-player use — the item did not end` line, with no destroy
  report after it (I1); the local take-off's item stays in the member's inventory through the reconcile
  cycles, with the refused pickup and its rollback absent from the log (I4).
- **The review's third finding is visible in the row.** The removal report now carries the revived object's
  own slot and condition: I4's kernel row reads `slot 0` and the condition the component held, where the
  pre-review code kept the pre-apply slot and the last applied-condition step.
- **Both directions share one path.** I1 is the host as the item's owner; I2's apply and I4's take-off are a
  member's own item, and the member's `Applied`/`Carried` rows come from the same value on its own wire.
- **The mark's adoption is the same fact on both sides**: the host's own item X and the member's item Z both
  end with the ledger at 0 and the item's own `favourited` flag carrying the mark.

## Residuals for the user

None. Every judged fact is a console answer, a client read, an archive file or a log excerpt.

## Limits

- **One run, and the fixture is this batch's own mod**, not the deployed example mod: the row's "a mod that
  recorded the id still finds it" is proven by a real mod calling the public `IModGameState.TryGetItem`, but
  that mod is the acceptance's, not a third-party sample. Its source is in the record's artifact directory.
- **No frame is claimed**: every row is a machine read, so nothing here observes the world's drawing.
- **The save/reconnect is a rejoin of the running session**; the archive was read directly from the committed
  cut (the `.cuoz`), not reloaded by a Continue. The archive's contents and the rejoin's restored row are both
  read.
- **The container landing and the "cannot place" landing were not driven**: both rows use the slot landing the
  native `AutoPickUpItem` takes with a free slot. Their kernel shapes are named in the ticket's limits.
- **The third client's saved-copy read is absent by design** (a guest's `CharacterDataStore` does not hold
  another member's saved character); its `/idread` and its own `/data get` are the third-peer evidence.
