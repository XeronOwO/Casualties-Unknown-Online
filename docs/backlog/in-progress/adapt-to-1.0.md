# Adapt the mod to the game's 1.0 release

- Status: In progress — the 1.0 adapter is reconciled and read on a live session (2026-11-09): the capability report passes, two 1.0 clients met in one world over the IP-direct transport, and the two runtime differences are fixed. The system map is complete (ten domain pages + `principles.md`, `docs/design/game/`). What remains: the backlog's release pass.
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
IP-direct transport with two local instances. The reading:

- Host: the release install, launched through Steam; transport `IpDirect`, role `Host`, a run started.
- Guest: a byte copy of the install (`E:\cuo-instance-b`, own driver port 18591), launched directly; its driver answered
  `steamInit: true`; transport `IpDirect`, joined `127.0.0.1:7777`, role `Guest`.
- Both read `inWorld: true, gateWaiting: false` — one world, two players, on 1.0.

Evidence: `docs/evidence/acceptance/adapt-to-1.0-ip-direct-20261109.md`. The same batch closed the two runtime
differences (`SetBlock`'s overload pinned; the camera no longer reads the removed `following` field). What this does not
read: the Steam lobby path (needs a second owning account) and a real network hop (both instances are loopback).
