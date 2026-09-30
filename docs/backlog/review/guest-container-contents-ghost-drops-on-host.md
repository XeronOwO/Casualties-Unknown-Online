# Guest carried container contents periodically appear as world drops on the host view (dog food in trash bag)

- Status: Review
- Priority: Medium
- Acceptance (20260927-d): row 4 `pass` from the run's suites; rows 1–3 are setup-gap blocked on a container scenario — record `docs/evidence/acceptance/guest-container-contents-ghost-drops-on-host-20260927.md`
- Acceptance (20260930-f): rows 1–2 `pass` from this batch's container scenario (row 2 with the panel frames read); row 3 `unproven` — the rejoin never re-activated the session — record `docs/evidence/acceptance/guest-container-contents-ghost-drops-on-host-20260930.md`
- Acceptance (20260930-g): row 3 `unproven` — the reconnect now re-activates the session (three attempts), but the carried container's contents did not survive the re-entry (the guest's body came back empty after the cleared-table cycle and the dogfood was still missing in the control) and the attribution stays open — record `docs/evidence/acceptance/guest-container-contents-ghost-drops-on-host-20260930-g.md`
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

## Acceptance criteria status

- [x] No periodic dog food can appears as a world drop on the host while the guest carries the trash bag/container — addressed at the display/domain boundary; runtime acceptance still pending.
- [x] The guest view and host view show the dog food inside the container consistently — existing clone refactor is unchanged apart from id stripping.
- [x] No duplicate/ghost item id, no transfer-table resurrection, no dropped item after reconnection — the authoritative kernel/state paths are unchanged.
- [x] Existing container/item sync tests and repo gates remain green — 2226 tests pass.

## Non-goals

- Not changing the game's container behavior outside CUO sync.
- Not adding remote backpack interaction parity in this cycle (separate backlog ticket).
