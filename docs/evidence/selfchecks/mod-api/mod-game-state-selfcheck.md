# Mod ReadGameState — read-only player character projection self-check

Owner cycle: backlog Phase 4 Mod API remainder, TODO "ReadGameState". Decision:
implement the first read-only game-state projection as a mod-facing surface
over the already-arriving 1 Hz character stream (`RemoteVitalsService` /
`RemoteInventoryService`), enforce `ModPermission.ReadGameState` at the read
surface, and do **not** add a wire message or protocol bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Permission flag | `ModPermission.ReadGameState` was already declared, validated, carried through the handshake, but had no live enforcement point (mod-api.md §1/§3) |
| 2 | Character stream | The 1 Hz `CharacterDataMsg` / `CharacterHealthMsg` already carries remote vitals and carried/worn inventory, including recursive container contents |
| 3 | Existing read-only caches | `RemoteVitalsService` / `RemoteInventoryService` already project that stream into immutable, session-scoped per-SteamID snapshots for the Online UI |
| 4 | Session presence | `SessionService.LocalInWorld` / `IsRemoteInWorld` already answer whether a player is in the world vs lobby |
| 5 | No Unity leak boundary | `CUO.Abstractions` is the only assembly mods may reference; the new DTOs must contain no game/Unity types |

## 2. Design

- New `IModContext.GameState` exposes `IModGameState` to every mod.
- `IModGameState` is gated by `ReadGameState`: `CanRead` reports the declared
  flag, and `TryGetPlayer` refuses (false + log) when it is missing.
- The adapter reads the same `RemoteVitalsService` / `RemoteInventoryService`
  caches the Online UI uses — no second source of truth, no wire change.
- `IModPlayerState` is an immutable copy holding `SteamId`, `InWorld`,
  `IModPlayerVitals?` and `IModPlayerInventory?`. Missing halves are null until
  their snapshot arrives.
- `IModPlayerInventory` / `IModInventoryEntry` project the recursive item tree
  (`InstanceId`, `ItemId`, `SlotIndex`, `Condition`, `Favourited`,
  recursive `Contents`), matching the clone renderer's data shape.
- A remote leaving the world or a session end clears the underlying caches, so
  the mod surface can never expose a stale player from a previous run.
- This slice intentionally covers the **remote player character** projection.
  Local-player character state and world/item/block/entity global state remain
  future slices; the same read-only DTO pattern is the forward path.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Permission | `IModGameState.CanRead` reflects `ReadGameState`; `TryGetPlayer` re-checks and logs | `ModGameStateTests.MissingReadGameStatePermission_IsRefused` |
| Host guest report | Host adapter exposes a guest's vitals + inventory from the cached 1 Hz report | `ModGameStateTests.WithPermission_ExposesRemotePlayerVitalsAndInventory` + direct cache asserts |
| Guest host snapshot | Guest adapter exposes the host's projected state | `ModGameStateTests.Guest_ExposesHostPlayerState` |
| No snapshot | `TryGetPlayer` returns false before any data arrives | `ModGameStateTests.TryGetPlayer_ReturnsFalse_WhenNoSnapshotHasArrived` |
| Leave-world lifecycle | Remote leaving the world clears the mod-visible projection | `ModGameStateTests.RemoteLeavingWorld_RemovesTheProjection` |
| No Unity leak | All exposed types are immutable Abstractions interfaces | New `IModGameState` / `IModPlayerState` / `IModPlayerVitals` / `IModPlayerInventory` / `IModInventoryEntry` files |
| No wire change | No `NetMsg`, no protocol/direction-table change | `ProtocolVersion` stays 31; no protocol file touched |
| Structure | New runtime code is a single new partial file; nested adapter/results are container types | `ModService.GameState.cs`; `tools/check-architecture.ps1` |

## 4. Verification

- **L0 integration**: `ModGameStateTests` (5 tests) drives the production
  composition root (`TestNode` + fakes), including permission refusal, host
  guest-report projection, guest host-snapshot projection, no-snapshot false,
  and leave-world clear.
- **Existing coverage**: `RemoteVitalsServiceTests` / `RemoteInventoryServiceTests`
  already lock the underlying projection semantics.
- **Code gates**: `dotnet build`, `dotnet test` (1148 passed), `dotnet format`,
  check-architecture / check-event-replay / check-entity-event-dispatch.
- **Development-period rule**: L0 simulation + static evidence,
  `no manual acceptance`.
