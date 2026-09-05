# Sleep behavior policy decision

- Status: Resolved
- Category: Gameplay / sleeping policy
- Source: User backlog (2026-09-05)

## Decision

- **Normal player-initiated sleeping is allowed in shared sessions.** The
  game's own sleep/nap mechanics remain available; CUO does not add a gate that
  would fight native behavior or block single-player-style gameplay.
- **Forced sleep (mushroom tail / other mandatory effects) is allowed and
  treated as ordinary sleep.** It is not a voluntary world-time request; remote
  presentation already rides the existing sleeping/nap path (standard variant
  when no nap tracker is present).
- **World-time acceleration remains the existing cooperative host-authoritative
  policy:** the shared clock accelerates only when every in-world alive player
  is unconscious (`WorldTimePolicy.DecideSleepSpeed`); any awake player blocks
  acceleration. Manual Fast/SuperFast requests are cooperative and do not move
  the clock while anyone is awake. This is not a local time-scale hack.
- **Remote presentation remains on the existing player stream:**
  `PlayerEntity.Sleeping` / `NapVariant` at 20 Hz plus `CharacterHealthMsg`
  face vitals at 1 Hz are sufficient; no new wire field or protocol change is
  needed.

## Evidence

- `WorldTimePolicy.cs` / `WorldTimePolicyTests` — all-unconscious sleep
  acceleration and awake-block rules.
- `WorldTimeSync.cs` — host-authoritative application; guest suppression of
  local sleep fast-forward.
- `NapAndDogShakeSyncSelfcheck` — sleeping/nap remote presentation.
- `CloneFacePresentation` / `RemoteBodyFactory` — remote face/vitals
  presentation is unaffected by allowing sleep.
- No code/protocol change is required by this decision; current behavior is the
  chosen policy.

If a future host rule is wanted to disable/limit voluntary sleep while still
allowing forced sleep, it would be a separate host-rules feature and would need
its own native-UI/mechanism audit.
