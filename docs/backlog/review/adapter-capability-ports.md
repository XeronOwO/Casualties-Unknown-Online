# Split IGameAdapter into capability ports

- Status: Review
- Priority: Medium
- Category: Architecture / adapter seam
- Source: Loomi architecture review (2026-09-20), item 3
- Related: `todo/patch-bridge-domain-ports.md`, `review/adapter-capability-catalog.md`

## Problem (evidence)

`src/CasualtiesUnknownOnline.Runtime/GameAdapter/IGameAdapter.cs` was one 125-line interface whose 21
members span eight unrelated concerns:

- lifecycle and patch installation (`ProbeGame`, `Install`, `Uninstall`, `OnApplicationQuit`),
- the capability report (`CapabilityReport`),
- the start gate (`IsWaitingForReady`, `WaitingText`) and the world-presence read
  (`IsInWorldOrGenerating`),
- world bootstrap parameters (`CaptureWorldParams`, `ApplyWorldParams`),
- local heal-item queries (`HasLocalHealItem`, `GetLocalHealItems`),
- the trader recruit request (`TryRequestTraderRecruit`),
- native UI modal blocking (`SetOnlineUiModal`, `SetOnlineUiScopedBlocks`,
  `SetOnlineUiEscapeSurfaceVisible`),
- remote presentation (`OpenRemoteBackpack`/`CloseRemoteBackpack`,
  `OpenRemoteMedical`/`CloseRemoteMedical`) and the head anchor (`TryGetRemoteHeadPosition`).

Every consumer saw all of it, every test double implemented all of it, and a version adapter had to
implement all of it even when a capability is absent on that build. Runtime also reaches into adapter
internals through `InternalsVisibleTo`, so the seam was a shared wall rather than a narrow port.

## Goal

Capability ports instead of one widening interface. Names settled in the change, roughly:
`IGameIntegrationLifecycle`, `IWorldBootstrapPort`, `IRemoteInventoryPresentation`,
`IRemoteMedicalPresentation`, `IPlayerAnchorQuery`, `INativeInputBlocker`, and — once
`review/adapter-capability-catalog.md` lands — `IAdapterCapabilityQuery`. A consumer resolves the ports
it actually uses; a version adapter implements per capability rather than the whole surface.

## Acceptance

- Behaviour-preserving refactor: no call site changes meaning, and the adapter's own tests plus the
  patch-contract checks stay green.
- A shape gate fails when a new method is added back onto the aggregate interface, so the old surface
  cannot regrow while the ports are being introduced.
- The `InternalsVisibleTo` surface is reviewed in the same change and reduced where a port can carry
  the read instead; anything that stays records why.

## Notes

The interface is small in lines but wide in responsibility — the split is about who must implement
what, not about file size. Do not merge this with the patch-bridge split: they are different seams
with different consumers.

## What landed (2026-09-21)

The split was decided by a CALL-SITE census, not by the member list: every member's call sites across
`src/` and `tests/` were enumerated first, and only members a call site reaches became ports. Fourteen
of the twenty-one did; the seven that no call site reached were removed from the boundary instead of
being moved into a port nobody would resolve (a port forces every implementation to carry a member no
consumer calls — the exact cost this ticket exists to remove).

### The ten ports and their consumers

