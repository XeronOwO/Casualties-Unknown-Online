# references/

Compile-time references for the Game Adapter layer.

The game assemblies are **game-owned and copyrighted** — they are never
committed to the repository. Copy them locally **on demand** (only the ones
the current code actually references) before building; the csproj references
them via relative `HintPath`s with `<Private>False</Private>` (compile-time
only, never copied to output).

> **On-demand policy**: do not copy the whole set upfront. Add a DLL here
> only when code starts referencing types from it, then add the matching
> `<Reference>` to the csproj.

## How to populate

From the game's root folder (replace the path with yours):

```powershell
$game = "C:\path\to\game"
$managed = "$game\CasualtiesUnknown_Data\Managed"
$bepinex = "$game\BepInEx\core"

Copy-Item "$managed\Assembly-CSharp.dll"        .   # game main assembly (private types)
Copy-Item "$managed\UnityEngine.dll"            .   # game's bundled UnityEngine
Copy-Item "$managed\UnityEngine.CoreModule.dll" .   # Unity core module (same version as game)
Copy-Item "$managed\UnityEngine.Physics2DModule.dll" .   # Physics2D/Rigidbody2D (Game Adapter)
Copy-Item "$managed\UnityEngine.AnimationModule.dll"  .   # HingeJoint/anim helpers (Game Adapter)
Copy-Item "$managed\netstandard.dll"            .   # Unity Mono compatibility layer
Copy-Item "$bepinex\0Harmony.dll"               .   # HarmonyX fork 2.9.0 (runtime copy lives in BepInEx/core)
Copy-Item "$bepinex\plugins\KrokMP\steam_api64.dll" .  # Steam native lib (deployed via deploy.ps1)
```

> **Why reference 0Harmony directly instead of the `Lib.Harmony` NuGet package**:
> the game's BepInEx/core ships 0Harmony.dll 2.9.0 (the BepInEx fork of
> HarmonyX), while nuget.org's `Lib.Harmony` stops at 2.4.2. Referencing the
> game's own copy keeps compile-time and runtime versions identical — the same
> convention as Steamworks.NET. Never deploy 0Harmony.dll (BepInEx/core owns
> it; deploy.ps1 excludes it).

## Origin table

| File | Source |
|---|---|
| `Assembly-CSharp.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `UnityEngine.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `UnityEngine.CoreModule.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `UnityEngine.Physics2DModule.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `UnityEngine.AnimationModule.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `netstandard.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `0Harmony.dll` | `<game>\BepInEx\core\`(HarmonyX fork 2.9.0 — not on NuGet as 0Harmony) |
| `steam_api64.dll` | `<game>\BepInEx\plugins\KrokMP\`(native,not a compile reference — deploy.ps1 ships it) |

Keep the versions in sync with the game build you are developing against
(see `CLAUDE.local.md` for this machine's game path).
