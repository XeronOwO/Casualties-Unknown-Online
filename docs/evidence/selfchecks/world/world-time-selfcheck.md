# World Time Flow — Self-Check (revised 2026-09-19)

Delivery fact sheet for the multiplayer world-time domain (backlog:
`review/world-time-local-initiation.md`; the original sheet landed 2026-08-16 with
the host-authoritative model of ProtocolVersion 13). ProtocolVersion 28, NetMsg
90/91.

Model: `Time.timeScale` is process-global Unity state, so exactly one side owns
it — the host. A manual speed change is **local first**: the initiator's own
client applies it at once and reports the intent, the host arbitrates
accept-first, and the answer settles the initiator (user ruling 2026-09-18,
decision 184).

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Speed hotkeys | `PlayerCamera.HandleInput` speed1/2/3 call `SetTimeScale(Normal/Fast/SuperFast)` (PlayerCamera.cs:885-896) | host: local-first (it is the authority) and the postfix adopts the speed as the request; guest: the call WRITES ITS OWN CLOCK at once and is reported as `WorldTimeRequest`; the host's answer settles the pending intent | PlayerCameraSetTimeScalePatch; WorldTimeSync; WorldTimeLocalInitiation |
| 2 | Movement-key reset | the mover's own `Input.GetKeyDown(right/left)` calls `SetTimeScale(Normal, switchSound:false)` (PlayerCamera.cs:921-924); up/down never did | unchanged and now the ONLY movement rule: the host-side velocity veto (`WorldTimePolicy.IsMoving`) is deleted, so a body that was pushed, carried or drifting changes nothing, and a walking teammate no longer cancels an accelerated session | PlayerCameraSetTimeScalePatch; WorldTimePolicy |
| 3 | Unconscious/dying fast-forward | `HandleUnconsciousScreen` calls `SetTimeScale(UnconsciousFast 25× / DyingFast 3.5× / Normal)` when the local black screen is up (PlayerCamera.cs:2233-2245) | all those calls are suppressed in a session (`CallContext.Origin.WorldTimeSleepLocal`); the host's policy applies 25×/3.5× only when EVERY in-world player is unconscious and observable | PlayerCameraHandleUnconsciousScreenPatch; WorldTimePolicy; WorldTimeSync |
| 4 | Direct timeScale writes | `WorldGeneration.Update` resets `Time.timeScale = 1f` on quake start (:870); ConsoleScript can write arbitrary values | host pump maps the actual timeScale back into the domain (`WorldTimeSpeedScale.FromTimeScale`) and broadcasts the correction; guest pump enforces the last host speed when a direct writer moved it to another domain speed | WorldTimeSync.Update; WorldTimeSpeedScale |
| 5 | Host arbitration | Time.timeScale is process-global; the host owns the shared world | accept-first: a request is refused only when it is unrepresentable (not an in-world member, not a guest-requestable speed, the start gate owning the clock) — and a refusal is ANSWERED with the authoritative speed so the initiator returns to it; a teammate being awake is no longer grounds for refusal | WorldTimeSync.OnRequestReceived; WorldTimePolicy |
| 6 | Initiator reconciliation | — (new) | the pending intent is confirmed by an equal answer, adopted when idle, and otherwise ramps back to the host's value over `RampSeconds` of unscaled time; enforcement of the host's last value is suspended while an intent is in flight | WorldTimeLocalInitiation; WorldTimeLocalInitiationTests |
| 7 | Late joiner / reconnect | a joiner starts at the game's default 1× regardless of the host's current speed | host re-broadcasts the current speed on `RemoteSceneChanged(inWorld=true)` and every 5 s (idempotent) — an unchanged broadcast also settles a pending intent | WorldTimeSync |
| 8 | Local-only time effects | SurvivorNote/EPda slowmo, PauseHandler pause, forced menu/death resets | unchanged on both sides for Slowmo/Paused/force calls — recorded as local-only; the 5 s host re-broadcast self-heals the next domain-speed write | PlayerCameraSetTimeScalePatch |

## Design

- `WorldTimeSpeed` enum + `WorldTimeRequestMsg` (guest→host) + `WorldTimeMsg`
  (host→guest): Normal/Fast/SuperFast/UnconsciousFast/DyingFast only. Slowmo
  and Paused are deliberately NOT on the wire — they stay local presentation.
  `WorldTimeSpeedScale` owns the one speed ↔ `Time.timeScale` mapping
  (1 / 5 / 20 / 25 / 3.5).
