# Auto turret trap fires unexpectedly after reload ("走火")

- Status: Todo
- Priority: Medium
- Category: Trap/entity sync / turret presentation
- Source: User report (2026-09-04) — the automatic turret trap sometimes fires a shot unexpectedly after it has finished loading/reloading. The user also asks whether this could be related to the dog-food container ghost-drop issue. Record only; no code action taken yet.

## Goal

Eliminate turret "走火" (stray/phantom firing) so a turret only fires when it should, according to the trigger-side authoritative event, and never fires an extra shot after a reload.

## Current turret sync design

- Turret firing is replayed as a trap entity event (`TurretFired`):
  - `docs/features/entities.md:125` — `TurretScript` is classified as a repeatable detect trap: "tracers + gunshot; self-destruct = explosion family; covered | TurretFired/TurretSelfDestructed".
  - `src/CasualtiesUnknownOnline.Runtime/Session/World/TurretReplayTimeline.cs` — the replay timeline: warning at t=0, shot at `ShotDelaySeconds` (0.5 s), and the native 15 s reload.
  - `src/CasualtiesUnknownOnline.GameAdapter/World/TrapStateActions.cs:360-414` — `ApplyTurretFired` writes the post-fire state (`didShoot = true`, `timeSinceFired = 3s`), plays the warning, starts `TurretLightSpriteGate`, and delays the shot visuals by 0.5 s.
- The `didShoot = true` immediate lock is deliberately used to stop the native `TurretScript.Update` from firing a real shot on a replayed/peer copy:
  - `docs/evidence/selfchecks/presentation/turret-light-sprite-selfcheck.md:26-31,57` — "didShoot is still set true at the warning — the game can never fire a real shot on a replay side."
- `TurretLightSpriteGate` only fixes the early light-sprite flicker; it does not change firing logic.

## Reported symptom

- After the turret has completed loading/reloading, it inexplicably fires a shot.
- This could be a replay-side phantom shot, a native `TurretScript.Update` shot on a peer despite the intended lock, a timing mismatch after the reload window, or a duplicate/extra event.

## Investigation needed

1. Capture runtime event/log evidence:
   - Is the stray shot associated with a `TurretFired` entity event, or is it a native `TurretScript.Update` shot not represented by any event?
   - Which side (host, guest, replay copy) fires it, and at what timestamps relative to the warning, shot delay and 15 s reload?
2. Verify the post-fire state actually survives the full reload:
   - `didShoot`, `timeSinceFired`, `didBeep`, and the native reload fields on both trigger-side and replay-side turret copies.
   - whether any replayed/copy turret can enter the native fire branch after the reload window when a body is in range.
3. Determine if the stray shot is only visual or also damages/creates kernel facts:
   - does it play sound/tracer only, or does it also hit a player/entity?
4. Check the possible relationship with the dog-food ghost item issue:
   - both may share a stale/duplicated entity/item state or a projected replay running more than once;
   - but keep them as separate tickets unless evidence proves a shared root cause.

## Required design direction (for the implementation cycle)

- Keep the turret's authority/event replay consistent: only the trigger-side `TurretFired` event should drive shot presentation; replay-side native update paths must not independently fire.
- If the 15 s reload state can expire and let a peer copy fire, decide the correct co-op semantics and lock it accordingly.
- If a shot visual is delayed, ensure it is scheduled exactly once per confirmed event and cannot be repeated after reload.
- Add runtime/event logs or tests to prove no stray shot after reload.

## Acceptance criteria (for the later implementation cycle)

- The turret does not fire unexpectedly after a completed reload.
- Every visible shot corresponds to an actual turret engagement event, not a stale/replayed/native side effect.
- Host and guest views are consistent; no extra sound/tracer/damage without an authoritative trigger.
- Existing turret/trap tests and repo gates remain green.

## Non-goals

- Not changing turret behavior outside CUO sync unless required.
- Not merging with the dog-food ticket until evidence shows a shared root cause.
- Not implementing in this cycle — this ticket is a backlog record only.
