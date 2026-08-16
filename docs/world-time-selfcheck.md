# World Time Flow — Self-Check (2026-08-16)

Delivery fact sheet for the multiplayer world-time domain (backlog: wait /
fast-forward / sleep-acceleration). ProtocolVersion 13, NetMsg 90/91.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Speed hotkeys | `PlayerCamera.HandleInput` speed1/2/3 call `SetTimeScale(Normal/Fast/SuperFast)`; movement keys reset Normal (PlayerCamera.cs:887-895, 923) | host hotkeys stay local-first and broadcast; guest hotkeys/movement resets become `WorldTimeRequest` reports to the host (never a local timeScale write) | PlayerCameraSetTimeScalePatch; WorldTimeSync |
| 2 | Unconscious/dying fast-forward | `HandleUnconsciousScreen` calls `SetTimeScale(UnconsciousFast 25× / DyingFast 3.5× / Normal)` when the local black screen is up (PlayerCamera.cs:2235-2244) | all those calls are suppressed in a session (`CallContext.Origin.WorldTimeSleepLocal`); the host's policy applies 25×/3.5× only when EVERY in-world player is unconscious and no one is moving | PlayerCameraHandleUnconsciousScreenPatch; WorldTimePolicy; WorldTimeSync |
| 3 | Direct timeScale writes | `WorldGeneration.Update` resets `Time.timeScale = 1f` on quake start (:870); ConsoleScript can write arbitrary values | host pump maps actual timeScale back into the domain and broadcasts the correction; guest pump enforces the last host speed when a direct writer moved it to another domain speed | WorldTimeSync.Update |
| 4 | Guest fast-forward | each side could previously run its own 5×/20×, diverging world timers | guest local SetTimeScale for Normal/Fast/SuperFast is swallowed and sent as a request; Slowmo/Paused stay local-only (existing accepted presentation semantics) | PlayerCameraSetTimeScalePatch |
| 5 | Host authority | Time.timeScale is process-global Unity state; the host owns the shared world | host accepts guest requests only while nobody is moving; movement or sleep overrides and clears the request; sleep acceleration never re-applies a stale request | WorldTimePolicy (pure) |
| 6 | Late joiner / reconnect | a joiner starts at the game's default 1× regardless of the host's current speed | host re-broadcasts current speed on `RemoteSceneChanged(inWorld=true)` and every 5 s (idempotent) | WorldTimeSync |
| 7 | Local-only time effects | SurvivorNote/EPda slowmo, PauseHandler pause, forced menu/death resets | unchanged on both sides for Slowmo/Paused/force calls — recorded as local-only; the 5 s host re-broadcast self-heals the next domain-speed write | PlayerCameraSetTimeScalePatch |

## Design

- `WorldTimeSpeed` enum + `WorldTimeRequestMsg` (guest→host) + `WorldTimeMsg`
  (host→guest): Normal/Fast/SuperFast/UnconsciousFast/DyingFast only. Slowmo
  and Paused are deliberately NOT on the wire — they stay local presentation.
- `WorldTimePolicy` (pure): any moving player or unknown player state forces
  Normal and clears the request; if every in-world ALIVE player has
  `consciousness <= 20`, the session runs DyingFast (any `brainDying`) or
  UnconsciousFast; otherwise the requested speed stands.
- `WorldTimeChannel` / `IWorldTimeControl` (Runtime): star-shaped time
  plumbing — guest reports the request, host broadcasts the authoritative
  speed.
- `WorldTimeSync` (GameAdapter deep module): owns the requested speed, the
  applied speed, the per-member state capture (local Body health + 20 Hz
  velocity buffers + the host's 1 Hz CharacterData store for guest
  consciousness/blood pressure), the host policy pump, the 5 s resend and the
  world-entry fan-out.
- Harmony adapters are thin:
  - `PlayerCameraSetTimeScalePatch` — prefix routes every SetTimeScale
    through the bridge unless it is a CUO apply or a suppressed sleep-local
    call; postfix reports the host's applied change.
  - `PlayerCameraHandleUnconsciousScreenPatch` — opens/closes the
    `WorldTimeSleepLocal` CallContext scope so the vanilla auto-fast-forward
    never writes timeScale in a session.

## Verification design

1. L0 (pure): `WorldTimePolicyTests` locks movement-override, request-adopt,
   request-clear, all-sleep 25×, dying 3.5×, awake-blocks-sleep, dead-player
   ignoring, unknown-state safety.
2. L0 (wire): direction-table rows for NetMsg 90/91; protobuf zero-omission
   round-trip for `WorldTimeSpeed.Normal`; a real host+guest wire test for
   request-up and broadcast-down.
3. Static: `PatchContractTests` resolves `PlayerCamera.SetTimeScale` and
   `PlayerCamera.HandleUnconsciousScreen` against the game assembly (the new
   patch classes are counted by `PatchInventory`).
4. Runtime (later, final acceptance only): host F8 + guest join; host presses
   speed2 → both HUDs show ×5; guest presses speed3 → host adopts and both
   show ×20; either player moves → both return ×1; both players unconscious →
   both run ×25 and return to ×1 when one wakes. Logs: `[WorldTime]` lines on
   both sides.
5. Assertion-validity proof: deleting the policy call or the host broadcast
   turns the new tests red; restored to green.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Speed hotkeys | host broadcast / guest request | PlayerCameraSetTimeScalePatch; WorldTimeSync |
| Sleep auto-fast-forward | suppressed locally, host all-sleep policy | PlayerCameraHandleUnconsciousScreenPatch; WorldTimePolicy |
| Direct timeScale writes | host adopt+broadcast / guest enforce | WorldTimeSync.Update |
| Guest request | validated Normal/Fast/SuperFast only | WorldTimePolicy.IsGuestRequestSpeed |
| Movement gate | any moving or unknown state → Normal | WorldTimePolicyTests |
| Sleep speed | 25× all unconscious, 3.5× any brain-dying | WorldTimePolicyTests |
| Request lifecycle | cleared on movement/sleep, not restored | WorldTimeDecision.NextRequested |
| Late joiner | RemoteSceneChanged + 5 s resend | WorldTimeSync |
| Local-only Slowmo/Paused | not on the wire, force calls allowed | PlayerCameraSetTimeScalePatch |
| Direction | NetMsg 90 g2h / 91 h2g | PacketReceiver; DirectionTests |
| Wire | Normal zero round-trip | NetPacketTests |
| Version gate | ProtocolVersion 13 | ProtocolVersion.cs |
| Structure | touched classes stay under the 600-line gate | tools/check-architecture.ps1 |
