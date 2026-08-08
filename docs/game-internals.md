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
- **World-defining fields verified (star-network Step 5)**: `WorldGeneration.totalTraveled` (public int, WorldGeneration.cs:4162), `biomeDepth` (public int, :4165), `biomeOverride` (public `OverrideSceneType` enum, :4237 — `{None, Tutorial, Debug}`, drives generation branches at :2631-2660 and dungeon/radiation logic at :861-866). All three are read/written via `HarmonyTraverse` (field-access pattern like `runSettings`). `WorldStartParams.LoadedRun` has **no backing field** on WorldGeneration (`PreRunScript.LoadRun`, PreRunScript.cs:294, is the save-flow entry — Phase 3 saves scope), so it stays false on the wire; guest generation otherwise matches via the restored `Random.state`.

## KrokMP's Approach (reference only, not to copy)

From `reversing/KrokMP/KrokoshaCasualtiesMP/` (full decompiled source):

- **Character prefab**: `ServerMain.CHARACTER_PREFAB` — a clone of the scene player GameObject (`Instantiate`, then `SetActive(false)`) used as a template. `NetBody._Internal_CreateNetBody` instantiates it per remote player, finds `Body` via `GetComponentInChildren<Body>`, attaches a `NetBody` component.
- **Player 2 creation**: server creates a cloned Body per guest at spawn (`CreateNewPlayerCharacter`, spawn location from `WorldPlacePlayer` flow); **clients also instantiate their own clone** on first body-sync packet and apply server state to it (`CharSync.Client_ReadData2`).
- **Sync model**: ~30 Hz state broadcast (`NetBodySyncPacket` — position, look, body state), client applies to the local clone. Server is authoritative-ish, but every peer runs full local simulation of every body (state-sync style, not input-driven).
- **Camera/UI routing**: `PlayerCamera.main.body` is overridden (`BodyGetterOverrider`, `InvButton_get_body` patches) so local camera/UI target the local player's clone.
- **Worldgen**: `Patched_GenerateWorld` / `Patched_WorldPlacePlayer` (via `HarmonyReversePatch`) replace the original coroutines; server drives world state, clients wait (`WorldChunkSync` tilemap sync etc.).

## Clone & Render Chain (Phase 1 verified findings)

Remote player clones are `Instantiate` of the scene `"Experiment"` GameObject (same template KrokMP uses). Per-clone component behavior, verified in the decompiled sources:

- `Body.FixedUpdate` — physics. Render proxies skip it.
- `Body.Update` → `HandleVisuals` (`Body.cs:3123+`) — drives limb poses from the Animator skeleton (`bodyAnimator`/`armsAnimator`) + local world queries (BoxCast for `grounded`); also `Random.Range` jitter (consumes RNG). **MUST run on proxies** or limb sprites stay uninitialized/invisible and no poses are driven.
- `Limb.Update` (`Limb.cs:498+`) — shader params (`_SkinDamage` etc.), heal timers, infection checks (consumes `Random`); does NOT move limbs. Safe to leave running.
- `IKHandle.Update` (`IKHandle.cs:40-57`) — lerps `targetPos` to `Camera.main.ScreenToWorldPoint(Input.mousePosition)` and draws `LineRenderer` toward it. Clones drew aim lines at the **local player's mouse** ("head looking at mouse" symptom) → disabled on clones.
- `HingeJoint2D` on cloned limbs — disabled on proxies (physics frozen).
- `Body.Awake` (`Body.cs:1048`) accesses `WorldGeneration.world.soundMixerGroup` — clones must be created after the world exists (they are: at `RemoteJoined`, which requires both sides InWorld).

Render proxy recipe: frozen physics (`FixedUpdate` skipped, all `Rigidbody2D.simulated=false`, `HingeJoint2D` disabled, `IKHandle` disabled) + live `Body.Update` (animations/poses) + root transform written every frame from the peer's state report (with first-snapshot interpolation guard — see PlayerEntity.StateReceivedMs).

## Sync Model (Phase 1 landed, user-mandated)

Each player simulates **only its own body** locally; peer state is exchanged at 20 Hz (`PlayerState` host→guest, `PlayerStateReport` guest→host; both carry position/look/velocity/pose flags). The remote player is a frozen render clone fed by the state stream. "Host-authoritative" does **not** mean the host computes the guest's movement — guest movement is always local (user mandate: "移动必定是在本地计算的,主机只做校验"); authority covers world-state ownership (world-gen seed, saves, later: interactions), not per-frame player simulation. Previous attempt (host shadow-simulating the guest's clone) was reverted (`882a43d`).
