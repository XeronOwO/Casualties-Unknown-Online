# Single-file package for the core plugin

- Status: Todo
- Priority: Low-Medium
- Category: Build / release packaging
- Source: the user's 2026-10-07 backlog request (marked optional) — publish one file that carries CUO itself,
  with the extension mods (pinyin search, the sample mod, and the rest) left out, and with the technical
  questions the user names — `AssemblyLoadContext` and resource-file streams — answered in the design.
- Related: `tools/deploy.ps1` (what a deployment ships today), `review/pinyin-search-standalone-mod.md` and
  `src/CasualtiesUnknownOnline.ModExample` (the extensions that stay out of the core package),
  `future/phase5-tooling-ecosystem.md` (packaging/install tooling), `review/plugin-host-shell.md` (the entry's
  own layering work), `tools/verify-deploy.ps1` (the identity story that must keep working)

## What is observed

- A deployment today writes the plugin plus ~30 dependency assemblies (`deploy.ps1` reports 35 deployed files,
  34 of them matching this tree's build output), and the deployed set is what `verify-deploy.ps1` compares
  against the build output.

## What is asked

One artifact a user can drop in and start from, containing the CUO core only:

- **One file** for the plugin and everything it needs at runtime, with the mod-facing extensions shipped
  separately (they are mods, not the core).
- The design must state how the plugin resolves its own dependencies at runtime (an
  `AssemblyLoadContext`-style resolver, or an assembly-resolve handler) and how resources are read (embedded
  or packed streams rather than loose files), including the case where the game's own loader enumerates what
  it will load.

## What is not known yet

- Whether BepInEx 5's plugin loader will load a single packed assembly at all, and what the game's Unity
  runtime does with an assembly that carries dependencies the loader did not see; this is the feasibility
  question that comes before the packaging choice, and it is answered by a probe, not by reading.
- Whether the GAME's assembly references (which only the Game Adapter may take) can survive packing, given the
  adapter loads against the game's own assemblies at runtime.
- How `verify-deploy.ps1`'s identity comparison should treat a packed artifact: the same one build → one
  artifact rule has to keep holding, or the ticket states what replaces it.

## Required work

1. Feasibility probe first: pack one build and load it in the real client; record what the loader refuses
   before designing anything.
2. Design the package's contents and its boundary with the extension mods, and state the compatibility rule
   for a mod that needs the core's assemblies.
3. Keep the release identity checkable: the packaged artifact must still name the commit it was built from,
   and the deployment check must still prove the running client is that build.
4. Document the difference for a user (one file versus the current deployment) in the human docs, in both
   language blocks.

## Non-goals

- Not an installer, not an auto-updater and not a mod manager: those are
  `future/phase5-tooling-ecosystem.md`.
- Not the entry's own refactor (`review/plugin-host-shell.md`); this ticket consumes it rather than replacing
  it.
## Cost, read from the ticket's own requirements (2026-10-18)

Picked up and put back down in the same cycle, with the reason recorded so it is not mistaken for a quick win:

1. **The feasibility probe is the gate, and it is a runtime reading** — pack one build and find out what BepInEx
   5's loader and Unity's own assembly loading do with it. Nothing about the package can be designed until that
   answers, and it needs the real client, a deployment, and a log read.
2. **The release identity story has to keep working** — `verify-deploy.ps1` compares the deployed set against the
   build output, and a packed artifact has to preserve "one build → one artifact named for its commit" rather than
   quietly dropping the check that proves a running client is this build.
3. **The user-facing documentation is bilingual by rule** — `docs/en/` and `docs/zh/` carry the same page set and
   `standard/alignment.txt` records each confirmed pair, so a packaging page lands on both sides in one change.

So this is a probe-plus-design-plus-docs change, not a build-script tweak: at least two cycles, with the first
ending at a reading rather than a package. That is why the next cycle should not open it expecting a one-round
close, and why a Medium-High ticket is the better use of the next cycle if one is closeable.

## The feasibility probe, answered on the real client (2026-10-24)

A BepInEx 5 plugin (`[BepInPlugin("cuo.probe.singlefile", ...)]`) was built against the game's own
`BepInEx.dll` and Unity assemblies, with ONE file deployed: its dependency (`ProbeHelper.dll`) was embedded as a
manifest resource and never copied beside it. An `AssemblyResolve` handler served that name from the embedded
bytes. The deployed plugins directory held only `ProbePlugin.dll`; the real client was launched and its
`BepInEx/LogOutput.log` read:

```
[Info   :   BepInEx] Loading [Single-file probe 0.0.1]
[Info   :SingleFileProbe] [SingleFileProbe] static constructor: the embedded resolver is registered
[Info   :SingleFileProbe] [SingleFileProbe] resolve requested for ProbeHelper
[Info   :SingleFileProbe] [SingleFileProbe] loading ProbeHelper from 4608 embedded bytes
[Info   :SingleFileProbe] [SingleFileProbe] helper says: helper loaded from the embedded resource
```

So the loader's answer is **yes**: BepInEx 5 loads a plugin assembly whose dependency is not on disk, and the
dependency is served from the plugin's own embedded resource at the first reference. Two facts the design has to
carry:

- **The handler must be registered in a STATIC constructor, not in `Awake`.** The first attempt registered it in
  `Awake` and failed with `FileNotFoundException: Could not load file or assembly 'ProbeHelper'`: the JIT
  resolves the helper token while compiling the method that names it, so the handler registered inside that same
  method runs too late. Registered in the type's static constructor it is in place before the method is compiled.
- **The handler is asked for OTHER assemblies too** (`System.ComponentModel.DataAnnotations` in this run) and
  must answer `null` for them so normal probing continues.

The probe also names what remains open, which is the design half: whether CUO's SEVERAL assemblies can be packed
into one (type identity across a merge, not just an embedded side-assembly), whether the Game Adapter's
references to the game's own assemblies survive (they are the game's, not ours, and stay external), how the
~30 loose dependencies are embedded, and how `verify-deploy.ps1`'s one-build-one-artifact check follows a packed
file (the plugin already logs `build 0.1.0+<commit>`, so the identity itself is not the unknown 鈥?the artifact
comparison is). No package work is started in this cycle; this section is the reading the design starts from.