- `WorldTimePolicy` (pure): the all-unconscious sleep branch owns the clock while
  it applies (its speed, request cleared) and refuses to accelerate over an
  unobserved player; otherwise the standing request stands — no movement input,
  no cooperation gate.
- `WorldTimeLocalInitiation` (pure): the initiator's state machine — pending
  intent, confirm on an equal answer, `Adopt` for an ordinary change, and a
  `Ramp` correction when the host's answer differs (unscaled time; the value at
  the end is the host's own, applied through the normal SetTimeScale path).
- `WorldTimeChannel` / `IWorldTimeControl` (Runtime): star-shaped time plumbing —
  guest reports a locally applied speed, host broadcasts the authoritative speed.
- `WorldTimeSync` (GameAdapter deep module): owns the requested/applied speed,
  the accept-first arbitration and the answer, the per-member sleep facts (local
  Body health + the host's 1 Hz CharacterData store for guests' consciousness and
  blood pressure + the remote body proxy as the observability requirement), the
  host policy pump, the 5 s resend and the world-entry fan-out. It never touches
  timeScale while the start gate holds (the gate owns the load freeze).
- Harmony adapters are thin:
  - `PlayerCameraSetTimeScalePatch` — prefix routes every SetTimeScale through
    the bridge unless it is a CUO apply or a suppressed sleep-local call; the
    guest's manual call is local-first and reported; the postfix reports the
    host's applied change.
  - `PlayerCameraHandleUnconsciousScreenPatch` — opens/closes the
    `WorldTimeSleepLocal` CallContext scope so the vanilla auto-fast-forward
    never writes timeScale in a session.

## Verification design

1. L0 (pure): `WorldTimePolicyTests` locks the honored manual request, the
   sleep gate (25× / 3.5× / awake-blocks / dead-ignored / unknown-blocks), and
   the unrepresentable-request normalization.
2. L0 (pure): `WorldTimeLocalInitiationTests` locks confirm / adopt / ramp, the
   ramp's interpolation and exact end value, the higher-value ramp (the sleep
   gate answering above the initiated speed), the two-press burst, and the
   session reset; `WorldTimeSpeedScaleTests` locks the shared mapping.
3. L0 (wire): direction-table rows for NetMsg 90/91; protobuf zero-omission
   round-trip for `WorldTimeSpeed.Normal`; a real host+guest wire test for
   report-up, broadcast-down and the request→answer round trip.
4. Static: `PatchContractTests` resolves `PlayerCamera.SetTimeScale` and
   `PlayerCamera.HandleUnconsciousScreen` against the game assembly.
5. Runtime (user release cycle only): host presses speed2 while a teammate is
   awake → the host runs ×5 at once and the guest follows; a guest presses
   speed2 → the guest runs ×5 at once and the host follows; a refused/overridden
   request ramps back within a frame or two; either player presses left/right →
   that player returns to ×1 and the session follows. Logs: `[WorldTime]` lines
   on both sides.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Speed hotkeys | local-first on both sides; guest reports and reconciles | PlayerCameraSetTimeScalePatch; WorldTimeSync; WorldTimeLocalInitiation |
| Sleep auto-fast-forward | suppressed locally, host all-sleep policy | PlayerCameraHandleUnconsciousScreenPatch; WorldTimePolicy |
| Direct timeScale writes | host adopt+broadcast / guest enforce | WorldTimeSync.Update |
| Guest request | validated Normal/Fast/SuperFast only, then accepted | WorldTimePolicy.IsGuestRequestSpeed |
| Movement | the mover's own native key rule only — no host veto | PlayerCameraSetTimeScalePatch; WorldTimePolicy (no velocity input) |
| Sleep speed | 25× all unconscious, 3.5× any brain-dying | WorldTimePolicyTests |
| Request lifecycle | sleep clears it; a manual request stands until the next change | WorldTimeDecision.NextRequested |
| Reconciliation | confirm / adopt / ramp, enforcement suspended while pending | WorldTimeLocalInitiationTests |
| Late joiner | RemoteSceneChanged + 5 s resend | WorldTimeSync |
| Local-only Slowmo/Paused | not on the wire, force calls allowed | PlayerCameraSetTimeScalePatch |
| Direction | NetMsg 90 g2h / 91 h2g | PacketReceiver; DirectionTests |
| Wire | Normal zero round-trip | NetPacketTests |
| Version gate | ProtocolVersion 28 | ProtocolVersion.cs |
| Structure | touched classes stay under the 600-line gate | SourceShapeGateTests |
