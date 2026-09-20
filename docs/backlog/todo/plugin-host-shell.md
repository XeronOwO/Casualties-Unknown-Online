# Compress the plugin into a host shell

- Status: Todo
- Priority: Medium
- Category: Architecture / layering
- Source: Loomi architecture review (2026-09-20), item 6
- Related: `todo/application-layer-first-slice.md`, `docs/development/agent-reference.md` (repository layout)

## Problem (evidence)

`src/CasualtiesUnknownOnline.Plugin/Plugin.cs` is 594 lines and holds four jobs at once: the BepInEx
entry and host configuration, the Online UI overlay (the IMGUI `OnGUI` path plus the waiting
overlay), seven config editors (`IpDirectConfigEditor`, `PlayerColorConfigEditor`,
`HostRulesConfigEditor`, `LoggingConfigEditor`, `LanguageConfigEditor`, `PinyinSearchConfigEditor`,
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
