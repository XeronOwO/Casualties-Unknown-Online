# Acceptance record — the item data read verb: the ambiguous selector and an applied item

- Ticket: `item-data-read-commands` — verdict: **moved to `done/`**. Its two remaining rows (5b and 6) pass; the
  rows batch `20261018-q` judged (1-4, 5a, 5c) are that record's and were not re-run here.
- Batch: `20261020-a` — tickets `item-data-read-commands`, `offscreen-arrow-label-clipping`.
- Commit: `b28ee39d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c9769177d53b0279daa93f05f75b1c513f53a546`. No rebuild: the delta between the artifact's commit and
  this tree's HEAD is documentation-only (`git diff --stat c9769177..HEAD`), and `tools/verify-deploy.ps1`
  exited 0 against this tree's build output before the clients started.
- Run: 2026-10-19 19:46:00 → 19:55:45 (+08:00), by the clients' own log clock · Host: physical machine ·
  Member: `Steam1` sandbox · Bystander: `Steam2` sandbox.
- Dependencies: `game`, `deploy` (verified, not rebuilt), `steam`, `sandboxie`, `sandbox-alt`, `hotrepl` (three
  endpoints), `input`, `logs`, `artifacts`.
- Artifacts: the `r5b-*`, `r6-*`, `m-row6.txt` and `driver.log` readings in the directory named by
  `acceptance-artifacts-dir`; Steam IDs are written `<host>`/`<member>`/`<bystander>` here.

The row table this run judged was written into the batch plan before the session ran (batch artifact, local
only). Rows 5b and 6 are the ticket's own; rows 1-4, 5a and 5c are batch `20261018-q`.

| # | Row (the ticket's own) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 5b | `/data get @a slot 0` in a session with two members BESIDES the caller: the count of bodies the ambiguous selector matched, together with what to write instead | machine | **pass** | three roles, each caller resolving `@a` to the two OTHER bodies: host `That selector matched 2 bodies (<member>, <bystander>) — a slot read names ONE item, so name one body: @p, a selector with [limit=1], or the item's own id.`; member `That selector matched 2 bodies (<host>, <bystander>) — …`; bystander `That selector matched 2 bodies (<host>, <member>) — …` (`r5b-host-slot.json`, `r5b-guest-slot.json`, `r5b-alt-slot.json`). The listing form `@a` still answers per body: two blocks, one carried item each (`r5b-host-listing.json`). |
| 6 | An item that IS recorded and then applied to a limb (a splint): listed by the body form as `limb N`, and read by its id as `applied to <owner>, limb N` | machine | **pass** | The member created a splint (`item-provide mode=create type=splint`: id `9492956821`, slot 0, `r6-guest-create-splint.json`) and both the member and the host read it carried first: `#9492956821 splint [carried by <member>] revision 2` (`r6-guest-data-carried.json`, `r6-host-data-carried.json`). The apply ran through the game's own entry `PlayerCamera.ApplyWoundItem(splint)` with `selectedLimb` staged on limb 2 of 15; that body calls the item's own `useLimbAction` (`PlayerCamera.cs:754`), which added the component and destroyed the item object (`r6-guest-apply.json`: `afterComponent:true`, `componentCondition:1`, `conscious:true`, `aboveCutoff:true`, `usableOnLimb:true`, `actuallyUsable:true`), and the client logged `[LimbItem] the local limb action of splint (id 9492956821) installed the item on limb 2; it keeps its id and now lives on that limb.` (`m-row6.txt`, guest log, 19:48:15.255). The host's log carries one `Receive ItemApplyLimbCommand=66B/1f` in its traffic line, i.e. the relocation arrived as one command. After the apply: the member's body form lists `#9492956821 splint [limb 2] {…}` (`r6-guest-data-body.json`) and `/data get id 9492956821` reads `#9492956821 splint [applied to <member>, limb 2] revision 3` on the member, the host and the bystander (`r6-guest-data-id.json`, `r6-host-data-id.json`, `r6-alt-data-id.json`); the member's own limb and the host's stored copy both read `splinted:true, components:"SplintLimb"` for limb 2 (`r6-guest-limb2.json`, `r6-host-saved-limb2.json`). |

## What the run adds beyond the rows

- **The ambiguity answer is identical on every role and always names the two OTHERS**: the selector is the
  caller's own view (`@a` never names the caller), so a three-client session is what makes the count two, and
  the wording is the same whichever role asks.
- **The applied row is one relocation seen by all three roles.** The member's own replay kernel answered the
  same `applied to … limb 2` as the host's authority and the bystander's copy, so the read surface is not
  host-only.
- **The apply's two halves are both native**: the item's own `useLimbAction` added the component (its sound and
  sprite included), and CUO's relocation patch on the same entry produced the `[LimbItem]` line and the row.

## Residuals for the user

None. Every judged fact is a console answer, a probe result or a log excerpt.

## Limits

- **The apply's entry was driven in process, with its input staged.** `selectedLimb` is scene state the run set
  before calling `PlayerCamera.ApplyWoundItem`; the delegate, the component, the item destroy and CUO's
  relocation patch are the product path under test. This is not the `seed-splint` component injection: the
  component was added by the item's own delegate and the item row was relocated by the same call. The probe is
  `limb-apply.cs` in the batch's artifact directory (one eval, `mode=probe` then `mode=apply`), so the row can be
  re-driven without a new recipe.
- **The splint is a created fixture.** `item-provide mode=create` is the game's own `Utils.Create` +
  `Body.PickUpItem(force:true)`; the created item entered the domain on that pickup, which is why it had an
  instance id at all.
- **The member's limb carried a splint before the apply and a splint after it** — that is the item under test,
  not a pre-existing component: `beforeComponent:false` in `r6-guest-apply.json` is the read taken immediately
  before the call.
- **One run, one world, no reconnect.** The applied row's survival across a save/reconnect is another ticket's
  row and was not read here.
- The host's own `/data get` listing showed one carried item for the host (its starting supply) and not the
  member's; a body form always answers the bodies the selector names, not every member's inventory.
