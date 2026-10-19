# Acceptance record — Give an item to another player (the push direction)

- Ticket: `give-item-to-another-player` — verdict: **back to `todo/`**, status field
  `- Status: Todo — Rejected (batch 20261021-a: row 4's container refusal falls back to a slot and then
  destroys the handed-over item)`
- Batch: `20261021-a` — tickets `guest-frame-rate-lower-than-host`, `give-item-to-another-player`
- Commit: `fd6e8e00` · Deployed artifact: `CasualtiesUnknownOnline.dll`
  `0.1.0+c9769177d53b0279daa93f05f75b1c513f53a546` — `tools/verify-deploy.ps1` exit 0 before the clients
  started; the commits above the build commit are documentation only
- Run: 2026-10-19 20:03:48 → 20:10 by the batch's clock · Host: physical machine · Guest: sandbox
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts` — preflight eleven present
- Artifacts: the `g*` readings in the directory named by `acceptance-artifacts-dir`; Steam ids are written
  `<host>`/`<member>`

Rows 1 (both destination shapes), 2, 3 and 6 were read in earlier batches and are not re-judged here. This
run judged the two rows that were still owed: row 4's weight/tag refusal and row 5's local control.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| G4 | The receiver's bag refuses the item by weight, on the RECEIVER's own client, with the native feedback and an observable line | machine | **fail** | The receiver's `plasticbag` (`container-read`/`canhold-probe`: `maxWeightPerItem 3.501 g`, holding 0 g) refuses the member's 4 g `toolbox`; the receiver logs `Restore: toolbox (id 9492956821) does not fit plasticbag(Clone) — refused by the container's own rule, not loaded.` and `[PlayerInteraction] … falling back to a slot.` and `placed … in local slot 2` (`g4-host-window.txt`). **Seconds later the item is gone from both clients**: the same id is destroyed (`[ItemTrace] op=6 … OnItemDestroyed result=Committed(1)`), the fact table reports `[CharSync] divergence … left the inventory without an event sync`, the standing-object materializer retires it, and the host's kernel holds the id as TERMINAL — `g5-host-ledger.json` reads `terminal: ["9492956821"]`, `carried`/`contained` name it nowhere; `g5-host-tree-after.json` and `g5-guest-tree-after.json` list it on neither body. The giver's own `ItemDestroy` for the same id is refused `InvalidTransition (terminal item 9492956821 cannot be destroyed again)` (`g4-guest-window.txt`). |
| G5 | The local control: the same release onto the giver's own backpack is the native local move, unchanged | machine | **pass** | `g10-guest-local-release.json`: `item=local1`, `castIndex=2`, `castSlot=0`, `castItem=plasticbag`, `castItemProxy=false`, `castItemOwner=0`, `viewOpen=false`, `focusSet=false`, `castIsContainer=true`, `castContentsBefore=0 → castContentsAfter=1`, `calls=none`. `g10-guest-tree-after.json`: the bandage (`18082891413`) is inside the member's own `plasticbag` (`13787924117`), slot 0, `parentType=plasticbag`, slot -1. The member's log carries the native `[ContainerLoad] bandage … moved inside body container plasticbag` and no `[RemoteIntent]` line (`g10-guest-window.txt`). |

## The failure's chain, in the receiver's own log order

```text
[20:07:38.860] [Take] <host> takes toolbox (id 9492956821) from <member> (remote-backpack allowConscious=True, container 1043998033859).
[20:07:38.878] [WRN] Restore: toolbox (id 9492956821) does not fit plasticbag(Clone) — refused by the container's own rule, not loaded.
[20:07:38.878] [WRN] [PlayerInteraction] toolbox (id 9492956821) not placed into container 1043998033859 (the container's own rules refused it) — falling back to a slot.
[20:07:38.879] [INF] [PlayerInteraction] placed toolbox (id 9492956821) in local slot 2.
[20:07:38.891] [INF] [ItemTrace] op=6 item=9492956821 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]
[20:07:38.910] [WRN] [CharSync] divergence for <member>'s toolbox (id 9492956821): left the inventory without an event sync (the 1 Hz snapshot carried it).
[20:07:38.911] [INF] [StandingItem] retired toolbox (id 9492956821, owner <member>): the data no longer carries the row.
```

The refusal is real and on the receiver's side, and the fallback line is written — but the row's item does
not survive it. `PlayerInventoryPlacement.AddCarriedItemToLocalContainer` calls
`ItemStateCodec.RestoreContent`, which instantiates the child WITH its `ItemInstanceId` and then checks
`Container.CanHoldItem`; on the refusal it logs and calls `Object.Destroy(child.gameObject)` (deferred to the
end of the frame). The caller then falls back to `AddCarriedItemToLocalBody`, which instantiates a SECOND
object with the SAME id and places it in slot 2. At the end of the frame the first object's destroy fires and
publishes the id as destroyed; the kernel marks it terminal, and the second object is swept. The id and the
object are lost together.

The fix must not destroy a materialized child that the fallback is about to own: hand the same object to the
slot fallback, or destroy it without publishing a destroy for its id. A regression test must drive a
container refusal and read the item alive in a slot afterwards; the ticket stays open for it.

## Residuals for the user

None.

## Limits

- **One fixture shape.** A 4 g `toolbox` (a container item) aimed at a 3.501 g `plasticbag`; the occupied-slot
  and tag-restriction shapes of row 4 are not this run. The receiver's rule was judged on the live pair
  (`canhold-probe`: `itemWeight 4`, `totalWeight 4`, `canHold false`) and on its own log lines.
- **Two clients, no third peer.** G4's outcome was judged from the two participants' own trees and the
  host's authoritative tables; a third client's view was not part of this row.
- **The item id is a fixture id.** `9492956821` is the id this session's fixture created; the record's claim is
  about the chain, not about that number.
