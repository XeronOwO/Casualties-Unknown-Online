# Game Adapter: capability ports

Date: 2026-09-21
Scope: ticket `docs/backlog/review/adapter-capability-ports.md` — the Game Adapter boundary
(`src/CasualtiesUnknownOnline.Runtime/GameAdapter/IGameAdapter.cs`) split from one 21-member interface
into ten capability ports with a frozen, member-free composition. Decision 212 records the ruling.
Independent adversarial review of this cycle: `%TEMP%\cuo-review-adapter-capability-ports.md`
(0 blockers / 0 majors / 2 minors / 4 nits — every number reproduced independently, and all six
findings fixed in the same commit: the dead `IGameAdapter` DI registration, four `current` selfcheck
rows still naming pre-split members, the event-inclusive census, the exactly-once registration pin, one
transient-state number, and one over-general census sentence).

## What landed

- **The boundary is ten ports.** `IGameAdapter` is now a composition (`IGameIntegrationLifecycle`,
  `IAdapterCapabilityQuery`, `IWorldPresenceQuery`, `IStartGateState`, `ILocalHealItemQuery`,
  `ITraderRecruitRequest`, `INativeInputBlocker`, `IRemoteInventoryPresentation`,
  `IRemoteMedicalPresentation`, `IPlayerAnchorQuery`, plus `IDisposable`) and declares no member of its
  own: 125 lines → 39. Each port is one file in `Runtime/GameAdapter/`, so a consumer resolves the
  capability it uses. The aggregate itself is NOT registered in the composition root (nothing resolves
  the whole adapter); the compile-time proof that one object implements the composition is the class
  declaration, and no dead registration is left inside the surface being narrowed.
- **The port set came from a call-site census, not from the member list.** Every member's call sites in
  `src/` and `tests/` were enumerated first (see the census table); only the fourteen a call site
  reaches became ports.
- **Seven members no call site reached were removed**, not ported — a port nobody resolves forces every
  implementation to carry it, which is the cost this ticket exists to remove.

## The census that decided the set

