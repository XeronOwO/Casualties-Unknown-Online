# Remote backpack item projection acceptance issues (projection rework needed)

- Status: Review
- Priority: High
- Category: Remote inventory / item projection / container sync
- Parent: `docs/backlog/review/remote-backpack-native-interaction-parity.md`
- Source: User acceptance findings (2026-09-06) on the remote-backpack review ticket.

## Context

During acceptance of the "Remote backpack native interaction parity" review
ticket, the user found multiple issues in the remote item projection layer.
The user's judgment: the projection mechanism was not designed for these
interactions and feels fundamentally weak; it needs a thorough audit or a
complete rework rather than more point patches.

## Autonomous fix cycle (2026-09-07+)

This cycle addressed the remaining reported interaction family instead of adding
another isolated point patch. The changes are grouped by root cause:

1. **Durability projection** — already corrected by the unified
   `RemoteItemPresentation.ApplySourceValues` pass; all clone-inventory renderer
   branches now carry `Condition`, `Favourited` and `Liquids`.

2. **Double-Tab transfer (both directions)** — a dedicated
   `RemoteInventoryOperationKind.TransferToRequester` path now runs through
   `PlayerInventoryTakeService.HandleRemoteBackpackTake`. The remote backpack
   Tab-transfer is explicitly allowed to transfer a conscious remote owner's
   item to the opener's own inventory when `AllowRemoteInventoryTake` is
   enabled; the normal Online UI unconscious-only take rule is unchanged.
   - `PlayerInventoryTakeService.HandleTakeRequest` was refactored to
     `HandleTakeCore(..., allowConsciousSource: false)`; the new remote path uses
     `allowConsciousSource: true`, preserving the existing health-null and
     conscious refusal for the legacy path.

3. **Pour / edge-drop** — the remote release router now treats the native
   `liquidDrainObject` as the pour gesture and sends `Pour`; left/right screen
   edges always send `Drop`. A water container released on the drain icon pours,
   while edge-dropping it no longer gets swallowed as a pour.

4. **Host-side trash-bag take-out vanish** — same-owner container transfer no
   longer destroys and rebuilds the real source when the native container
   refuses the move. It logs the container capacity/tag check and leaves the
   real item in place so the next character report re-converges instead of
   orphaning the item.

5. **Guest main-hand placement** — the remote release router now processes body
   slot `InvButton`s even when the native `InvButton.Overlaps` center-button
   distance filter would skip them, so the main-hand slot can receive
   `MoveToSlot` like every other slot.

6. **Held-remote-item + Tab-close + R medical use** — new
   `RemoteInventoryOperationKind.UseOnSelf` plus
   `PlayerItemUseService.HandleRemoteHeldItemUse`. A remote display proxy
   released on the local WoundView after closing the remote backpack is routed
   to a host-authoritative request that consumes/updates the remote owner's item
   and applies the body-side effect to the requester. Item lookup/replacement is
   recursive, so items inside a trash bag/backpack are supported. The owner does
   not need to be conscious; the requester must be conscious/alive and must be
   able to see the owner.

7. **Selective trash-bag insertion** — service-side tests confirm no
   item-type-specific restriction in the MoveToContainer path. If a specific
   item is still refused on the real client, the newly added native-container
   refusal log now records weight/max-per-item/max-total/current so the next
   dual-client pass can distinguish a native game rule from a projection defect.

## Verification

- Full build: 0 warnings / 0 errors.
- Full suite: **2529 passed / 0 failed** (+ 17 normative gate tests).
- `dotnet format` applied.
- New tests cover:
  - host uses remote guest water bottle on self,
  - nested-container recursive held-item self-use,
  - reverse direction (guest uses host item on self),
  - unconscious owner + conscious requester,
  - worn remote item is refused,
  - double-Tab transfer in both directions with a conscious owner,
  - legacy Online UI take still refuses a source with no health snapshot.
- Latest DLLs deployed to the concrete game directory and SHA256 matches the
  build output for all six CUO assemblies.

## Remaining boundary

Real dual-client visual/gesture acceptance remains the final unified acceptance
pass. The code-side paths are complete; the native trash-bag refusal log and
the new public routes are the evidence points for that pass.

## Original acceptance matrix

The original matrix is retained as the acceptance record; rows whose code path
is now implemented are listed here so the final dual-client pass can verify them
against the latest deployed DLLs.

| Scenario | Expected | Current observed (pre-cycle) |
|---|---|---|
| Host holds 75% metal scrap; guest opens host backpack | guest sees 75% | guest sees 100% (fixed by unified source-value projection) |
| Guest moves host water bottle into host trash bag | succeeds | failed (code path has no type restriction; runtime trace may show native container refusal) |
| Guest moves host dog food into host trash bag | succeeds | failed (same) |
| Guest moves host lantern into host trash bag | succeeds | failed (same) |
| Guest moves host metal scrap into host trash bag | succeeds; 75% shown; removable | succeeds |
| Guest double-Tab transfers a host item to self | succeeds | fails (now `TransferToRequester`) |
| Guest pours out host water bottle | succeeds | fails (now drain-object pour) |
| Guest edge-drops host water bottle | succeeds | fails (now screen-edge Drop) |
| Host moves guest item into guest trash bag | succeeds | succeeds |
| Host drags guest item out of guest trash bag to guest slot | item returns to slot | item disappears (now non-destructive same-owner apply) |
| Host places item into guest main hand | succeeds | fails (now body-slot button routing ignores center distance) |
| Host places item into other guest slots | succeeds | succeeds |
| Host double-Tab transfers a held guest item to self | succeeds | fails (now `TransferToRequester`) |
| Host holds guest item, presses Tab to close, then R opens medical and uses item on self | supported and usable | not supported (now `UseOnSelf`) |

## Non-goals

- No parallel CUO remote-backpack UI; the native surface remains the only UI.
- Cross-player remote-to-remote handoff without a local inventory is still
  future work.
