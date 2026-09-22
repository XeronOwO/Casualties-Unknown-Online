# Compress the plugin into a host shell

- Status: Review
- Priority: Medium
- Category: Architecture / layering
- Source: Loomi architecture review (2026-09-20), item 6
- Related: `review/application-layer-first-slice.md`, `review/adapter-capability-ports.md`, `docs/development/agent-reference.md` (repository layout)

## Problem (evidence)

`src/CasualtiesUnknownOnline.Plugin/Plugin.cs` was 594 lines when this ticket was written (584 at HEAD
`5f75f1c3`, measured) and holds four jobs at once: the BepInEx
entry and host configuration, the Online UI overlay (the IMGUI `OnGUI` path plus the waiting
overlay), six config editors (`IpDirectConfigEditor`, `PlayerColorConfigEditor`,
`HostRulesConfigEditor`, `LoggingConfigEditor`, `LanguageConfigEditor`,
`ConfigurationProfileStore`), the service registration including the adapter
(`PluginDependencyRegistrar.Apply` as `extraRegistrations`), and direct adapter-state writes — the
clearest being `GameAdapterImpl.SkipIntro = true` on the join path.

`src/CasualtiesUnknownOnline.Plugin/CasualtiesUnknownOnline.Plugin.csproj` also references
`Assembly-CSharp` plus nine UnityEngine modules. The repository layout rule says the Game Adapter is
the ONLY project referencing game assemblies, so the entry currently breaks its own layout rule. The
csproj comment records why the reference is direct (the NuGet UnityEngine.Modules 5.6.0 package
mismatched the game's Unity 2022.3 runtime and Unity could not instantiate the plugin script), which
is also why the reference cannot simply be deleted: the game types the entry uses must move first.

## Stages

1. Registration moves into a dedicated adapter host; the plugin keeps only BepInEx/Unity lifecycle
   forwarding and host configuration.
2. The IMGUI overlay and the config editors move out of the entry (a presentation area — project or
   folder decided in the change, on the evidence of what they actually reference).
3. `SkipIntro` becomes an intent on a port — e.g. `IJoinFlowPresentation.PrepareForDirectJoin()` —
   so the entry stops writing an adapter static.
4. Drop `Assembly-CSharp` from the plugin project and let the layout rule hold; keep the Unity module
   references the entry genuinely needs, with the reason recorded where the next reader looks.

## Acceptance

- The plugin project references no game assembly, and the layout rule has no exception left to
  explain.
- `Plugin.cs` is off the 600-line watchlist because responsibilities left, not because text was
  reformatted.
- Startup and join behaviour is unchanged: the existing suites stay green and the deployed artifact
  identity is checked as usual. The overlay's look and feel is a user-acceptance item.

## Notes

Order matters: stages 1–3 must land before stage 4, otherwise the reference is deleted while code
still needs the game types and the build breaks in a way that invites a quick re-add.

## What landed (2026-09-22)

All four stages, in the required order, in one change. The stage-4 premise had changed under the
ticket: by the time this landed the presentation half no longer touched a game type, so removing
`Assembly-CSharp` was a measurement, not a migration (stage 4 below).

### 1. Registration moved into the adapter's own composition

`PluginDependencyRegistrar.cs` (413 → 275 lines) is the BepInEx config → DI bridge plus the
plugin-side editors only: it binds the `[Sync]`/`[Logging]`/`[Respawn]`/`[HostRules]`/`[Save]`/
`[UI]`/`[IpDirect]`/`[Diagnostics]` entries, registers the five config editors,
`ConfigurationProfileStore` and the `IHostRulesEditor` replacement, and then calls
`GameAdapterComposition.Register(services)` at exactly the position the adapter block occupied
before.

`src/CasualtiesUnknownOnline.GameAdapter/GameAdapterComposition.cs` (180 lines, new) owns
everything the adapter owns: the `GameAdapter` singleton, its twelve capability ports (registered
exactly once each from that singleton), `WorldLibraryService`, `NativeWorldFacts`,
`LiveTrapLayoutSource`, the six `IMod*` replacements, the nine content-binding providers,
`PlayerInteractionVisibility`, `LocalCharacterCapture` and `MapsterMapper.IMapper`. Registration
ORDER is the `ICuoService` pump order, so it is preserved line for line: adapter first, the services
that read its ports after it, the content providers after the adapter's install step. The plugin
names no adapter type now — it resolves ports, and the one entry point it calls is
`GameAdapterComposition.Register`.

### 2. The presentation left the entry

| New unit | Lines | What it owns |
| --- | --- | --- |
| `OnlineUiHost.cs` (plugin project) | 259 | The overlay's composition (services, `OnlineUiActions`, `IpDirectActions`, `LocationPingInputHandler`, the ~25 overlay delegates) plus its frame-time half: `Update()` — console + quick-panel hotkeys, location-ping input, ESC-close suppression, the native input modal — and `Draw()` — start-gate overlay, overlay, mod windows. |
| `LobbySwitchActions.cs` (plugin project) | 109 | The lobby policy (`TryJoin`/`TryCreate`/`TryLeave`/`CanJoin`/`CanCreate`) and the last-refusal reason, so the Steam callbacks and the Online UI buttons run ONE policy instead of two copies. |

The six config editors were already separate files before this change; what left the entry is every
reference to them.

`Plugin.cs` is 584 → 294 lines: the BepInEx entry, host configuration (paths, the F6 bind,
`+connect_lobby` parsing), Unity lifecycle forwarding and the Steam callbacks. It consumes four
ports and no concrete adapter type:

| Port the shell consumes | Why the shell itself needs it |
| --- | --- |
| `IGameIntegrationLifecycle` | `OnApplicationQuit` forwarding |
| `ICarryPresentationPump` | `LateUpdate` |
| `IJoinFlowPresentation` | the `+connect_lobby` direct-join intent |
| `IAdapterCapabilityQuery` | the startup capability-report log line |

### 3. Two ports replaced the two direct adapter writes

- `IJoinFlowPresentation.PrepareForDirectJoin()` (new port) replaces `GameAdapter.SkipIntro = true`.
  The intent is the port; the adapter's field is private and `PreRunScriptIntroSkipPatch` reads the
  adapter's own `internal static IsIntroSkipped`, so the public static setter is gone.
- `ICarryPresentationPump.PinCarriedPresentation()` (new port) replaces the public
  `GameAdapter.LateUpdateCarryPresentation()`; the implementation is explicit-interface only, so the
  port is the single entry point.

`AdapterCapabilityPortShapeTests` now pins 12 ports and 16 members, and the registration check reads
`GameAdapterComposition.cs` (the composition moved out of the plugin, so the gate's source pin moved
with it).

### 4. The plugin project references no game assembly

`CasualtiesUnknownOnline.Plugin.csproj` (77 → 83 lines) no longer references `Assembly-CSharp`. The
claim is measured on the built assemblies (assembly-reference tables read from the PE metadata), not
asserted from the sources:

| Built assembly | Assembly references |
| --- | --- |
| `CasualtiesUnknownOnline.dll` (the plugin) | `mscorlib`, `System.Core`, `BepInEx`, `UnityEngine.CoreModule`, `UnityEngine.IMGUIModule`, `UnityEngine.InputLegacyModule`, `UnityEngine.TextRenderingModule`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`, CUO `Abstractions` / `Runtime` / `GameAdapter` — fourteen names, **no `Assembly-CSharp`** |
| `CasualtiesUnknownOnline.GameAdapter.dll` (control) | the same list plus `Assembly-CSharp` (and 0Harmony, Mapster, GameState, Protocol, the game's UI/Physics2D/ParticleSystem/Animation/Audio/Tilemap modules) |

The Unity module references were re-measured one at a time — remove one, rebuild the project, read
the compiler — instead of being carried as "probably needed":

| Module | Measurement |
| --- | --- |
| `UnityEngine.SubsystemsModule`, `UnityEngine.SharedInternalsModule` | unused — removed, project still builds |
| `UnityEngine`, `UnityEngine.CoreModule` | needed (BepInEx's `BaseUnityPlugin` derives from `MonoBehaviour`; `Application`, `Debug`, `LogType`) |
| `UnityEngine.InputLegacyModule` | needed (`Input.GetKeyDown`, `KeyCode` for the hotkeys) |
| `UnityEngine.IMGUIModule` | needed (the overlay's IMGUI pass) |
| `UnityEngine.TextRenderingModule` | needed (`GUIStyle` / text in that pass) |
| `netstandard` | kept DEFENSIVELY, not as a measured requirement: removing it builds clean and leaves the plugin's reference table identical (measured 2026-09-22); it pins the game's own copy instead of a targeting-pack one if the compiler ever emits the reference |

The csproj comment records the rule, the module-by-module reason and the measurement to re-run
before adding a module back.

### The layout rule is now a gate

`GameAssemblyReferenceGateTests` (new, 7 cases) fails the build when a project outside the declared
set references a game assembly. Its scan surface is DERIVED, not hand-written: the new
`ProjectDirectionPolicy.SolutionProjects` reads `CasualtiesUnknownOnline.slnx` once for both gates, so
a new project is covered the moment the solution lists it. The declared game-binding set is
`CasualtiesUnknownOnline.GameAdapter` (the framework's only game-binding layer) and
`CasualtiesUnknownOnline.PinyinSearch` (the satellite mod's game-binding half); test and tool
projects stay unconstrained through the existing `ProjectDirectionPolicy.ConsumerProjects`. It
carries a census floor (13 projects), a positive half (every declared binder is still in the
solution and still binds), and five synthetic matcher cases (including `Assembly-CSharp-firstpass`,
which must NOT be dragged in by a prefix match).

### Files

- New: `src/CasualtiesUnknownOnline.GameAdapter/GameAdapterComposition.cs`,
  `src/CasualtiesUnknownOnline.Plugin/OnlineUiHost.cs`,
  `src/CasualtiesUnknownOnline.Plugin/LobbySwitchActions.cs`,
  `src/CasualtiesUnknownOnline.Runtime/GameAdapter/IJoinFlowPresentation.cs`,
  `src/CasualtiesUnknownOnline.Runtime/GameAdapter/ICarryPresentationPump.cs`,
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/GameAssemblyReferenceGateTests.cs`.
- Changed: `Plugin.cs`, `PluginDependencyRegistrar.cs`, `CasualtiesUnknownOnline.Plugin.csproj`,
  `GameAdapter.cs`, `Patches/PreRunScriptIntroSkipPatch.cs`,
  `Runtime/GameAdapter/IGameAdapter.cs`,
  `tests/CasualtiesUnknownOnline.Tests/Patching/AdapterCapabilityPortShapeTests.cs`,
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ProjectDirectionPolicy.cs`,
  `docs/evidence/sync-coverage-evidence.json` (row W6's quote followed the moved registration).

### Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings, 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx --verify-no-changes --include <every changed .cs>`:
  exit 0.
- Normative gates: 100/100 (93 before this change; the new gate contributes 7).
- Focused: `--filter "FullyQualifiedName~Patching|FullyQualifiedName~KernelReplicationLayerBoundary"`
  338/338; `--filter "Category!=Integration"` 2097/2097.
- Two mutation controls were re-run for the two ports this change adds (2026-09-22): declaring `ProbeRegrowth()` back on the aggregate AND implementing it turns `Aggregate_DeclaresNoMemberOfItsOwn` red (17/18), and deleting the `IJoinFlowPresentation` registration turns `EveryPort_IsRegisteredExactlyOnceFromTheAdapterSingleton` red (17/18); both files were restored byte-identically (md5 compared).
- Behaviour preservation is the whole claim, and the ONE declared difference is the logger category:
  messages the UI and the lobby policy emit now come from `OnlineUiHost` / `LobbySwitchActions`
  instead of `Plugin`. Same text, same levels, same trigger points.

### Limits

- No real-machine evidence of any kind: this is an internal boundary change, and nothing here claims
  the host or a guest behaves differently. The overlay's look and feel stays a user-acceptance item,
  as the ticket's acceptance section says.
- The plugin project still references the GameAdapter PROJECT, and `GameAdapterComposition.Register`
  is its ONLY reason to (verified: deleting the project reference fails with `CS0234` while a raw
  `Reference` to the same DLL builds clean), so the shell and the adapter still deploy and reload
  together; that edge is declared in `ProjectDirectionPolicy.AllowedReferences` and is not a
  game-assembly reference.
- The presentation host applies the IP-direct display name and the local player colour while it builds
  the overlay. Both writes are STILL inside `Awake` (the host is constructed at `_onlineUi = new
  OnlineUiHost(...)`, inside `Awake`) and still before the `ICuoService.Initialize`/`Start` pump; what
  moved is their position WITHIN `Awake`. Their only readers (`SessionPeerMaintenance.CreateHandshakeMsg`,
  `HandshakeHandler`, the IP-direct start validation) run on later frames, so the gap is not observable.
- Whether the container resolves the new ports is pinned by source shape plus the compile-time
  guarantee only — the test project does not reference the plugin project, so there is no
  container-level test that resolves them from the real composition.
