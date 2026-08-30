# Nested Container-Content Sync — Self-Check (2026-08-16)

Delivery fact sheet for backlog item **#120**: an item moved inside a carried
container (a backpack's contents shifted between slots/containers/limb pouches)
now travels as one dedicated parent-container fact event instead of riding the
1 Hz character snapshot silently. The clone fact table updates immediately, and
the `[ContainerLoad]` "no event sync" warning is gone.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | A body-internal `LoadItem` (drag-UI LoadItem/UnloadItem, Container.cs:46-66) previously skipped reporting and logged `[ContainerLoad] … no event sync, the 1 Hz character snapshot carries it`. | `ContainerItemSync.cs` (OnLoadedIntoContainer, was the `else` branch) |
| 2 | `ItemStateCodec.CaptureItem` already captures a container recursively — `Contents` is the full recursive wire tree. | `ItemStateCodec.cs` CaptureItem |
| 3 | The carried-fact chain (`ItemCarriedSync`, NetMsg 63) is the existing host→guest event for use/slot/pickup; its receiver `CloneFactTable.ApplyCarriedSync` updates the owner's clone fact table immediately. | `ItemCarriedSyncService.cs`; `CloneFactTable.cs` |
| 4 | The guest report side (`ItemSlot`, NetMsg 61) establishes the "owner's own body is the fact source; host records + relays" accept-with-correction shape for carried facts. | `ItemActionSync.cs` SendItemSlot/FireItemSlotReceived |
| 5 | The host's transfer table is the authoritative carried record for corrections and reconnect restores. | `ItemArbitration.cs` (_transferred) |
| 6 | `CloneFactTable.ApplyCarriedSync` previously matched top-level `data.Items` only — a nested container's change could not be applied in place. | `CloneFactTable.cs` pre-change `FindIndex` |
| 7 | The `[CharSync]` divergence monitor only compared top-level items, so nested container changes carried by the 1 Hz snapshot were invisible to the event-missed warning. | `CloneFactTable.cs` WarnOnDivergence pre-change |

Whole-family audit: the same full-parent-fact shape now covers host and guest
body-internal moves, top-level and nested containers, and tracked/untracked
parents (untracked falls back to the report as fact, exactly like use/slot).
World-contents movement (items moved inside a WORLD container) already had an
event (`ItemDropped` with ParentItemId) and is unchanged.

## 2. Design

- **One move = one message.** The moved item is never reported on its own.
  Instead the PARENT container's full recursive capture is the event: guest → host
  `ItemContainerContentMsg` (NetMsg 95), host records and relays it as the
  existing `ItemCarriedSync` fact. A host-side move is the authority and
  broadcasts `ItemCarriedSync` directly.
- **Exact rebuild, not a delta.** `ItemArbitration.RecordContainerContent`
  adopts the reported full capture onto the transfer-table entry (top-level
  state + contents replaced); `CloneFactTable` replaces the matched node in the
  recursive carried tree wholesale.
- **Nested targets work.** `CloneFactTable.ApplyCarriedSync` now searches the
  contents tree recursively, so a changed pouch inside a backpack replaces the
  pouch node instead of appending a phantom top-level item.
- **Divergence monitor extends to nested contents.** A 1 Hz snapshot that
  carries a nested-content change the event chain missed now logs a `[CharSync]`
  divergence, matching the user rule that timed-snapshot fallback must be loud.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Body-internal move capture | `ContainerItemSync` reports/broadcasts the parent container's full fact instead of warning | `ContainerItemSync.cs` OnLoadedIntoContainer |
| Guest report | `ItemContainerContentMsg` (NetMsg 95) guest→host, direction-gated | `ItemActionSync.cs`; `PacketReceiver.cs`; `DirectionTests.cs` |
| Host record | `ItemArbitration.RecordContainerContent` adopts the full capture onto the transfer-table entry | `ItemArbitration.cs` |
| Host relay | Host broadcasts the adopted parent as `ItemCarriedSync` (owner = reporter) | `ItemActionSync.cs` FireItemContainerContentReceived |
| Clone fact apply | `CloneFactTable.ApplyCarriedSync` replaces top-level or nested nodes recursively | `CloneFactTable.cs`; reflective `CloneFactTableNestedCarriedSyncTests` |
| Divergence monitoring | `WarnOnDivergence` compares nested content trees | `CloneFactTable.cs` ContentsEquivalent |
| Wire compatibility | ProtocolVersion 19 → 20 refuses mixed-version sessions (v19 peer would miss nested events until the 1 Hz snapshot) | `ProtocolVersion.cs` |
| Structure | no file crosses the 600-line gate; one top-level type per file | `tools/check-architecture.ps1` |

## 4. Verification design

- **L0 wire/simulation:** `ItemContainerContentSyncTests` — a tracked guest
  container move reaches the other guest as `ItemCarriedSync` with the full
  recursive capture, the host's transfer-table contents update, and the owner is
  excluded; an untracked parent falls back to the reported fact.
- **L0 protocol:** `NetPacketTests.ItemContainerContent_RoundTripsRecursiveCapture`
  locks the new message's payload shape.
- **L0 reflection (adapter):** `CloneFactTableNestedCarriedSyncTests` — a nested
  container inside a backpack is replaced in place, not appended top-level.
- **Direction guard:** `DirectionTests` classifies `ItemContainerContent` as
  guest→host (host accepts, guest drops).
- **Contract guards:** `PatchContractChecker` covers the unchanged container
  patch surfaces; `dotnet test` still passes with the new message enumeration.
- **Static evidence:** call-site references in `ContainerItemSync.cs` and
  `ItemStateCodec.cs` above.
- **Runtime evidence:** development-period rule — L0 simulation + reflective
  patch surface + static evidence + real-game-dir deploy; **no manual
  acceptance** (user 2026-08-16 mandate).

## 5. Accepted residuals (recorded, not re-discovered)

- The clone renderer still does not display container contents visually
  (`ContainerItemSync.cs` former comment): this slice makes the fact table /
  reconnect restore correct, but "opening another player's inventory" UI is
  still part of the open Direct player interaction / Online UI work.
- Order-only content reordering within one container is not treated as a
  divergence (the monitor compares recursive id sets, not order) — there is no
  observed semantic that depends on content order in the fact table.