| Port | Members | Consumers |
| --- | --- | --- |
| `IGameIntegrationLifecycle` | `OnApplicationQuit` | `Plugin.OnApplicationQuit` (Unity's quit broadcast) |
| `IAdapterCapabilityQuery` | `CapabilityReport` | `Plugin` startup log (catalog stage 1's report) |
| `IWorldPresenceQuery` | `IsInWorldOrGenerating` | `Plugin`'s two lobby-switch guards, `IpDirectActions.CanStart`, `OnlineUiWorldsDrawer` (Worlds page), `PluginDependencyRegistrar` (`WorldLibraryService.worldActive`) |
| `IStartGateState` | `IsWaitingForReady`, `WaitingText` | `Plugin.OnGUI` (gate) and the start-gate overlay |
| `ILocalHealItemQuery` | `HasLocalHealItem`, `GetLocalHealItems` | `OnlineUiActions` |
| `ITraderRecruitRequest` | `TryRequestTraderRecruit` | `OnlineUiActions` |
| `INativeInputBlocker` | `SetOnlineUiModal`, `SetOnlineUiScopedBlocks`, `SetOnlineUiEscapeSurfaceVisible` | `Plugin.Update` (modal + ESC surface), `OnlineUiOverlay.Draw` (scoped blocks) |
| `IRemoteInventoryPresentation` | `OpenRemoteBackpack` | `OnlineUiActions.OpenRemoteBackpackFromUi` |
| `IRemoteMedicalPresentation` | `OpenRemoteMedical` | `OnlineUiActions.OpenRemoteMedicalFromUi` |
| `IPlayerAnchorQuery` | `TryGetRemoteHeadPosition` | `OnlineUiOverlay`'s nameplate / off-screen-arrow pass |

`IWorldBootstrapPort` from this ticket's goal text is NOT among them: `CaptureWorldParams` and
`ApplyWorldParams` had no call site — `WorldParamsService` owns both boundary hooks itself (its own
generation boundary calls `CaptureAtBoundary`, and `EnsureGuestApplied` calls `Apply`) — so a port
would have been an entry point nothing resolves. `IAdapterCapabilityQuery` landed with the catalog's
stage 1, which is what gave the plugin's capability-report read a real port to resolve.

### The aggregate is a composition and declares nothing

`IGameAdapter` now lists the ten ports plus `IDisposable` and declares no member of its own (39 lines,
from 125). It stays as the seam's composition identity: the class declaration
`GameAdapter : IGameAdapter, …` is the compile-time proof that ONE object implements all ten ports, and
the shape gate pins the composition against drift. It is deliberately NOT registered in the plugin's
composition root — nothing in the tree resolves the whole adapter any more (the plugin resolves five
ports plus the concrete adapter), and a registration no consumer resolves would be exactly the dead
surface this change removes elsewhere. `Plugin` keeps the concrete adapter for exactly one call,
`LateUpdateCarryPresentation`, an adapter Update-pump detail rather than a consumer capability
(`review/plugin-host-shell.md` took that coupling and landed it on 2026-09-22: the call is now the
`ICarryPresentationPump` port, the composition is `GameAdapterComposition` in the adapter project,
and the aggregate composes twelve ports).

### The seven removals, each with its reason

| Removed member | Why |
| --- | --- |
| `ProbeGame`, `Install`, `Uninstall` | Driven by `ICuoService.Initialize` (`if (ProbeGame()) Install();` and `Stop() => Uninstall()`) on the same instance. `GameAdapter` keeps the three methods as private members; nothing resolved them through the boundary. |
| `CaptureWorldParams`, `ApplyWorldParams` | No call site anywhere in `src/` or `tests/`. The guest path is live through the adapter's own bridge (`EnsureGuestApplied`, `ResetGenStreamToBaseline`); the host capture is live inside `WorldParamsService`. |
| `CloseRemoteBackpack`, `CloseRemoteMedical` | No call site. The views close themselves: `RemoteBackpackView.ClearIfStale` / `RemoteMedicalView.Close` run from their coordinators' Update, and opening one native view closes the other. `RemoteBackpackCoordinator.Close` became unreachable with the port and was deleted. |

### The shape gate and its controls

`tests/CasualtiesUnknownOnline.Tests/Patching/AdapterCapabilityPortShapeTests.cs` (16 cases) pins: the
aggregate declares no member of its own (the census reads methods, properties and events alike); every
port declares exactly its census and no member name is shared by two ports; the aggregate composes
exactly the pinned ports plus `IDisposable`; the composition carries exactly 14 members; every port is
registered EXACTLY ONCE from the one adapter singleton in `PluginDependencyRegistrar` (counted, so an
unwired port and a duplicate whose last descriptor wins both fail; read as source, because the tests
project does not reference the plugin project) — that pin moved to `GameAdapterComposition` on
2026-09-22 (`review/plugin-host-shell.md`); and a synthetic composition that declares a method, a
property and an event is flagged (the matcher's own contract). Two mutation controls were run on the
real tree: declaring `ProbeRegrowth()` back on the aggregate and deleting one port registration each
turned the gate red; restoring them turned it green.

### InternalsVisibleTo: reviewed with a census, kept with reasons

The census was taken empirically: removing `[assembly: InternalsVisibleTo("CasualtiesUnknownOnline.GameAdapter")]`
from `Runtime/AssemblyInfo.cs` and building makes the compiler name every Runtime member the adapter's
code references and cannot see — 22 distinct internal names: `AdapterCapabilityKind`,
`AdapterCapabilityReport`,
`BlockBreakArbitration`, `BlockBreakPendingState`, `DropPendingState`, `EnemyHealthReconcile`,
`FacePresentationVitals`, `IRestoredWorldFactSink`, `ItemFollowDecision`, `LiveWorldWriteOutcome`,
`ModStatusStore.StatusPresence`, `NativeFieldWrite`, `PatchContract`, `PatchVerificationFailure`,
`RemoteCharacterPresentation`, `RestoredWorldFactReplay`, `RunMenuReturnRequest`, `SettledStreamThrottle`,
`TradeStockState`, `TrapActionOutcome`, `TrapDropPendingState` and `BreakVerdict.Verdict`.

They are Runtime-OWNED VALUES the adapter applies: the patch-contract and capability-report facts, the
mod status/building tables, and the item/world/trap/trade decision results the adapter turns into game
writes. The one read this change could move to a port — the plugin's capability report — is port-carried
now (`IAdapterCapabilityQuery`), so the grant's only remaining consumers are the adapter's own
implementation paths. A port per value family would have to either make those types public (publishing
the Runtime's internal decision model) or carry a copy (two truths for one decision), so the grant
stays as the narrower seam and the census plus this reason is recorded in decision 212.

### The one file-size consequence

`Plugin.cs` grew past the 600-line aggregate gate by the port plumbing alone (596 → 619), so the
start-gate waiting panel moved into its own IMGUI surface, `src/CasualtiesUnknownOnline.Plugin/StartGateOverlay.cs`
(44 lines), drawn from `Plugin.OnGUI` at the same point in the frame with the same text: `Plugin.cs` is
584 lines. `OnlineUiOverlay` is 573 → 576 (a first version that took the panel instead crossed the cap
at 613 — a discarded intermediate that never reached the tree — which is why the panel is its own type
rather than another method on the overlay).

### Files

Runtime: `GameAdapter/IGameAdapter.cs` (composition) plus one file per port
(`IGameIntegrationLifecycle`, `IAdapterCapabilityQuery`, `IWorldPresenceQuery`, `IStartGateState`,
`ILocalHealItemQuery`, `ITraderRecruitRequest`, `INativeInputBlocker`, `IRemoteInventoryPresentation`,
`IRemoteMedicalPresentation`, `IPlayerAnchorQuery`). Adapter: `GameAdapter.cs` (per-port explicit
implementations), `RemoteBackpackCoordinator.cs` (dead `Close` wrapper deleted). Plugin:
`PluginDependencyRegistrar.cs` (port registrations), `Plugin.cs`, `OnlineUiActions.cs`,
`OnlineUiOverlay.cs`, `OnlineUiContext.cs`, `OnlineUiWorldsDrawer.cs`, `IpDirectActions.cs`,
`StartGateOverlay.cs` (new). Tests: the new shape gate plus the three reflection contract tests
re-pointed at `IRemoteInventoryPresentation`, `IRemoteMedicalPresentation` and `INativeInputBlocker`.

### Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings, 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx` (with build): 3789/3789 passed; the normative gates
  93/93. Focused set (shape gate + the three contract classes): 30/30.
- `dotnet format CasualtiesUnknownOnline.slnx --verify-no-changes --include <changed files>`: exit 0.
- The gate's own red/green controls above are reproducible by re-applying either mutation for one run.

### Limits

This is an internal refactor: no real-machine, dual-client or in-game claim is made, and the deployed
artifacts were verified by identity (hash/version), not by playing. The DI wiring is asserted as source
shape and by compile-time implementation of the aggregate, not by resolving the plugin's container in a
test (the tests project does not reference the plugin project). The adapter's read of Runtime internals
is recorded, not reduced, for the reasons above.
