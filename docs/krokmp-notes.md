# KrokMP Reverse-Engineering Notes

> **Historical reference only.** KrokMP is studied as a source of design context;
> CUO does not copy its implementation. Current CUO architecture is in
> [`architecture-evolution/`](architecture-evolution/).

> Findings from decompiling `KrokoshaCasualtiesMP.dll` (game dir:
> `BepInEx/plugins/KrokMP/`). API signatures, architecture observations, and
> compatibility-layer feasibility. Raw decompile output lives in
> `reversing/KrokMP/` (gitignored) — this file is the distilled knowledge.

## Deployment layout

KrokMP ships as a folder `BepInEx/plugins/KrokMP/`:

| File | Role |
|---|---|
| `KrokoshaCasualtiesMP.dll` | main plugin assembly (also contains the public API namespace) |
| `Steamworks.NET.dll` | Steam identity + lobby |
| `LiteNetLib.dll` | **raw UDP transport** — NOT Steam P2P networking |
| `OpusSharp.Core.dll` + `opus.dll` | voice chat (Opus) |
| `steam_api64.dll` | native Steamworks |
| `Multiupdater.dll` | self-updater |

Additionally `BepInEx/plugins/CUCoreLib.dll` is a shared core library used by
several community mods (QoL.Unknown, CasualtiesCraft, …).

**Observation**: KrokMP = Steamworks.NET for lobby/identity + LiteNetLib (plain
UDP) for game data. This is exactly the "Steam P2P treated as LAN UDP" pitfall
in `architecture.md` §10.1 — NAT/firewall traversal issues, connection quality,
and host-load problems plausibly trace back to it. CUO must NOT copy this
stack; MVP uses `ISteamNetworkingMessages`.

## Public API surface (what mods hook into)

The official API namespace `KrokoshaCasualtiesMultiplayerAPI` contains exactly
3 types — a tiny, well-shaped extension point:

- `GamemodeBase` (abstract, `MonoBehaviour`): `virtual void Init(string[] args)`,
  `protected virtual Update()` auto-destroys the gamemode on clients.
- `GamemodeManager` (static): `SetGamemode<T>()` / `SetGamemode(Type)` (server
  only, spawns `DontDestroyOnLoad` GameObject), `GetGamemode()`, `HasGamemode()`,
  `DeleteGamemode()`, `GetAllAvailableGamemodes()` (assembly scan for
  `GamemodeBase` subclasses).
- `BattleRoyale` (built-in example gamemode).

The example gamemode also leans on these main-namespace statics (the de-facto
modding API):

- `KrokoshaScavMultiplayer`: `IsNetworkActiveAndIsServer()`,
  `IsNetworkActiveAndIsClient()`, `is_server`, `network_system_is_running`,
  `rules` (game-rule struct: PVP, AutoContinue, LateJoinAllowed,
  SavePlayerInventory, …), `ApplyGameRules()`.
- `NetPlayer`: `AllLivingPlayers`, `BodyToPlayerDict`, static events
  `OnPlayerJoined` / `OnPlayerLeft` / `OnPlayerDeath`, `playername`, `pos`,
  `Server_DoAlertSingle(...)`.
- `NetBody`: `bodyname`, death events.
- `Chat.Server_ChatAnnouncement(in string)`, `ServerMain.Server_AnnounceAlert(...)`
  (server broadcast), `Util.IsWorldGenerated()` / `IsInWorld()`, `KM` math
  helpers, `Con.con.ParseFloat`.
- Game-private types are used heavily (WorldGeneration, WorldgenPatches,
  PlayerCamera, …) — KrokMP mods compile against the game assembly directly.

## Architecture of the internals (for contrast, NOT to copy)

Main namespace is dominated by one Harmony patch file per game mechanic
(`*_MultiplayerPatch.cs`), plus sync infrastructure: `NetPlayer`/`NetBody`
state classes, `NetId`/`AnyObjectNetId`, `SyncInfo`, `AlwaysSyncAttribute`,
`BaseCoolSyncSubSystem`, per-feature packet classes (`AmmoSyncPacket`, …), and
a `rules` struct applied by `ApplyGameRules()`. Voice via Opus.

## Compatibility-layer feasibility

For the reserved KrokMP compat adapter (`architecture.md` §5.4):

- **Realistic**: the official extension surface is tiny (3 API types + a
  handful of statics). API-level mapping is feasible: `GamemodeBase` /
  `GamemodeManager` semantics map cleanly onto a host-side gamemode concept.
- **Boundaries**: mods that reach into KrokMP internals (NetPlayer internals,
  sync-system details, per-feature patch internals) are NOT compat targets.
- **Reality check**: many community mods may bypass the API entirely and
  Harmony-patch KrokMP's implementation classes — those need evaluation per
  mod during Phase 4+ work.

## Game-internal types observed (first hints for the Game Adapter)

- `PlayerCamera` (player UI/camera controller; has `recipeFilter`,
  `RefreshRecipeList()` — see JustUnknownCharacters mod)
- `WorldGeneration` (`world` static, `width/height/chunkWidth`, `WorldToBlockPos`)
- `WorldgenPatches` (worldgen events), `RadiationLine`, `GunScript`,
  `AmmoScript`, `UITooltip`
- Scenes: `SampleScene` is the gameplay scene
- Unity version claim: **unverified** — the notes previously said “Unity 5.6-era”,
  but the decompiled modules reference Unity 2022-era module assemblies
  (`UnityEngine.CoreModule`, `UnityEngine.Physics2DModule`, etc.). Do not rely
  on this as a build-compatibility fact without re-verification.
