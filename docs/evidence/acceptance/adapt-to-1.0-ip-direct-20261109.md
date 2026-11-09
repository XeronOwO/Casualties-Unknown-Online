# 1.0 over IP direct — two clients in one world

Date: 2026-11-09. Artifact: `0.1.0+74c6bfd5` deployed to the 1.0 install, verified
("Deployment matches this tree's build output").

## What this reads

The mod runs on the game's 1.0 release (`Assembly-CSharp.dll` 2026-11-09 22:01:11, 1,030,656 bytes; Unity 2022.3.62,
app id 4576490), and two 1.0 clients on one machine met in one world over the IP-direct transport — the path the user
sanctioned while the second and third Steam accounts do not own the game.

## The fixture

- Host: the release install, launched through Steam, on the acceptance driver's host port. The plugin's capability
  report read `Game Adapter capability report … verdict: session available — no blocking hook failure`.
- Guest: a byte copy of the install in a sibling folder, its own acceptance driver port and its own CUO config carrying
  `[IpDirect] DisplayName = Guest`, `JoinAddress = 127.0.0.1`. Launched directly (`CasualtiesUnknown.exe`); its driver
  answered `steamInit: true`.
- The host's IP host was created from the Online UI: transport switched to `IpDirect` (`home.transport.ip`), display name
  `Host`, a listen port, then `ip.create_host`.
- The guest joined the host's address and port, then `ip.join`.

## The readings

| client | transport | role | inWorld |
|---|---|---|---|
| host (18590) | `IpDirect` | `Host` | `true` |
| guest (18591) | `IpDirect` | `Guest` | `true` |

The host ran `start-run`; both clients reached `inWorld: true, gateWaiting: false` — one world, two players, over the
IP-direct transport, on 1.0.

## The two runtime differences this batch also settles

- `WorldGeneration.SetBlock` is overloaded in 1.0; the untyped patch target made the whole adapter install refuse and
  the patch is pinned to `(Vector2Int, ushort)` (`WorldGenerationSetBlockPatch.cs`).
- `PlayerCamera.following` no longer exists; 1.0 computes the camera from `body.transform.position`, so the spectator
  camera threw `MissingFieldException` every frame. The camera is now set by a prefix on
  `PlayerCamera.HandleCameraPosition` (`PlayerCameraHandleCameraPositionPatch.cs`), and the capability report is green.

## Limits

- Both clients are on one machine over loopback; the Steam lobby path and a real network hop are not read here.
- The guest instance is a local test copy, not a shipping arrangement; its HotRepl port and display name differ only in
  that copy's own config.
- The second Steam account is not used: the guest runs on the host's Steam identity, so the reading does not cover
  two-account lobby sessions.