| Member at HEAD | Call site(s) | Outcome |
|---|---|---|
| `OnApplicationQuit` | `Plugin.OnApplicationQuit` (Unity's quit broadcast) | `IGameIntegrationLifecycle` |
| `CapabilityReport` | `Plugin` startup log line | `IAdapterCapabilityQuery` |
| `IsInWorldOrGenerating` | `Plugin`'s two lobby-switch guards, `IpDirectActions.CanStart`, `OnlineUiWorldsDrawer` (Worlds page), `PluginDependencyRegistrar` (`WorldLibraryService.worldActive`) | `IWorldPresenceQuery` |
| `IsWaitingForReady`, `WaitingText` | `Plugin.OnGUI` gate check and start-gate overlay | `IStartGateState` |
| `HasLocalHealItem`, `GetLocalHealItems` | `OnlineUiActions` (Online UI heal selector) | `ILocalHealItemQuery` |
| `TryRequestTraderRecruit` | `OnlineUiActions.RecruitPlayerFromUi` | `ITraderRecruitRequest` |
| `SetOnlineUiModal`, `SetOnlineUiEscapeSurfaceVisible` | `Plugin.Update` (modal + ESC-surface guard) | `INativeInputBlocker` |
| `SetOnlineUiScopedBlocks` | `OnlineUiOverlay.Draw` (scoped raycast blocks) | `INativeInputBlocker` |
| `OpenRemoteBackpack` | `OnlineUiActions.OpenRemoteBackpackFromUi` | `IRemoteInventoryPresentation` |
| `OpenRemoteMedical` | `OnlineUiActions.OpenRemoteMedicalFromUi` | `IRemoteMedicalPresentation` |
| `TryGetRemoteHeadPosition` | `OnlineUiOverlay`'s nameplate / off-screen-arrow pass | `IPlayerAnchorQuery` |
| `ProbeGame`, `Install`, `Uninstall` | none through the boundary — `ICuoService.Initialize` calls `if (ProbeGame()) Install();` and `Stop() => Uninstall()` on the same instance | removed from the boundary; kept as private members |
| `CaptureWorldParams`, `ApplyWorldParams` | none — `WorldParamsService` calls `CaptureAtBoundary()` from its own generation boundary and `EnsureGuestApplied` calls `Apply` | removed from the boundary (both domain paths stay live) |
| `CloseRemoteBackpack`, `CloseRemoteMedical` | none — the views close themselves (`RemoteBackpackView.ClearIfStale`, `RemoteMedicalCoordinator`'s staleness edges) and opening one closes the other | removed from the boundary; `RemoteBackpackCoordinator.Close` became unreachable and was deleted |

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| `IGameAdapter` | Composition of ten ports + `IDisposable`, zero own members | `AdapterCapabilityPortShapeTests.Aggregate_DeclaresNoMemberOfItsOwn`, `.Aggregate_ComposesExactlyThePinnedPorts` |
| Each port | One file, XML doc naming the capability and its consumers | `AdapterCapabilityPortShapeTests.Port_DeclaresExactlyItsPinnedMembers` (10 rows), `.NoMemberName_IsSharedByTwoPorts`, `.Composition_CarriesExactlyFourteenMembers` |
| `GameAdapter` | Per-port explicit implementations; `ProbeGame`/`Install`/`Uninstall` private; the removed `Close*`/world-param implementations deleted | `dotnet build` (the aggregate would not compile without every port), contract tests below |
| DI wiring | Every port registered EXACTLY ONCE from the one `GameAdapterImpl` singleton in `PluginDependencyRegistrar.Apply`; the aggregate is not registered (nothing resolves it) | `AdapterCapabilityPortShapeTests.EveryPort_IsRegisteredExactlyOnceFromTheAdapterSingleton` (source pin, counted) |
| Consumers | `Plugin` (5 ports + the concrete adapter for `LateUpdateCarryPresentation`), `OnlineUiActions` (4 ports), `OnlineUiOverlay`/`OnlineUiContext`/`OnlineUiWorldsDrawer` (anchor + world presence), `IpDirectActions` + the registrar (world presence) | `dotnet build`; the port registrations above |
| Native UI contracts | The three reflection contract tests point at the ports: `IRemoteInventoryPresentation`/`IRemoteMedicalPresentation`/`INativeInputBlocker`; the two behaviour-pinned natives keep asserting the adapter implements the port | `RemoteBackpackContractTests`, `RemoteMedicalContractTests`, `OnlineMenuInputGuardContractTests` |
| Gate matcher | A synthetic composition that declares a method, a property and an event is flagged; a clean one is not | `AdapterCapabilityPortShapeTests.AggregateCheck_FlagsAMemberDeclaredOnTheComposition` |
| Gate controls (red → green) | Declaring `ProbeRegrowth()` back on the aggregate turned `Aggregate_DeclaresNoMemberOfItsOwn` red; deleting one port registration turned `EveryPort_IsRegisteredExactlyOnceFromTheAdapterSingleton` red; reverting both turned the class green | same two tests, run in the landing cycle |
| `Plugin.cs` size | 596 → 584 lines: the start-gate waiting panel became its own IMGUI surface, `StartGateOverlay.cs` (the port plumbing alone put the class at 619, over the 600 aggregate gate; the first attempt put the panel on `OnlineUiOverlay` and crossed the cap at 613, so it is its own type) | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` (green) |

## InternalsVisibleTo: reviewed with an empirical census

The grant was measured, not guessed: removing
`[assembly: InternalsVisibleTo("CasualtiesUnknownOnline.GameAdapter")]` from
`src/CasualtiesUnknownOnline.Runtime/AssemblyInfo.cs` and building makes the compiler name every
Runtime member the adapter's code references and cannot see — 22 distinct internal names (`AdapterCapabilityKind`,
`AdapterCapabilityReport`, `BlockBreakArbitration`, `BlockBreakPendingState`, `DropPendingState`,
`EnemyHealthReconcile`, `FacePresentationVitals`, `IRestoredWorldFactSink`, `ItemFollowDecision`,
`LiveWorldWriteOutcome`, `ModStatusStore.StatusPresence`, `NativeFieldWrite`, `PatchContract`,
`PatchVerificationFailure`, `RemoteCharacterPresentation`, `RestoredWorldFactReplay`,
`RunMenuReturnRequest`, `SettledStreamThrottle`, `TradeStockState`, `TrapActionOutcome`,
`TrapDropPendingState`, `BreakVerdict.Verdict`). The file was restored byte-for-byte (`git diff` on it
empty) and the tree rebuilt.

They are Runtime-owned values the adapter applies: the patch-contract and capability-report facts, the
mod status/building tables, and the item/world/trap/trade decision results. The one read a port CAN
carry — the plugin's capability report — is port-carried now (`IAdapterCapabilityQuery`); the rest
stay behind the grant because a port would have to either publish those types (putting the Runtime's
internal decision model on the surface) or carry copies (two truths for one decision). Recorded in
decision 212 rather than reduced.

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings, 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx` (with build): 3789/3789 passed, normative gates 93/93.
  Focused set (shape gate + the three contract classes): 30/30.
- `dotnet format CasualtiesUnknownOnline.slnx --verify-no-changes --include <changed files>`: exit 0.
- Every new/edited file carries the repository's CRLF working-tree convention
  (`.gitattributes` `eol=crlf`); the LF a write tool produces was converted before the format run,
  which is why the format verify is clean.

## Limits

- No real-machine, dual-client or in-game claim: this is an internal boundary refactor, verified by
  build, tests, gates and static reading. The deploy step of the cycle verifies artifact identity
  (delivery id and hash) only — it does not run the game.
- The DI wiring is pinned as source shape plus the compile-time implementation of the composition; the
  plugin's container is not resolved in a test, because the tests project does not reference the plugin
  project.
- The adapter's read of Runtime internals is recorded with its census, not reduced (above).
- `Plugin.cs` keeps one concrete-adapter call (`LateUpdateCarryPresentation`) — an adapter Update-pump
  detail, not a consumer capability; `todo/plugin-host-shell.md` owns that coupling.
