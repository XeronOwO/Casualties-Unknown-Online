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