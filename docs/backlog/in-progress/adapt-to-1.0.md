# Adapt the mod to the game's 1.0 release

- Status: In progress — the 1.0 adapter is reconciled and read on a live session (2026-11-09): the capability report passes, two 1.0 clients met in one world over the IP-direct transport, the two runtime differences are fixed, and the build now compiles against the 1.0 assemblies (the demo's reference cost a `MissingFieldException` every frame — 8 fps — and the release client now runs without it). The system map is complete (ten domain pages + `principles.md`, `docs/design/game/`). What remains: the backlog's release pass.
- Priority: Highest
- Category: Adapter / release
- Source: the user (2026-11-09): the project targets 1.0 only, the demo is no longer a supported target.

## The install

- 1.0 installs to `…\steamapps\common\Casualties Unknown` (the demo is `…\Casualties Unknown Demo`), app id 4576490.
- The release ships no `steam_appid.txt`; without it `SteamAPI.InitEx` fails and the mod cannot initialize Steam. The
  deploy script now writes it into the release install (the user confirmed the fix by hand first). The demo id is not
  written — the project does not target the demo.
- BepInEx is not shipped either; a fresh install needs the BepInEx 5 layer copied in (the demo's `winhttp.dll`,
  `doorstop_config.ini`, `BepInEx/` were reused for the first 1.0 launch).

## What 1.0 changed under the mod

1. `WorldGeneration.SetBlock` — an overload was added, so the name-only Harmony target became ambiguous and the whole
   adapter install refused. Fixed: the target names `(Vector2Int, ushort)` (`bce8fec1`); the capability report now
   passes.
2. `PlayerCamera.following` — the field is gone. The camera now follows `body.transform.position` directly
   (`PlayerCamera.cs:2144`). Our spectator camera writes and reads that field
   (`World/SpectatorCameraService.cs:247,475,477`), so every frame throws `MissingFieldException` in `FrameTick.Run`
   and the adapter's update aborts before anything else runs. Needs a 1.0-shaped spectator: drive the camera's own
   target (freecam or a Harmony hook on the position computation) instead of the removed field.
3. Newtonsoft's contract resolver walks a property whose `[Required]` attribute lives in
   `System.ComponentModel.Annotations, Version=5.0.0.0`; the game's Managed folder has no such assembly, so the language
   load throws `TypeLoadException` and the plugin reports `Language missing!`. Either the offending model drops the
   DataAnnotations attribute, or the assembly ships with the plugin (the demo's plugin folder carried 4.2.1 under the
   same name, so a binding story is needed).

## The map work

The release assemblies are decompiled once into `reversing/release-1.0/Assembly-CSharp/` (301 files); the game's own
systems are being written up in `docs/design/game/` (index + `worldgen-and-layers.md` + `items.md` first). The map is
the ground for every later compatibility decision: a 1.0 contract is judged from the decompiled line, not from the demo's
memory.

## Acceptance

- The capability report passes on a clean 1.0 install.
- The adapter's `Update` completes a frame with no exception (today every frame throws at `following`).
- The language catalogue loads (`Language missing!` gone).
- Three Steam-launched clients join one world and a run starts, on 1.0.

## Two clients in one world over IP direct (2026-11-09)

The Steam lobby path needs a second account that owns 1.0, which the user does not have; the sanctioned substitute is the
IP-direct transport with two local instances. - Host: the release install, launched through Steam; transport `IpDirect`, role `Host`, a run started.
- Guest: a byte copy of the install in a sibling folder (its own driver port and display name), launched directly; its
  driver answered `steamInit: true`; transport `IpDirect`, joined the host's address, role `Guest`.
- Both read `inWorld: true, gateWaiting: false` — one world, two players, on 1.0.

Evidence: `docs/evidence/acceptance/adapt-to-1.0-ip-direct-20261109.md`. The same batch closed the two runtime
differences (`SetBlock`'s overload pinned; the camera no longer reads the removed `following` field). What this does not
read: the Steam lobby path (needs a second owning account) and a real network hop (both instances are loopback).

## The third difference: the build referenced the demo (2026-11-09)

The first 1.0 session ran at 8 fps with `MissingFieldException: Field not found: single .BuildingEntity.health` once per
frame. The cause was the build, not the runtime: `references/Assembly-CSharp.dll` was still the demo's (879,104 bytes),
where `health` is a field; 1.0's assembly (1,030,656 bytes) exposes it as a property over `_health`
(`reversing/release-1.0/Assembly-CSharp/BuildingEntity.cs:12-20`), and the adapter's patches read `__instance.health`,
which a demo-referenced build compiles as `ldfld`.

The references now come from the release install (`CasualtiesUnknown_Data/Managed`, `BepInEx/core`, `Plugins/x86_64`),
and one suite fact moved with the game: the item declaration did not reach the two new public fields the game reads
(`secret`, `untradeable`, `Item.cs:7932`). They are declared through an optional `IModItemFlags` interface, so a mod
that never sets them still binds.

Reading: the release client, launched from the deployed `0.1.0+1ead765c` build, wrote `MissingFieldException` zero times
over its first 45 seconds and answered the driver (`steamInit: true`); the earlier build wrote it once per frame.
`BuildingEntity`'s per-frame `Update` is the game's own, so the frame rate returns with the field access gone. The
checked-in `references/` were replaced in the same change (`1ead765c`).

## Night mode split (2026-11-09)

The user is away; this ticket is worked as a queue of small tickets. Each one is self-contained: a chain read from the
1.0 code, a machine-readable acceptance row, implement → build → gates → suite → deploy → reading → commit. A ticket
that needs a product decision is parked with the question written into its own status, and the next one starts.

1. `1.0-item-customization-truth` — a customized item's label, colour and favourite mark reach the other clients
   (`PlayerCamera.cs:841/851/861` are the three writes; the wire's item facts today carry condition only).
2. `1.0-prosthetics` — a prosthetic replaces a limb through the game's own alert path (`Item.cs:749-830`); the host's
   record and the member's body agree afterwards.
3. `1.0-run-settings-extended` — the run settings 1.0 added reach the host-authoritative baseline and the save's
   `run.json`.
4. `1.0-endings` — the two endings (`MercEnding()`, `SurfaceBreachCutscene()`) are host events; members see the same
   ending and the save takes its final cut.
5. `1.0-dune-mode` — `DuneMode`'s 20-second object/material shuffle is host-authoritative.
6. `1.0-layers-6-11` — the deeper layers: the descent trigger `position.y < -halfHeight + 3.1f` (`:1138`), the layer-end
   cut on the layer 10 exit, and a run that visits them.
7. `capability-catalog-required-refusal` — the existing `in-progress/adapter-capability-catalog.md` stage 2: a required patch
   that fails refuses the install with a visible notice; an optional one degrades off.
8. `ip-direct-shows-the-purchase-notice` — the filed `todo/` ticket.

## Two clients in one world on the release build (2026-11-10, smoke)

Host and a second local instance (its own install copy, the same plugin) met over the IP-direct transport on
`0.1.0+7f74641db`-era code: host `IpDirect/Host`, guest `IpDirect/Guest`, both `inWorld: true, gateWaiting: false`
within six seconds of `start-run`.

- Host log: `Game Adapter patches installed and verified (282 targets)`, the capability report passes, and
  `[ItemCustody] rows=340 objects=340 mismatches=0`.
- Guest log: the capability report passes and `[ItemCustody] rows=340 objects=340 mismatches=0`.
- The only exception in either log is the known third-party one (HotRepl's NJsonSchema `TypeLoadException` for
  `System.ComponentModel.DataAnnotations.RequiredAttribute`), not the mod's.

That is the release-build smoke: the 1.0 install runs, both clients join over IP direct, and the world's item rows and
objects agree on both sides.

## The install surface: BepInEx on a fresh install (2026-11-10)

The release ships no BepInEx and no `steam_appid.txt`; without both the mod cannot load (Steam init fails, and the
plugin folder has no loader). `tools/deploy.ps1` writes the app id into a release install; the loader layer is now its
own step:

- `tools/install-bepinex.ps1 -GameDir <install>` copies a BepInEx 5.4.23.4 x64 layer (`winhttp.dll`,
  `doorstop_config.ini`, `BepInEx/{core,patchers,plugins}`) into the install, verifies the five files the preloader
  needs, and is idempotent (a complete layer is reported and left alone).
- Source order: `-SourceDir <a layer>` → the demo install's layer when it exists on this machine → `-Download` (the
  pinned GitHub release) when asked. It never downloads silently and never touches the game's own files.
- Read on this machine: the release install reports `BepInEx layer already installed (BepInEx 5.4.23.4)`; a scratch
  folder holding only `CasualtiesUnknown.exe` gets the full layer (18 core files, `winhttp.dll`,
  `doorstop_config.ini`) and the script prints the deploy command for it.

## The release artifact (2026-11-10)

`tools/package-release.ps1` builds the solution in Release, stages the plugin folder the deploy writes into a game
(the assemblies the game's Mono runtime needs, minus the BepInEx-owned ones, plus the native `steam_api64.dll`),
writes `install.txt` and zips the two under `artifacts/release/casualties-unknown-online-<sha>.zip`.

Read on this machine: `Packaged 34 file(s) + the install note`, `2868232 bytes, 35 archive entries`; the verifier
found `CasualtiesUnknownOnline/CasualtiesUnknownOnline.dll`, `CasualtiesUnknownOnline/steam_api64.dll` and
`install.txt` inside, and no BepInEx-owned assembly.

The note is the player's three steps: install BepInEx 5.4.23.4 x64 into the game folder, copy the plugin folder into
`BepInEx/plugins`, create `steam_appid.txt` with `4576490` beside the exe. It names the native DLL's reason (Steamworks
loads it from disk).

The package also carries the bundled mod plugins when their Release output exists: `RecursiveCrafting`,
`PinyinSearch` and `Music` ship their plugin assembly plus the `.Core` assembly they load from disk; a DLL that
duplicates one the core set already ships is skipped by name (one copy of an assembly per folder).

Read on this machine (installed from the staged folder, release client): the package holds 39 files + the note
(3,043,938 bytes, 40 entries); the log discovered and loaded all three mods —
`[Mods] discovered cuo.music 0.1.0 (ClientOnly, permissions SendNetworkMessage, RegisterLocalCommand …)`,
`cuo.pinyinsearch 0.1.0 (ClientOnly, permissions None, binds PlayerCamera.RefreshRecipeList)`,
`cuo.recursivecrafting 0.1.0 (ClientOnly, permissions RegisterLocalCommand, binds Recipe.fullName + Recipe.description getters)` —
and each answered `loaded (bind + initialize + start in the discovery frame)`. The adapter line in the same log read
`Game Adapter patches installed and verified (282 targets)`; the only exception is the known third-party
HotRepl/NJsonSchema `TypeLoadException`, not the mod's.

The install's `[Adapter] DisabledCapabilities` was still `diagnostics` from the stage-3 readings; it was restored to
empty (the production default) after the reading.

## The bundled mods on the release build (2026-11-10)

The package's mods were read on the release client (single host, a fresh run):

- `RecursiveCrafting`: `/craftchain flimsyknife` answered `[Recursive] flimsyknife: cannot be crafted — nothing here provides "scrapmetal".` —
  the console command, the recipe lookup and the chain walk all run on 1.0.
- `PinyinSearch`: the crafting panel opened (`crafting-panel mode=open` → `detailName "<sprite index=25>rope"`), the literal query
  `flimsy` narrowed the list to `rowCount 1`, and the pinyin query `cuiruo` narrowed it to `0`. The zero is correct for this client:
  it runs the game in English, so the recipe names are English and the pinyin matcher has no Chinese name to match. The pinyin path
  needs a Chinese-language client; that is a named limit of this reading, not a defect.

The literal half and the console half are machine-read on the release build. The pinyin mapping itself is Core logic with unit coverage (`PinyinSearch.Core` tests in the suite), and the live half that matters on 1.0 is the hook: the mod registry reported `binds PlayerCamera.RefreshRecipeList`, and the filter ran (the row count changed with the query). A Chinese-language live client would read the same hook with Chinese names; the game's language on this install is English and has no settings entry, so that variant is a named fixture gap, not an unread product row.
