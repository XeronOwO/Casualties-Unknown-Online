# GameAdapter concrete-service dependency narrowing — self-check (2026-08-23)

Backlog §3.5 (open item: **GameAdapter testability / concrete service
dependencies**) called out that several adapter domain objects depended on
concrete Runtime services. This cycle closes the concrete-Runtime-service
portion of that item by making every GameAdapter deep module compose against
the existing narrow control interfaces, and adds those interfaces' missing
adapter-facing members.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `GameAdapter` / `GameAdapterDomains` | The adapter's constructor and domain container accepted concrete `SessionService`, `WorldService`, `ItemService`, `EntitySyncService`, `CharacterDataStore`. |
| Adapter deep modules | `CharacterDataSync`, `RunCoordinator`, `ItemWorldSync`, `TradeStateSync`, `EnemyCombatDirector`, `SpeechSync`, `WorldTimeSync`, etc. stored those concrete services as fields. `PlayerInteractionService` was also referenced only for its control surface. |
| Runtime control interfaces | `ISessionControl`, `IWorldControl`, `IItemControl`, `IEntitySyncControl`, `ICharacterDataControl` already existed for packet handlers/domains, but lacked several members the adapter needed. |
| Unity statics / spawn factories | Harmony patch classes and renderer/clone code still use `FindObjectsOfType`, `Resources.Load`, `Utils.Create`, private reflection — intentionally left for a separate seam (see §6). |

## 2. Whole-family audit

- Every concrete Runtime service reference in `src/CasualtiesUnknownOnline.GameAdapter` was
  replaced by the corresponding control interface:
  `SessionService → ISessionControl`, `WorldService → IWorldControl`,
  `ItemService → IItemControl`, `EntitySyncService → IEntitySyncControl`,
  `CharacterDataStore → ICharacterDataControl`,
  `PlayerInteractionService → IPlayerInteractionControl` (the latter needed no
  new members — its existing control interface already carried the three events
  the adapter subscribes to).
- The interfaces gained only the members the adapter actually consumed:
  - `ISessionControl`: `IsRemoteInWorld`, `GetRemoteSpawnPos`, `ReportSceneState`, `SessionActivated`.
  - `IWorldControl`: `SendWorldJoin`, `SendWorldJoinTo`, `PublishWorldParams`.
  - `IItemControl`: `LayerModifierRandomState`, `CarriedInventoryReceived`.
  - `IEntitySyncControl`: `RemotePlayers`, `GetRemotePlayer`, `PublishLocalState`, `MarkLocalAttackSwing`, `RemoteJoined`.
  - `ICharacterDataControl`: `ReportCharacterData`, `ClearSavedCharacters`, and the `CharacterDataReceived` / `HostCharacterDataReceived` / `LimbStateEventReceived` / `CharacterSoundReceived` events.
- No behavior, wire format, protocol version, handler logic, DI registration
  order, or state ownership changed. The concrete services still implement
  these interfaces; the adapter just no longer depends on the concrete types.
- No duplicate mechanisms or dead wrappers were introduced.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Adapter session dependency | Concrete `SessionService` → `ISessionControl` | All `*-Control` interface additions + 24 GameAdapter files |
| Adapter world dependency | Concrete `WorldService` → `IWorldControl` | `IWorldControl` additions + 11 GameAdapter files |
| Adapter item dependency | Concrete `ItemService` → `IItemControl` | `IItemControl` additions + 19 GameAdapter files |
| Adapter entity dependency | Concrete `EntitySyncService` → `IEntitySyncControl` | `IEntitySyncControl` additions + 8 GameAdapter files |
| Adapter character dependency | Concrete `CharacterDataStore` → `ICharacterDataControl` | `ICharacterDataControl` additions + 4 GameAdapter files |
| Adapter player-interaction dependency | Concrete `PlayerInteractionService` → `IPlayerInteractionControl` (already had the needed events) | 2 GameAdapter files |
| L0 proof | New DI surface contract test exercises every newly exposed member through the runtime container | `AdapterControlSurfaceTests.cs` (5 tests) |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1266 passed / 0 failed (full suite) |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | clean |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir <real game dir>` | passed (real game directory only) |
| Protocol | unchanged |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- `AdapterControlSurfaceTests` resolves `ISessionControl`, `IWorldControl`,
  `IItemControl`, `IEntitySyncControl`, and `ICharacterDataControl` from the
  real `CuoBootstrap` composition and exercises the new adapter-facing methods
  through those interfaces.
- Static proof: a repository-wide search confirms no GameAdapter `.cs` file
  references any of the listed concrete service types anymore
  (`SessionService`, `WorldService`, `ItemService`, `EntitySyncService`,
  `CharacterDataStore`, or `PlayerInteractionService`).
- No manual dual-side acceptance is required for this compile-time dependency
  narrowing refactor (user rule 2026-08-16).

## 6. Remaining scope (explicitly not closed)

The Unity seam is still a separate follow-up: Harmony patches and clone
renderer classes continue to call `FindObjectsOfType`, `Resources.Load`,
`Utils.Create`, and private reflection directly. Making that seam injectable
for a true L0 GameAdapter harness remains in the backlog; the concrete-Runtime
service dependency slice is now closed.

## 7. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.

## 8. Structure review

- No new top-level state-owning classes; the additions are interface members
  and one test type.
- Touched files remain within the 600-line gate (architecture gate passed).
- No new expression-state bool fields.
- No mutable shared state introduced; the concrete services already own their
  state and the adapter reads through the same interfaces.
- No dead mechanism left behind: the old concrete-service constructor types are
  gone from the adapter, not co-existing with interface variants.
