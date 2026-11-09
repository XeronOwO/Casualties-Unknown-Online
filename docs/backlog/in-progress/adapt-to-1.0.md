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
