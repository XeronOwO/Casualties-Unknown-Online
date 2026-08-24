# Gun muzzle-flash particle replay — self-check (2026-08-23)

The animation/presentation audit noted that `GunScript.Fire` plays the
`muzzleParticle` one-shot (GunScript.cs:191) only on the owner's local gun;
remote render clones received the shot sound and recoil via
`CharacterSoundKind.GunFire`/`CharacterSoundMsg.RecoilDegrees`, but never saw
the muzzle flash. This closes that presentation gap without a wire change.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Source-side particle | `GunScript.Fire` calls `this.muzzleParticle.Play()` on every shot (GunScript.cs:191). |
| Existing shot event | `GunFirePatch` already reports the shot as `CharacterSoundKind.GunFire` on `CharacterSoundMsg` (GunFirePatch.cs:34-47); `CharacterSoundSync` replays the sound and recoil on the owner's clone. |
| Render clone never fires | `RemoteBodyDriver` guard in `GunFirePatch`/`GunStateSync` suppresses clone-side reporting; a clone is a display proxy and never runs the native `Fire` path. |
| Clone gun presentation | `CloneInventoryRenderer` materialises the owner's carried item prefabs into clone slots from the 1 Hz character snapshot; gun prefabs carry the same `ParticleSystem muzzleParticle` component. |

## 2. Change

- **New pure replay helper** — `MuzzleFlashReplay` (Game
  Adapter/Character) finds the clone gun whose world position is nearest to the
  reported fire position and calls `muzzleParticle.Play()` on it. It is
  display-only: no `GunScript` simulation, no state, no physics.
- **Hook into the existing event** — `CharacterSoundSync.OnReceived` calls
  `MuzzleFlashReplay.TryPlay` for every `CharacterSoundKind.GunFire` message
  after the existing sound/recoil replay, and logs whether a clone gun was
  found. The receiver-side `RemoteApply` scope already prevents the replay from
  being captured as a new local event.
- **No protocol change** — the fire event already exists and carries the fire
  position; no new NetMsg, no new protobuf field, `ProtocolVersion` stays 43.
- **Degradation** — if the 1 Hz inventory snapshot has not yet rendered the
  clone's gun (e.g. world-entry edge), the particle is skipped with a Debug
  log; the sound/recoil still play. A lost one-shot particle has no persistent
  state to heal.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1333 passed |
| `MuzzleFlashReplayTests` | 1 passed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | no event mechanism touched |
| Deploy | `tools/deploy.ps1` to the real game directory succeeded |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `MuzzleFlashReplayTests.TryPlay_TakesBodyAndFirePosition_AndReturnsBool`
  locks the replay helper's public/internal signature.
- `CharacterSoundSync` already has dedicated `CharacterSoundSyncTests` for the
  GunFire wire/relay path; the particle is a receiver-side presentation action
  on Unity `ParticleSystem`, verified by static evidence + the existing event
  path rather than a runtime Unity harness in this developer cycle.

## 5. Structure review

- `MuzzleFlashReplay` is a small single-concern static helper; no new state,
  no new wire class, no cross-call business state.
- `CharacterSoundSync` remains its existing one-concern event replay domain and
  stays under the 600-line gate.
- No dead mechanism left behind; the source `GunScript.Fire` path is unchanged.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.
