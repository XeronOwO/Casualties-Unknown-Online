# Guest carried container contents periodically appear as world drops on the host view (dog food in trash bag)

- Status: Review
- Priority: Medium
- Acceptance (20260927-d): row 4 `pass` from the run's suites; rows 1–3 are setup-gap blocked on a container scenario — record `docs/evidence/acceptance/guest-container-contents-ghost-drops-on-host-20260927.md`
- Acceptance (20260930-f): rows 1–2 `pass` from this batch's container scenario (row 2 with the panel frames read); row 3 `unproven` — the rejoin never re-activated the session — record `docs/evidence/acceptance/guest-container-contents-ghost-drops-on-host-20260930.md`
- Acceptance (20260930-g): row 3 `unproven` — the reconnect now re-activates the session (three attempts), but the carried container's contents did not survive the re-entry (the guest's body came back empty after the cleared-table cycle and the dog food was still missing in the control); this batch's artifacts are the evidence the reconnect half of *Root cause and fix* rests on — record `docs/evidence/acceptance/guest-container-contents-ghost-drops-on-host-20260930-g.md`
- Category: Item/container sync / remote presentation
- Source: User report (2026-09-04) — a guest puts dog food into a trash bag, then while moving the host periodically sees a can of dog food drop from the guest's body; the guest's own view does not see it.
- Selfcheck: `docs/evidence/selfchecks/items/remote-clone-display-content-id-free-selfcheck.md`

## Goal

Eliminate the host-only periodic dog food "drop" from the guest's carried container. The nested dog food should remain inside the trash bag in every view and in the authoritative item state; no ghost world item should appear on the host.

## Root cause and fix

The remote clone inventory renderer materializes a carried container's contents on the
remote clone as display proxies. The live defence that keeps domain operations away from
those proxies is the display-proxy skip in `RemoteItemSceneOps.FindWorldItem` and
`FindExistingAt` — and its siblings guarded by `RemoteCloneRender` in `PickupSync`,
`BodyItemPatches` and `ContainerItemSync` — so a domain event for the real id never
addresses a proxy and cannot unparent it into a world drop.

The section this ticket originally carried named a `CloneInventoryContentSanitizer` wired
into `CloneInventoryRenderer.RestoreRemoteContents`. In the frozen tree that type has no
production caller, and the renderer marks each materialized clone child with the
display-domain `RemoteInventoryItemId`, never a domain instance id; the sanitizer claim was
stale and is corrected here. The batch `20260930-f` row 1–2 verdicts rest on the observable
behaviour, not on that claim.

### Reconnect half (row 3)

Two causes, established from batch `20260930-g`'s artifacts and fixed 2026-10-01:

1. **The restore reported its own placements as operations.** `CharacterRestoreApplier.ApplyItems`
   re-materializes the snapshot through the game's own slot path (`Body.PickUpItem` /
   `Container.LoadItem`), and those hooks (`PickupSync`, `ContainerItemSync`) report a pickup. The
   host already holds those facts, so the kernel refused exactly them — that batch's host log
   carries `Conflict (item … is already carried)` for the restored bag and light — and the refusal
   path (`ItemApplication.OnItemRejected` → `RollbackPickup`) is the one that pulls a restored item
   back OUT of the body. The pass now runs inside `CallContext.Origin.RemoteApply`, the scope the
   item correction applies already use. This is NOT the whole of that batch's loss: the control
   reconnect lost its dog food with no refusal at all, so the report is fixed because it is wrong on
   its own terms, not because it explains every loss.
2. **The merge flattened the container.** The host's per-id transfer table (its authoritative record
   of what the guest owns) is flat: an entry captured as a pickup digest states a container's
   contents as bare instance ids, and one rebuilt from the kernel states none. `CharacterDataStore`
   overlaid it on the guest's recursive snapshot by replacing a matched item wholesale — losing the
   snapshot's contents — and by indexing only the top level, so a contained child's own entry was
   appended beside its container and handed to `WearableRestorer`, whose limb-index guard dropped it
   (a content's slot is its parent's). The merge is now `TransferTableRestoreMerge`: it matches
   recursively, takes the entry's state onto the node the snapshot carries, keeps the snapshot's slot
   and contents, and counts and names an entry the snapshot cannot place instead of appending it.

The kernel-side flattening itself is NOT changed here: `KernelWireMapper`'s `ItemPickup` mapping
carries no container children, so a content the kernel already holds flat stays a flat carried fact
(see *Limits*).

## Acceptance criteria status

- [x] No periodic dog food can appears as a world drop on the host while the guest carries the trash bag/container — addressed at the display/domain boundary; runtime acceptance still pending.
- [x] The guest view and host view show the dog food inside the container consistently — existing clone refactor is unchanged apart from id stripping.
- [x] No duplicate/ghost item id, no transfer-table resurrection, no dropped item after reconnection — addressed on the restore path: the merge keeps the snapshot's placement and the restore re-materializes inside `RemoteApply` (see *Root cause and fix*); runtime acceptance still pending.
- [x] Existing container/item sync tests and repo gates remain green — verified by the focused suites and the repo gates on this branch.

## Limits (this cycle)

- A content the guest moved into a container AFTER its last 1 Hz report has no node in the snapshot,
  and the transfer table cannot state the parent (its `ParentItemId` is `0` where the table builds an
  entry), so the merge counts and warns it instead of guessing a placement. Narrow and named, not
  silent — the row-3 re-run must not read it as the container being restored empty.
- A content drop that reaches the host before its container is materialized logs
  `[ItemBind] container … not found — item stays where it is` (batch `20260930-g` host log) — the
  host-side sibling of the same placement family, not fixed here.
- The kernel keeps a picked-up container's contents flat (`ItemPickup` carries no container
  children), so where the snapshot has no nesting either, the restore reproduces the kernel's flat
  record.

## Non-goals

- Not changing the game's container behavior outside CUO sync.
- Not adding remote backpack interaction parity in this cycle (separate backlog ticket).
