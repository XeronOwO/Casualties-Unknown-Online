# Plugin host shell — the entry stops owning four jobs

Date: 2026-09-22
Scope: `review/plugin-host-shell.md`, all four stages. The plugin project stops being the
registration root, the UI compositor, the lobby policy and a game-assembly consumer; the adapter's
composition moves next to the adapter, the presentation and the policy become their own units, and
the layout rule becomes a gate (decision 213).

## What landed

- **`Plugin.cs` 584 → 294 lines**, holding BepInEx host configuration (paths, the F6 bind,
  `+connect_lobby` parsing), Unity lifecycle forwarding and the Steam callbacks. It consumes four
  ports — `IGameIntegrationLifecycle` (quit forwarding), `ICarryPresentationPump` (`LateUpdate`),
  `IJoinFlowPresentation` (the direct-join intent), `IAdapterCapabilityQuery` (the startup report log
  line) — and names no concrete adapter type.
- **Registration split at the ownership line.** `PluginDependencyRegistrar.cs` (413 → 275 lines) is
  the BepInEx config → DI bridge plus the plugin-side editors (`LoggingConfigEditor`,
  `LocalizationConfigEditor`, `PlayerColorConfigEditor`, `HostRulesConfigEditor`,
  `IpDirectConfigEditor`, `ConfigurationProfileStore`, the `IHostRulesEditor` replacement).
  `GameAdapterComposition.cs` (180 lines, in `CasualtiesUnknownOnline.GameAdapter`) owns the adapter
  singleton, its twelve ports, `WorldLibraryService`, `NativeWorldFacts`, `LiveTrapLayoutSource`, the
  six `IMod*` replacements, the nine content providers, `PlayerInteractionVisibility`,
  `LocalCharacterCapture` and `MapsterMapper.IMapper`. The plugin calls `Register(services)` once, at
  the exact position the extracted block occupied, so the registration order — which IS the
  `ICuoService` pump order — did not move.
- **The presentation and the policy are their own units.** `OnlineUiHost.cs` (259 lines) resolves the
  overlay's services once, builds `OnlineUiActions`/`IpDirectActions`/`LocationPingInputHandler` and
  the ~25 overlay delegates, and owns the frame-time half (`Update()`: console and quick-panel
  hotkeys, location-ping input, ESC-close suppression, the native input modal) and the IMGUI pass
  (`Draw()`: start-gate overlay, overlay, mod windows). `LobbySwitchActions.cs` (109 lines) owns the
  create/join/leave policy and the last-refusal reason, so the Steam callbacks and the UI buttons run
  one policy instead of two copies.
- **Two ports replaced the two direct adapter writes.** `IJoinFlowPresentation.PrepareForDirectJoin()`
  replaces `GameAdapter.SkipIntro = true` (the field is private now; `PreRunScriptIntroSkipPatch`
  reads `internal static IsIntroSkipped`), and `ICarryPresentationPump.PinCarriedPresentation()`
  replaces the public `LateUpdateCarryPresentation` with an explicit-interface implementation. The
  aggregate composes twelve ports / sixteen members.
- **No game assembly in the plugin project.** `Assembly-CSharp` is gone from the csproj, and the
  Unity module references were re-measured by removing one at a time and rebuilding:
  `UnityEngine.SubsystemsModule` and `UnityEngine.SharedInternalsModule` were unused and are gone;
  `UnityEngine`, `UnityEngine.CoreModule`, `UnityEngine.InputLegacyModule`,
  `UnityEngine.IMGUIModule`, `UnityEngine.TextRenderingModule` stay, each with its reason in the csproj
  comment; the game's `netstandard` stays DEFENSIVELY (removing it builds clean and leaves the
  reference table identical — measured 2026-09-22, so it is recorded as belt-and-braces rather than a
  requirement).
- **The layout rule is a gate.** `GameAssemblyReferenceGateTests` (7 cases) fails the build when a
  project outside the declared set references `Assembly-CSharp`; its scan surface comes from the one
  shared solution reader `ProjectDirectionPolicy.SolutionProjects`, its declared binders are
  `CasualtiesUnknownOnline.GameAdapter` and `CasualtiesUnknownOnline.PinyinSearch`, and
  `ProjectDirectionPolicy.ConsumerProjects` keeps tests and tools unconstrained.

## Mechanism inventory

