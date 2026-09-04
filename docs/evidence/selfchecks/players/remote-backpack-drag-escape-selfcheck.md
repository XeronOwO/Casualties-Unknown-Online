# Remote-backpack drag escape — display-proxy release containment (2026-08-27)

> **HISTORICAL** — This selfcheck describes the earlier fix that cancelled any
> held proxy when the remote backpack view closed. The native parity cycle
> replaced that cancellation with a release-time safety rule so a held proxy can
> legally continue into the Tab-switch transfer path; see
> `docs/evidence/selfchecks/items/remote-backpack-native-interaction-parity-selfcheck.md`
> for the current behavior.

Closes the open bug "Remote-backpack drag can duplicate a water bottle into
both inventories". A remote-clone display proxy could be dragged from the native
remote-backpack view, the view could be closed while the drag was still held,
and the proxy could then be released into the player's own native backpack. The
native release path re-parented the presentation-only proxy under the local
body; the local character-capture path then reported that proxy as if it were
the player's own item, producing the duplicate on the host.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Remote clone display proxies | `CloneInventoryRenderer` materialises recursive remote contents with `RemoteCloneRender` + `ItemInstanceId` (`src/.../Character/CloneInventoryRenderer.cs:200-264`) |
| 2 | Native drag pickup from remote view | The read-only pickup block was removed in #122; `PlayerCamera.TryPickupFromUI` can now set `PlayerCamera.dragItem` to a proxy (`PlayerCamera.cs:1396-1417`) |
| 3 | Remote take release path | `PlayerCameraDragUsePatch` routes a proxy release to `IPatchBridge.TryHandleRemoteBackpackTake` while the remote view is open (`PlayerCameraDragUsePatch.cs`) |
| 4 | View close clearing focus only | `RemoteBackpackView.ClearIfStale` / `Close` cleared focus but did not cancel an active proxy drag, so the drag could outlive the view |
| 5 | Native release path | `PlayerCamera.HandleReleaseDragging` would normally move the dragged item into the local body (`PlayerCamera.cs:1456-1497`, `TryPerformInventoryAction` line 1629) |
| 6 | Local character capture | `CharacterDataSync.CaptureCharacterData` serialises local body slot/limb items to `CharacterDataMsg`; it had no display-proxy filter |
| 7 | Guest initial carried report | `CarriedInventoryReporter.Report` assigns ids and reports id-less carried items; it also had no display-proxy filter |

## 2. Changes

- **Drag lifetime is tied to the remote view** — `RemoteBackpackView.Close`
  now calls `IPatchBridge.CancelRemoteProxyDrag` so a lingering
  `RemoteCloneRender` drag is cancelled the moment the view closes, before the
  user can open their own backpack and release the proxy.
- **Release-time invariant** — `PlayerCameraDragUsePatch` now runs a pure
  `RemoteProxyDragPolicy.ShouldCancelProxyRelease` check after the remote-view
  block. Any remote display proxy that was not consumed by the remote-take
  path is cancelled before the original native release or the cross-player
  drag-use path can touch it. This also closes the side path where a proxy
  could be released over another remote player and send a use request with the
  proxy's owner instance id.
- **Ownership verification for remote take** —
  `GameAdapterBridge.TryHandleRemoteBackpackTake` now requires the dragged
  proxy to be a descendant of the currently focused remote clone, preventing a
  stale proxy from being sent as a take request against a different focused
  owner.
- **Authority-capture filtering** — `CharacterDataSync.CaptureCharacterData`
  and `CarriedInventoryReporter.Report` skip items carrying
  `RemoteCloneRender`. Even if a display proxy ever leaks into the local body
  through an unforeseen path, it can never be serialized as an authoritative
  local inventory item (the last line of defence against wire duplication).
- **Observability** — `IPatchBridge.CancelRemoteProxyDrag` is the single
  cancellation seam and logs the item id / instance id / reason at Warn.

## 3. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Remote view close cancels a held proxy | `RemoteBackpackView.Close` → `CancelRemoteProxyDrag` | Static path; `RemoteBackpackContractTests` locks the new bridge surface |
| Closed-view proxy release is cancelled | `RemoteProxyDragPolicy.ShouldCancelProxyRelease(true,false)` = true before native/cross-player release | `RemoteProxyDragPolicyTests.ProxyNotConsumedByTake_MustCancelEvenAfterRemoteViewClosed` |
| Take-consumed proxy is not double-cancelled | `ShouldCancelProxyRelease(true,true)` = false | `RemoteProxyDragPolicyTests.ProxyConsumedByTake_DoesNotNeedAdditionalCancel` |
| Local item drag is unaffected | `ShouldCancelProxyRelease(false,false)` = false | `RemoteProxyDragPolicyTests.LocalItem_IsNeverCancelledByTheProxyRule` |
| Stale proxy cannot take from a different owner | `TryHandleRemoteBackpackTake` checks `IsChildOf(focused)` | Static path + existing host authority rejection if it ever reached the wire |
| Proxy cannot be captured as local authority | `CharacterDataSync` + `CarriedInventoryReporter` skip `RemoteCloneRender` | Static path |
| No wire/event-matrix break | No NetMsg/ProtocolVersion/event-row changes | `git diff` contains no Runtime protocol files |

## 4. Verification (development-period, no manual acceptance)

- **L0**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **1567 passed / 0 failed**.
- **Gates**: `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1` all pass.
- **Format**: `dotnet format` run.
- **Runtime verification**: development-period rule — L0 simulation + static
  evidence, **no manual acceptance**.

## 5. Structure review

- `CharacterDataSync.cs` is at 599 lines (under the 600-line gate); the
  display-proxy skip is an inline guard, no new state or helper in the class.
- `CarriedInventoryReporter` remains small; the guard is an inline continue.
- `PlayerCameraDragUsePatch` is small and now routes all drag clearing through
  one helper or the bridge.
- New `RemoteProxyDragPolicy` is a pure 16-line decision type, one top-level
  type per file.
- No dead mechanisms left: the old read-only pickup block was already removed
  in #122; this cycle does not reintroduce it.