## The CUO-scale packed artifact, and the shape it proves wrong (2026-10-24)

`tools/package-single-file.ps1` packs the real build: the plugin DLL plus 30 managed dependencies embedded as
`Packed.<name>.dll` (all seven CUO assemblies, the Microsoft/System/protobuf set, Mapster and Steamworks.NET),
written to `CasualtiesUnknownOnline.dll`; `steam_api64.dll` stays loose because it is native. The deployed plugin
directory was moved aside and replaced by that ONE file plus `steam_api64.dll`; the client was launched and the
logs read.

- BepInEx accepted the file: `[Info : BepInEx] Loading [Casualties Unknown: Online 0.1.0]` (the plugin
  attributes are readable from the packed assembly).
- The plugin was NOT instantiated. The game's own `Player.log` (BepInEx's log carries no error because the
  plugin never reached its own code) says only `The script 'CasualtiesUnknownOnline.Plugin' could not be
  instantiated!`; the main menu came up, and the HotRepl evaluator's `ping` declaration failed with
  `(1,1): InteractiveHost` because the CUO types it names were never loaded.

**Why, and what it means for the design.** The plugin type's FIELDS name CUO and dependency types
(`ServiceProvider`, `ICuoService`, `SteamService`, `OnlineUiHost`, ...), so the runtime must resolve those types
while LOADING the type, before any static constructor of that type can run. A resolver installed in the plugin
class's static constructor is therefore too late for the plugin class itself; it worked in the earlier
leaf-dependency probe only because the helper was named in a METHOD body, whose tokens resolve when the method is
compiled, not when the type loads.

So the single-file package needs the entry split first: a tiny `[BepInPlugin]` type with NO packed-type
signatures (its static constructor installs the resolver, its `Awake` adds the real component by name), and the
real plugin class loading only afterwards. That is the `review/plugin-host-shell.md` refactor's own shape, and
this ticket consumes it, so the packaging work is BLOCKED on that refactor, not on anything the packer lacks.

The deployment was restored (the packed directory was a temporary replacement) and `verify-deploy.ps1` passes
against the normal set again. The packer stays committed as the probe's tool so the next cycle can rerun the
same reading after the entry split.

## The module-initializer escape hatch, tried and refused by this runtime (2026-10-24)

Before accepting the entry split, a second attempt tried to make the packed artifact work WITHOUT touching the
plugin's shape: a `PackedAssemblyResolver` (register the `AssemblyResolve` handler, then eagerly
`Assembly.Load` every embedded assembly) plus a packer step that injects a `<Module>` static constructor
calling it 閳?the one hook that would run before any type of the assembly loads.

The client gave the same answer as before, with one new fact: the packed assembly is accepted
(`Loading [Casualties Unknown: Online 0.1.0]`), the injected module initializer does NOT run early enough on
this runtime, and the plugin type still fails to instantiate 閳?`Player.log` again says only
`The script 'CasualtiesUnknownOnline.Plugin' could not be instantiated!` while BepInEx logs nothing else. So
Unity's Mono does not give a module initializer the "before every type" position this design needs, and the entry
split is the ONLY route: it is a real prerequisite, not a preference. The resolver source and the packer's
injection step were NOT committed (they cannot earn their keep until the split lands).

## The entry split landed, and the packed artifact runs (2026-10-24)

The refactor is in `src/CasualtiesUnknownOnline.Plugin/`: `Plugin` is now the entry only (the plugin attributes,
the static logger, a static constructor installing `PackedAssemblyResolver`, and an `Awake` that adds the host
and hands it the logger and config), and every startup field and lifecycle handler moved to the new
`PluginHost : MonoBehaviour`. The resolver (`PackedAssemblies.cs`) preloads every embedded assembly and serves
anything it carries; in the normal deployment it is inert.

Verified on the real client:

- **Normal deployment** (the full loose set, deployed): the client starts as before 閳?`Plugin
  CasualtiesUnknownOnline is loaded 閳?build 0.1.0+074a92bd...-dirty`, the capability report is OK, the Online UI's
  lobby controls are available, and an agent-driven session ran (ping, create-lobby, start-run, role Host).
- **Packed deployment** (ONE `CasualtiesUnknownOnline.dll` with 30 embedded managed assemblies plus the native
  `steam_api64.dll`): BepInEx loads it and CUO STARTS 閳?the same `is loaded 閳?build ...`, the capability report,
  the Online UI line, the Pinyin Search mod beside it (its `Abstractions` dependency served by the resolver), and
  the driver's ping / create-lobby / state all work against the packed client.

That answers the ticket's feasibility question completely: a single managed file carrying CUO and its
dependencies loads and runs on this BepInEx/Unity. What remains is the package's own work 閳?its contents and
boundary with the extension mods, the release-identity rule for `verify-deploy.ps1` (one packed file versus a
recursive build-output comparison), the native `steam_api64.dll` staying loose and being named for the user, and
the one-file-versus-deployment page in both language blocks. The ticket stays `todo` for that work; it is no
longer blocked.

The acceptance driver needed one lookup update in the same change (`tools/acceptance/driver/InProcessDriver.cs`):
it used to reach the Online UI through `Plugin._onlineUi`, and now finds the `PluginHost` component on the
entry's GameObject first.