# Remote inventory UI follow-up — openable containers + host take toggle (2026-08-26)

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/selfchecks/MANIFEST.md` and
> `docs/architecture-evolution/protocol.md` before citing.

> **Superseded note**: the "native not reused" boundary below was later
> corrected by `docs/selfchecks/native-remote-backpack-and-door-sound-selfcheck.md`
> (#118), which ports the native radial backpack view for remote players.
> This sheet remains the record for the collapsible-container + host-rule slice.

Closes the backlog "Remote-player inventory UI should reuse the game backpack
UI" as far as the CUO architecture permits. The native game radial/backpack
surface is hard-wired to `PlayerCamera.main.body` and its drag/drop path
mutates the local body, so it cannot be used to view a remote player's
authoritative inventory without either hijacking the local camera/body or
operating on display-only clone items. The Online UI remains the remote
inventory surface; this cycle makes that surface container-openable and adds a
host rule that controls the cross-player take operation.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Remote inventory cache | `RemoteInventoryService` fills from the 1 Hz character stream; already recursive via `RemoteInventorySnapshot` / `RemoteInventoryEntry.Contents` |
| 2 | Existing take authority | `PlayerInventoryTakeService.HandleTakeRequest` validates against host character snapshots and sends `PlayerInventoryTransferMsg` |
| 3 | Host rules surface | `HostRulesOptions` + `IHostRules` + `HostRulesService` + `PluginDependencyRegistrar` + `HostRulesConfigEditor` |
| 4 | Online UI member projection | `OnlineUiMemberProjection.canTake` is the single eligibility rule consumed by Players page, quick panel and right-click menu |
| 5 | Remote inventory drawer | `OnlineUiMemberListDrawer.DrawInventoryEntry` renders recursive inventory lines under the expanded member |

## 2. Changes

- **Host rule `AllowRemoteInventoryTake` (default `true`)** — host-only
  `[HostRules]` toggle shown on the Admin page. When `false`, the Online UI
  hides every remote-inventory Take action and the host authority refuses all
  `PlayerInventoryTakeRequestMsg` operations. The default `true` preserves the
  existing cooperative rule (unconscious/dead bodies may be looted).
- **Host enforcement** — `PlayerInteractionService` now composes
  `IHostRules` into `PlayerInventoryTakeService`; the take service checks the
  rule at decision time, so a runtime config edit takes effect without a
  restart (same hot-reload pattern as the other host rules).
- **Openable container rows** — the remote-inventory drawer now renders a
  container entry as a collapsible row (`Open` / `Close`) instead of always
  expanding every nested line. Expansion state is per
  `(ownerSteamId, instanceId)`, stored in `OnlineUiWindowState.ExpandedContainers`;
  it is presentation-only and never crosses the Runtime/wire.
- **No wire change** — no new `NetMsg`, no `ProtocolVersion` bump, no
  event/item/entity matrix row touched.

## 3. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| UI hides take when rule disabled | `canTake` returns empty when `allowRemoteInventoryTake=false` | `OnlineUiMemberProjectionTests.RemoteInventoryTakeDisabled_HidesNonLocalTakeActions` |
| Default keeps existing take behavior | Rule defaults to `true`; existing dead/unconscious take tests still pass | `HostRulesOptions.AllowRemoteInventoryTake = true` + existing `DeadUnconsciousRemoteCanCarryAndTakeSlotItems` |
| Host refuses when rule disabled | `PlayerInventoryTakeService` checks `IHostRules.AllowRemoteInventoryTake` before snapshot work | `PlayerInteractionServiceTests.Take_RemoteInventoryTakeDisabled_RefusesEvenUnconsciousTarget` |
| Hot reload composes new flag | `HostRulesService` reads the option monitor at property access | `HostRulesPolicyTests` compose + hot-reload assertions |
| Admin page exposes rule | `OnlineUiAdminDrawer` editable + read-only rows; localization keys | `admin.rule_allow_remote_inventory_take` en/zh |
| Containers open/close in UI | `OnlineUiMemberListDrawer` toggles `ExpandedContainers` per owner/item | Static UI code + localization keys `member.open_container` / `member.close_container` |
| No protocol/event surfaces changed | No NetMsg/version/matrix edits | `git diff` contains only Runtime/Plugin/tests/docs/config files |

## 4. Verification (development-period, no manual acceptance)

- **L0**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **1538 passed / 0 failed**.
- **Gates**: `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1` all pass.
- **Format**: `dotnet format` run; `--verify-no-changes` only flags the
  gitignored generated `obj/.../MyPluginInfo.cs`.
- **Runtime verification**: development-period rule — L0 simulation + static
  evidence, **no manual acceptance**.

## 5. Structure review

- `PlayerInventoryTakeService` remains under the 600-line gate; it gains one
  read-only `IHostRules` field and one guard branch.
- `OnlineUiMemberListDrawer` gains one collapsible-draw helper and one key
  helper; no new state beyond the presentation-only container set.
- `OnlineUiWindowState` grows one `HashSet<string>`; it is UI-local state, not
  expression-state bools.
- Dead mechanisms: none. The existing `RemoteInventoryService` / take channel /
  host-rule pipeline are composed, not duplicated.

## 6. Accepted boundaries

- The game's native radial/backpack UI is **not** reused for a remote player:
  `InvButton` reads `PlayerCamera.main.body` and the drag/drop path mutates the
  local body, so opening it on a remote clone would present/mutate display-only
  clone items rather than the authoritative remote inventory.
- Nested container items remain **view/open-only** in the Online UI; taking is
  still limited to top-level backpack/hand-slot items (existing take boundary).
- The new rule is a host-local session toggle, not per-player inventory
  permission or a wire-synced setting.
