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
Copy-Item "$managed\UnityEngine.TextRenderingModule.dll" .  # TextAnchor (Plugin wait-overlay GUI)
Copy-Item "$managed\UnityEngine.UIModule.dll" .   # Canvas/RenderMode (GameAdapter modal input blocker)
Copy-Item "$managed\Unity.TextMeshPro.dll"      .   # the game's TMP text (GameAdapter: the Online UI's native side)
Copy-Item "$managed\netstandard.dll"            .   # Unity Mono compatibility layer
Copy-Item "$bepinex\0Harmony.dll"               .   # HarmonyX fork 2.9.0 (runtime copy lives in BepInEx/core)
Copy-Item "$bepinex\plugins\KrokMP\steam_api64.dll" .  # Steam native lib (deployed via deploy.ps1)
```

> **Clear the download mark on what you place here.** A DLL that arrived through a
> browser, an archive or another machine carries a `Zone.Identifier` alternate data
> stream (the file properties' "Unblock" removes it). .NET Framework refuses to load
> such a file — `0x80131515`, "attempting to load an assembly from a network
> location" — and every copy preserves the stream, so it rides into the build output
> and stops the `net48` test host from loading its adapter, which then runs nothing
> while still exiting `0` ([build and test](../docs/en/contributing/build-and-test.md)).
> Clear it once, here: `Get-ChildItem . -File | Unblock-File`.

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
| `UnityEngine.TextRenderingModule.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `UnityEngine.UIModule.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `Unity.TextMeshPro.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `netstandard.dll` | `<game>\CasualtiesUnknown_Data\Managed\` |
| `0Harmony.dll` | `<game>\BepInEx\core\`(HarmonyX fork 2.9.0 — not on NuGet as 0Harmony) |
| `steam_api64.dll` | `<game>\BepInEx\plugins\KrokMP\`(native,not a compile reference — deploy.ps1 ships it) |

`Newtonsoft.Json.dll` is deliberately absent from both lists above: the shape the
game ships is delay-signed, so a .NET Framework host that verifies it refuses to
load it (`0x80131045`, "strong name signature could not be verified"), and the
`net48` test project takes the signed `Newtonsoft.Json` NuGet package instead. The
assembly identity is the same one (`13.0.0.0`, `30ad4fe6b2a6aeed`), so nothing
binds differently.

Keep the versions in sync with the game build you are developing against
(see `AGENTS.local.md` for this machine's game path).

## After a game update

The full update-day flow — snapshot the previous build, snapshot the new build,
classify the differences, then contract tests and offline replay — is
`docs/development/game-update-runbook.md`. In brief:

1. Re-copy the updated DLLs (the list above) into `references/`.
2. Snapshot the previous and the new build and diff them
   (`tools/CasualtiesUnknownOnline.ContractTool`): the classified report names
   every broken hook, every field/enum move, and the targets that are unchanged
   but still need a semantic look.
3. Run `dotnet test` — the Phase-3 **patch-contract tests** reflect these
   assemblies and assert every Harmony hook's target (type/method/argument
   types/patch parameter names) still resolves. Broken contracts are named one
   by one — that list is exactly the adapter work a game update requires
   (rename/retarget each broken hook, or drop it deliberately and delete its
   contract).
4. The runtime repeats the same check at launch (`PatchInventory.VerifyMissing`)
   — a game update that slipped past the tests fails loud at startup instead of
   silently running unpatched (a silently missing hook is how sync bugs hide).
5. If the update added/removed Unity modules the adapter references, add/remove
   the matching `<Reference>`/`<None>` entries in the GameAdapter and Tests
   csproj files.
