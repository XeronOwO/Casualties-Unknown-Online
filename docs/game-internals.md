# Game Internals — Casualties Unknown (Demo)

Reverse-engineering findings on the game's structure, from dnSpy decompiles in `reversing/` (raw material, gitignored) and KrokMP's decompiled source (also in `reversing/`). This file distills what CUO's Game Adapter must know. Game build: Demo, Unity 2022.3, Assembly-CSharp.dll.

## Scenes & Flow

- `PreGen` — main menu / pre-run setup (`PreRunScript`, run settings, tutorial).
- `SampleScene` — the actual game world (loaded from `PreRunScript.cs:268` via `SceneManager.LoadScene("SampleScene")`).
- `WorldGeneration` (MonoBehaviour in SampleScene, static singleton `WorldGeneration.world`): procedural world gen (`GenerateWorld`/`RegenerateWorld` coroutines, `FinishWorldGeneration`), block/tilemap chunk system (`ChunkScript`, `ISimEntity`), radiation line, earthquakes, save/continue (`SaveAndExit`, `ContinueRun`), `WorldPlacePlayer` coroutine (places the player body).
- `PlayerCamera` (SampleScene prefab, static singleton `PlayerCamera.main`): the player controller. `Update()` is the per-frame main loop (input, world UI, camera follow, death/unconscious screens). `HandleInput()` maps keys → `body.moveDir` (WASD), mouse → `body.targetLookPos`, plus jump/attack/interact/throw/ragdoll.
- The player is a **scene prefab, not code-spawned** — one Body + PlayerCamera per scene instance.

## Player Entity: `Body`

`PlayerCamera.main.body` — a `Body` MonoBehaviour (Rigidbody2D + limb simulation, PZ-style):

- Movement is **input-driven physics**: `PlayerCamera.HandleInput()` sets `body.moveDir` (Vector2), `Body.FixedUpdate()` turns it into forces/velocity (`rb.AddForce`, max-speed clamping, wall slide, jump). Nothing reads moveDir except the physics step.
- Position: `body.transform.position` (2D world space). Heading: `body.targetLookPos` (mouse world pos) + `body.isRight` (facing) + head limb rotation.
- Key state fields: `rb` (Rigidbody2D), `moveDir`, `targetLookPos`, `isRight`, `standing`, `alive`, `conscious`, `crouching`, `sleeping`, `limbs[]` (0 = head), `handSlot`, `eatTime`.
- `PlayerCamera` follows the body (`transform.position = body.transform.position`), so camera follows whatever body is `PlayerCamera.main.body`.

## Input

- `KeyBinds.GetBind("up"|"down"|"left"|"right"|"jump"|"attack"|"pause"|...)` — configurable keycodes cached in a dictionary.
- `PlayerCamera.HandleInput()` is the single input collection point (skips while console open / UI panels).

## World Generation & Saves

- World gen is **not deterministic** (`Random.Range` throughout; block gen uses `lehmer64` PRNG internally). `runSettings` presets (`normal`) drive difficulty knobs.
- `SaveSystem` + `WorldSaveData` exist — a save file captures a run's world. `SaveSystem.loadedRun` / `ContinueRun` resume it.
- World time: `WorldGeneration.TotalRunTime()`, `PlayerLayerDepthMeters()`, radiation line, earthquake cycle — all host-authoritative material (Phase 3+).

## KrokMP's Approach (reference only, not to copy)

From `reversing/KrokMP/KrokoshaCasualtiesMP/` (full decompiled source):

- **Character prefab**: `ServerMain.CHARACTER_PREFAB` — a clone of the scene player GameObject (`Instantiate`, then `SetActive(false)`) used as a template. `NetBody._Internal_CreateNetBody` instantiates it per remote player, finds `Body` via `GetComponentInChildren<Body>`, attaches a `NetBody` component.
- **Player 2 creation**: server creates a cloned Body per guest at spawn (`CreateNewPlayerCharacter`, spawn location from `WorldPlacePlayer` flow); **clients also instantiate their own clone** on first body-sync packet and apply server state to it (`CharSync.Client_ReadData2`).
- **Sync model**: ~30 Hz state broadcast (`NetBodySyncPacket` — position, look, body state), client applies to the local clone. Server is authoritative-ish, but every peer runs full local simulation of every body (state-sync style, not input-driven).
- **Camera/UI routing**: `PlayerCamera.main.body` is overridden (`BodyGetterOverrider`, `InvButton_get_body` patches) so local camera/UI target the local player's clone.
- **Worldgen**: `Patched_GenerateWorld` / `Patched_WorldPlacePlayer` (via `HarmonyReversePatch`) replace the original coroutines; server drives world state, clients wait (`WorldChunkSync` tilemap sync etc.).

CUO deliberately diverges: host-authoritative **input-driven** sync per `docs/architecture.md` §3 — the host simulates guest clone Bodies from guest input; guest clones render host state. MVP excludes client prediction/interpolation.
