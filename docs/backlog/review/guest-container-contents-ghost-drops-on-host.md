# Guest carried container contents periodically appear as world drops on the host view (dog food in trash bag)

- Status: Review
- Priority: Medium
- Category: Item/container sync / remote presentation
- Source: User report (2026-09-04) — a guest puts dog food into a trash bag, then while moving the host periodically sees a can of dog food drop from the guest's body; the guest's own view does not see it.
- Selfcheck: `docs/evidence/selfchecks/items/remote-clone-display-content-id-free-selfcheck.md`

## Goal

Eliminate the host-only periodic dog food "drop" from the guest's carried container. The nested dog food should remain inside the trash bag in every view and in the authoritative item state; no ghost world item should appear on the host.

## Root cause and fix

The remote clone inventory renderer used the shared authoritative restore helper to
materialize a carried container's contents on the remote clone. That helper attaches
`ItemInstanceId` to each materialized child. Remote clone items are display proxies,
not authoritative item-domain objects. With an id on a proxy, the domain lookup
(`RemoteItemSceneOps.FindWorldItem`) can resolve the proxy instead of the real item;
a subsequent domain event (e.g. a drop/correction for that id) can then unparent the
proxy and make it appear as a world drop from the guest's body on the host.

Changes in this cycle:

- Added `CloneInventoryContentSanitizer`: pure, recursive zeroing of `InstanceId`
  for clone display content before the renderer's restore.
- `CloneInventoryRenderer.RestoreRemoteContents` now passes sanitized content to
  `ItemStateCodec.RestoreContents`; display proxies never receive item-domain ids.
- `RemoteItemSceneOps.FindWorldItem` and `FindExistingAt` now skip any item under a
  `RemoteCloneRender`, as a defense-in-depth guarantee that domain operations never
  address display proxies.

## Acceptance criteria status

- [x] No periodic dog food can appears as a world drop on the host while the guest carries the trash bag/container — addressed at the display/domain boundary; runtime acceptance still pending.
- [x] The guest view and host view show the dog food inside the container consistently — existing clone refactor is unchanged apart from id stripping.
- [x] No duplicate/ghost item id, no transfer-table resurrection, no dropped item after reconnection — the authoritative kernel/state paths are unchanged.
- [x] Existing container/item sync tests and repo gates remain green — 2226 tests pass.

## Non-goals

- Not changing the game's container behavior outside CUO sync.
- Not adding remote backpack interaction parity in this cycle (separate backlog ticket).