| Mechanism | Change | Evidence |
| --- | --- | --- |
| Adapter composition | Registrations extracted verbatim from `PluginDependencyRegistrar.Apply` into `GameAdapterComposition.Register`, same order, same descriptors | `dotnet build`; the plugin's startup path resolves the same services |
| Plugin shell | `Awake` builds the provider, resolves its four ports, creates `LobbySwitchActions` + `OnlineUiHost`, wires the Steam callbacks; `Update`/`LateUpdate`/`OnGUI`/`OnApplicationQuit`/`OnDisable` forward | `Plugin.cs` (294 lines) |
| Presentation host | Overlay composition + input/modal rules + IMGUI pass, moved as-is | `OnlineUiHost.cs` (259 lines) |
| Lobby policy | `TryJoin`/`TryCreate`/`TryLeave`/`CanJoin`/`CanCreate` + `LastError`, used by the Steam callbacks and the overlay delegates | `LobbySwitchActions.cs` (109 lines) |
| Direct-join intent | Port `IJoinFlowPresentation.PrepareForDirectJoin`; `GameAdapter.SkipIntro` public setter removed, field private, patch reads `IsIntroSkipped` | `dotnet build` (the old static is unnameable from the plugin) |
| Carry pump | Port `ICarryPresentationPump.PinCarriedPresentation`; the public method became an explicit-interface implementation | `AdapterCapabilityPortShapeTests` (16 members) |
| Port census | 10 ports / 14 members → 12 ports / 16 members, registration pin re-pointed to `GameAdapterComposition.cs` | `AdapterCapabilityPortShapeTests` (18 cases, all green) |
| Game-assembly rule | The plugin DLL carries no `Assembly-CSharp` reference; the adapter DLL does (positive control) | PE metadata reference tables read from both built assemblies |
| Unity module set | Two unused modules dropped, five kept, each with a recorded reason | remove-one/rebuild sweep |

## Verification

| Check | Result |
| --- | --- |
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings, 0 errors |
| `dotnet format CasualtiesUnknownOnline.slnx --verify-no-changes --include <changed .cs>` | exit 0 |
| Normative gates (`CasualtiesUnknownOnline.NormativeGates.Tests`) | 100 total (93 + 7 new); 99 green in the frozen pre-commit tree — the single failure is `DeliveryChecklist_NoIncompleteRequiredBoxes`, whose last box stays unchecked until the post-review verification run |
| Focused `--filter "FullyQualifiedName~Patching\|FullyQualifiedName~KernelReplicationLayerBoundary"` | 338/338 |
| Fast loop `--filter "Category!=Integration"` | 2097/2097 (was 2095; the two-port shape census adds 2 rows) |
| Plugin assembly references | fourteen names: `mscorlib`, `System.Core`, `BepInEx`, `UnityEngine.CoreModule`, `UnityEngine.IMGUIModule`, `UnityEngine.InputLegacyModule`, `UnityEngine.TextRenderingModule`, `Microsoft.Extensions.DependencyInjection`, `.DependencyInjection.Abstractions`, `.Logging.Abstractions`, `.Options`, CUO `Abstractions`/`Runtime`/`GameAdapter` — **no `Assembly-CSharp`** |
| Mutation controls (re-run for the two new ports) | declaring `ProbeRegrowth()` back on the aggregate and implementing it → `Aggregate_DeclaresNoMemberOfItsOwn` red (17/18); deleting the `IJoinFlowPresentation` registration → `EveryPort_IsRegisteredExactlyOnceFromTheAdapterSingleton` red (17/18); both files restored with matching md5 |
| Evidence integrity | `SyncCoverageGateTests.SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine` failed on row W6's quote (the line moved out of `PluginDependencyRegistrar.cs`) and passes with the entry re-pointed to `GameAdapterComposition.cs` — the gate noticed the move before any doc did |

## Limits

- No real-machine evidence: this is an internal boundary change. Nothing here claims the host or a
  guest behaves differently, and the overlay's look and feel remains a user-acceptance item.
- The ONE declared behaviour difference is the logger category: UI and lobby messages now come from
  `OnlineUiHost`/`LobbySwitchActions` instead of `Plugin`. Same text, same levels, same triggers.
- The plugin still references the GameAdapter PROJECT (to call `GameAdapterComposition.Register`);
  that edge is declared in `ProjectDirectionPolicy.AllowedReferences` and is not a game-assembly
  reference.
- The IP-direct display name and the local player colour are applied from the presentation host's
  constructor, which runs inside `Awake` and before the `ICuoService` pump — the writes moved LATER
  WITHIN `Awake`, not out of it, and their readers all run on later frames (see the ticket's Limits
  for the reader list).
- Whether the container resolves the new ports is pinned by source shape plus the compile-time
  implementation of the composition; the test project does not reference the plugin project, so there
  is no container-level resolution test.
