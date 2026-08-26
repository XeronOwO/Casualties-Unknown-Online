# Native remote backpack view + shuttle-door trigger sound live replay — self-check (2026-08-26)

Follow-up to the previous remote-inventory cycle: the user correctly observed
that (1) the shuttle-door trigger sound was still missing on guests, because
the earlier fix only touched the host executor, and (2) the "open other
player's backpack" path still used the CUO custom UI instead of the game's
native radial backpack. This cycle fixes both.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Shuttle door live event replay | `EntityEventSync.OnRemoteEntityEvent` → guest side `TrapVisualReplay.Replay` → `ReplayShuttleDoor` |
| 2 | Native radial inventory body source | `InvButton.body` getter returns `PlayerCamera.main.body` (`InvButton.cs:12-17`) |
| 3 | Native worn-item generation | `PlayerCamera.UpdateWearables` uses `this.body.GetAllWearables()` (`PlayerCamera.cs:2767-2783`) |
| 4 | Native radial position/name | `PlayerCamera.HandleWhileDragging` runs every frame; KrokMP used the same seam to attach the radial to a focused body |
| 5 | Remote render clone | `RemotePlayerRenderer.TryGetRemoteBody` exposes the per-SteamId `Body` display clone |
| 6 | Remote clone container rendering | `CloneInventoryRenderer` materialises top-level clone items only; nested container children were not rendered |
| 7 | Host-rule take toggle | Existing `[HostRules] AllowRemoteInventoryTake` from the previous cycle |

## 2. Changes

- **Shuttle-door trigger sound live replay** — `TrapVisualReplay.ReplayShuttleDoor`
  now branches by `ShuttleDoorReplayState.ShouldReplayTriggerSound(elapsedSeconds)`.
  For live relays (`elapsed <= 0`) it runs `TrapStateActions.ApplyShuttleDoor`,
  which sets the activated latch and plays `shuttleNotice`; the door's own
  `Update` then drives the animation and the later `shuttleOpen` at 2 s. Late
  joiner snapshots (`elapsed > 0`) keep the elapsed jump with no old sounds.
- **Native remote backpack view** — new `RemoteBackpackView` static focus and
  `RemoteBackpackCoordinator` (session → render clone → open native radial):
  - `InvButtonBodyPatch` routes `InvButton.body` to the focused remote clone while the radial is open.
  - `PlayerCameraUpdateWearablesPatch` scopes `PlayerCamera.body` to the focused clone only while building worn-item buttons.
  - `PlayerCameraHandleWhileDraggingPatch` keeps the radial menu attached to the focused clone and disables the local radial circle.
  - `PlayerCameraTryPerformRadialActionPatch` and `PlayerCameraTryPickupFromUIPatch` keep the view read-only: the remote clone's items are display proxies, never mutated.
- **Recursive clone container rendering** — `CloneInventoryRenderer.RestoreRemoteContents`
  materialises snapshot `Contents` under a remote clone container item, marks the
  whole subtree with `RemoteCloneRender`, and disables physics/colliders. This
  makes the native container/backpack capable of showing nested content in the
  display clone.
- **UI action** — "Open backpack" appears in the Players page, quick panel and
  right-click context menu. It closes the CUO windows/panels and calls
  `IGameAdapter.OpenRemoteBackpack`; the custom item list remains available via
  the existing "View items" detail path.

## 3. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Guest hears trigger sound on live door relay | `ReplayShuttleDoor` calls `ApplyShuttleDoor` for `elapsed <= 0` | `ShuttleDoorReplayStateTests.ShouldReplayTriggerSound_LiveRelayOnly` (0/-1 true, >0 false) |
| Host executor still gets same sound | `ApplyShuttleDoor` unchanged | Existing `TrapStateActions.ApplyShuttleDoor` |
| Late joiner does not hear old sound | Elapsed branch still jumps without sound | `ShuttleDoorReplayState.FromElapsed` state tests + code path |
| Native radial reads remote clone | `InvButtonBodyPatch` postfix routes `InvButton.body` | Patch contract resolution in `PatchContractTests` |
| Native worn buttons use remote body | `PlayerCameraUpdateWearablesPatch` prefix swaps body only for the call | Patch contract resolution in `PatchContractTests` |
| Radial follows remote clone | `PlayerCameraHandleWhileDraggingPatch` moves `radialMenu` to focus | Static code path; no L0 Unity UI harness by design |
| Remote view is read-only | `TryPerformRadialAction` + `TryPickupFromUI` blocked while open | Patch contract resolution in `PatchContractTests` |
| Remote clone container shows contents | `CloneInventoryRenderer.RestoreRemoteContents` | Static code path; no L0 Unity prefab harness by design |
| UI opens native backpack | `IGameAdapter.OpenRemoteBackpack` + UI action + localizations | `RemoteBackpackContractTests` |
| No wire/event-matrix break | No NetMsg/ProtocolVersion/event-row changes | `git diff` contains only adapter/plugin/runtime-utility/tests/docs |

## 4. Verification (development-period, no manual acceptance)

- **L0**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **1544 passed / 0 failed**.
- **Gates**: `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1` all pass.
- **Format**: `dotnet format` run; `--verify-no-changes` only flags the
  gitignored generated `obj/.../MyPluginInfo.cs`.
- **Runtime verification**: development-period rule — L0 simulation + static
  evidence, **no manual acceptance**.

## 5. Structure review

- New top-level types are small: `RemoteBackpackView` (~80 lines),
  `RemoteBackpackCoordinator` (~45), each patch class under 60 lines.
- `CloneInventoryRenderer` remains under the 600-line gate; it gains two small
  private helpers.
- No new expression-state bools; the native view focus is a transient static
  presentation state owned by the adapter, cleared when the radial closes or
  the clone disappears.
- Dead mechanisms: the old custom remote inventory UI is kept as the explicit
  detail fallback, not duplicated in the native path.
