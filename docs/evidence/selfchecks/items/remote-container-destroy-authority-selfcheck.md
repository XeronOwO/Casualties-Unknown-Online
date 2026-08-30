# Remote container destroy authority — display-proxy destroy containment (2026-08-27)

Closes the open bug "Container contents disappear after guest views host
inventory (trash bag etc.)". The native remote-backpack view materialises
recursive container contents on the remote render clone. Those display proxies
carry the owner's real `ItemInstanceId`s so the native container UI can show
nested items; when the renderer pruned/replaced them it fired ordinary
`Item.OnDestroy`, and the item domain reported those ids to the host as real
world-item destroys.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Remote clone container rendering | `CloneInventoryRenderer.RestoreRemoteContents` materialises snapshot `Contents` under a remote clone container, marked `RemoteCloneRender`, with the snapshot's instance ids |
| 2 | Proxy destroy reporting | `Item.OnDestroy` → `ItemPatches` → `ItemWorldSync.OnItemDestroyed` reports any id-bearing item, including remote-clone proxies |
| 3 | Host relay path | `ItemMessageFlowService.FireItemDestroyedReceived` removed from the world table and broadcast every received destroy without owner/world validation |
| 4 | Host scene kill path | `ItemApplication.OnRemoteItemDestroyed` found any local object by id and killed it — it did **not** require the object to be a world item, so a guest's proxy destroy for a host carried id killed the host's real bag content |
| 5 | Container proxy loads | `Container.LoadItem`/`UnloadItem` on remote clone containers also entered `ContainerItemSync`, producing fake container-content reports and allocating guest-local display ids |

## 2. Changes

- **Send-side proxy suppression** — `ItemWorldSync.OnItemDestroyed` now
  returns immediately for any item inside a `RemoteCloneRender` tree
  (`GetComponentInParent`); `ContainerItemSync` and `ContainerItemPatches`
  also reject loads/unloads/spills on display-proxy containers.
- **Host destroy authority** — `ItemMessageFlowService.FireItemDestroyedReceived`
  only accepts a destroy report when the id is a registered world item or a
  carried item the **sender** owns. A non-owner/non-world destroy is ignored
  and not relayed. An owner's carried destroy now also removes the transfer-table
  entry (the item no longer exists to restore after a reconnect).
- **Receive-side world guard** — `ItemApplication.OnRemoteItemDestroyed`
  only kills a locally found object when it is a world item
  (`ItemWorldSync.IsWorldItem`), mirroring the existing remote-pickup guard.
  Carried items and remote display proxies are never killed by a remote destroy
  report.
- **No wire change** — no new `NetMsg`, no `ProtocolVersion` bump, no
  event/item/entity matrix row touched.

## 3. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Non-owner destroy ignored | Host refuses non-world/non-owned destroy before remove/broadcast | `ItemDestroyAuthorityTests.NonOwnerCarriedDestroy_IsNotBroadcast` |
| Owner carried destroy removes transfer entry | Host removes transfer table entry on owned destroy and relays | `ItemDestroyAuthorityTests.OwnerCarriedDestroy_RemovesTransferEntryAndBroadcasts` |
| Proxy destroy never reported | `ItemWorldSync.OnItemDestroyed` skips `RemoteCloneRender` trees | Static code path + adapter build |
| Proxy container moves never reported | `ContainerItemSync`/`ContainerItemPatches` skip display-proxy containers | Static code path + adapter build |
| Host never kills carried item from remote destroy | `ItemApplication.OnRemoteItemDestroyed` requires `IsWorldItem` | Static code path + full suite |
| No existing item lifecycle path broken | Owner/world destroy semantics preserved | Existing item-domain tests stay green |

## 4. Verification (development-period, no manual acceptance)

- **L0**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **1554 passed / 0 failed** (2 new).
- **Gates**: `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1` all pass.
- **Format**: `dotnet format` run; `--verify-no-changes` only flags the
  gitignored generated `obj/.../MyPluginInfo.cs`.
- **Runtime verification**: development-period rule — L0 simulation + static
  evidence, **no manual acceptance**.

## 5. Structure review

- `RemoteCloneContainerGuard` is a small new top-level type (one per file).
- `ItemMessageFlowService` gains a guarded branch only; no new state.
- `ItemWorldSync`/`ContainerItemSync`/`ItemApplication` gain early guards and
  logging in existing methods; no class crosses the 600-line gate.
- Dead mechanisms: none. The old unconditional destroy relay is replaced by an
  authority check, not layered on top.
