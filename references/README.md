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
$game = "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"
$managed = "$game\CasualtiesUnknown_Data\Managed"
$bepinex = "$game\BepInEx\core"

Copy-Item "$managed\Assembly-CSharp.dll"        .   # game main assembly (private types)
Copy-Item "$managed\UnityEngine.dll"            .   # game's bundled UnityEngine
Copy-Item "$managed\UnityEngine.CoreModule.dll" .   # Unity core module (same version as game)
Copy-Item "$managed\netstandard.dll"            .   # Unity Mono compatibility layer
Copy-Item "$bepinex\0Harmony.dll"               .   # Harmony (runtime copy lives in BepInEx/core)
Copy-Item "$bepinex\plugins\KrokMP\steam_api64.dll" .  # Steam native lib (deployed via deploy.ps1)
```

## Origin table

| File | Source |
|---|---|
| `Assembly-CSharp.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `UnityEngine.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `UnityEngine.CoreModule.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `netstandard.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `0Harmony.dll` | `<game>\BepInEx\core\` |
| `steam_api64.dll` | `<game>\BepInEx\plugins\KrokMP\`(native,not a compile reference — deploy.ps1 ships it) |

Keep the versions in sync with the game build you are developing against
(see `CLAUDE.local.md` for this machine's game path).
