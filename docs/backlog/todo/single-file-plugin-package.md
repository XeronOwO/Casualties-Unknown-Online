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
