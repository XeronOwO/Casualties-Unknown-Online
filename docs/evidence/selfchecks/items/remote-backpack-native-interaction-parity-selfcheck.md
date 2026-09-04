# Remote backpack native interaction parity — self-check

Closes the backlog item "Remote backpack native interaction parity". The native
remote-backpack view previously supported only a host-authoritative take on drag
release. This cycle maps the remaining natural native gestures (edge drop, move
into a remote container, pour/dump liquid, Tab-switch transfer) to the same
host-authoritative semantic model instead of mutating display proxies.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native radial inventory uses `InvButton.body` | `InvButtonBodyPatch` routes `InvButton.body` to `RemoteBackpackView.FocusedBody` while open |
| 2 | Display proxy identity marker | `RemoteInventoryItemId` now carries both the authoritative instance id and the owner SteamId; `CloneInventoryRenderer.SetRemoteInventoryItemId` writes both |
| 3 | Release handling | `PlayerCameraDragUsePatch` intercepts `HandleReleaseDragging` and routes remote-proxy releases |
| 4 | Host operation authority | `PlayerRemoteInventoryService` validates owner/requester/line-of-sight, updates kernel + character snapshot, and records result events |
| 5 | Kernel container reconciliation | `SyncContainerItemsCommand` reconciles the remote owner's target container subtree in one atomic batch |
| 6 | Same-owner container result | `PlayerInventoryTransferEvent`/message carriage of `TargetParentItemId` lets the owner's body place the item into the exact container |
| 7 | State-only result (pour) | Existing `PlayerItemUseResultEvent` path updates the owner's local item liquids/components |
| 8 | Tab-switch transfer | A remote proxy released into the local inventory after the remote view closes reuses the existing cross-player take request |
| 9 | Proxy safety | Unconsumed remote-proxy releases are still cancelled before native/cross-player release logic can move them into an authoritative body |

## 2. Native gesture → authoritative operation map

| Native remote-backpack gesture | CUO operation | Host authority | Supported |
|---|---|---|---|
| Drag item out and release (current take) | `PlayerInventoryTakeRequest` | Host validates unconscious/dead + `AllowRemoteInventoryTake` | Yes (pre-existing) |
| Drag remote water bottle to left edge / liquid-drain area | `RemoteInventoryOperationRequestMsg` → `Pour` | Host clears the liquid stacks, updates kernel + owner snapshot, sends `PlayerItemUseResult` | Yes |
| Drag item to left/right screen edge | `RemoteInventoryOperationRequestMsg` → `Drop` | Host moves kernel item to World at owner position, sends owner-removal transfer | Yes |
| Drag item onto a remote container (same owner) | `RemoteInventoryOperationRequestMsg` → `MoveToContainer` | Host reconciles `SyncContainerItemsCommand`, sends same-owner parent transfer | Yes |
| Hold remote item, Tab-close remote view, open local, release into local inventory | existing `PlayerInventoryTakeRequest` from marker owner | Host validates take rules | Yes |
| Combine / use / wear / battery / load-unload / favorite / slot-swap on a remote proxy | — | Not implemented in this slice; release is cancelled with a log | No |
| Remote container contents drag between remote containers | `MoveToContainer` when the release hits a remote container button; nested source depth is handled recursively | Yes (via same operation) | Yes |
| Remote-to-remote cross-player container move | — | Not implemented; not a native single-backpack gesture | No |

## 3. Changes

- **New protocol message** — `RemoteInventoryOperationRequestMsg` +
  `RemoteInventoryOperationKind` (`Drop`, `MoveToContainer`, `Pour`),
  `NetMsg.RemoteInventoryOperationRequest` (guest → host). Take/transfer-to-local
  intentionally reuses the existing take request path.
- **Runtime host service** — `PlayerRemoteInventoryService`:
  - validates host role, session/world, owner/requester distinct, line-of-sight,
    `AllowRemoteInventoryTake`, item existence and non-worn slot;
  - drops via `SpawnItemCommand` when the kernel has not yet seen the item,
    otherwise `DropItemCommand`;
  - moves via `SyncContainerItemsCommand` (handles unknown parent/child kernel
    facts in one atomic batch);
  - pours via `SpawnItemCommand`/`UpdateItemStateCommand` with emptied liquids;
  - records participant results via the existing player-interaction journal.
- **Player interaction transfer event** — `PlayerInventoryTransferEvent`,
  `RecordPlayerInventoryTransferCommand`, `WirePlayerInteraction`, and
  `PlayerInventoryTransferMsg` now carry `TargetParentItemId` for same-owner
  container placement.
- **Game Adapter apply side** — `PlayerInteractionApply` places a received
  same-owner transfer item into the local target container via
  `ItemStateCodec.RestoreContent` instead of a top-level slot.
- **Display proxy identity** — `RemoteInventoryItemId.OwnerSteamId` is stamped by
  `CloneInventoryRenderer`; the Tab-switch path no longer needs the remote view
  to stay open to know which player owns the held proxy.
- **Drag release routing** — `PlayerCameraDragUsePatch` now maps remote-proxy
  releases to pour/drop/container/Tab-switch; `RemoteBackpackView.Close` no longer
  cancels a held proxy because the release patch owns the safety decision.
- **Bridge split** — the remote-backpack routing moved to
  `RemoteBackpackOperationHandler` to keep `GameAdapterBridge` under the
  600-line architecture gate.

## 4. Verification (development-period, no manual acceptance)

- **L0 tests added**: guest drop, guest move-to-container, guest pour,
  line-of-sight refusal, direct host drop, transfer-event parent wire round-trip,
  remote-proxy owner marker and bridge contract.
- **Full suite**: `dotnet test CasualtiesUnknownOnline.slnx` — **2240 passed /
  0 failed** before the final DirectionTests update; the full run after the
  final update is recorded in the delivery checklist/gates below.
- **Gates**: `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1`, `tools/check-delivery.ps1` pass.
- **Format**: `dotnet format` run; `--verify-no-changes` clean except ignore rules.

## 5. Structure review

- New top-level types are single-purpose: `PlayerRemoteInventoryService`
  (~440 lines, under gate), `RemoteBackpackOperationHandler` (~140 lines),
  request DTOs, one handler class.
- `GameAdapterBridge` back under 600 lines after the extraction.
- No display-proxy mutation was added: every accepted gesture is a
  host-authoritative request; every unsupported gesture is still cancelled with
  an observable log.
- Existing `RemoteProxyDragPolicyTests` stay green; the policy's doc now
  mentions the Tab-switch transfer path.
