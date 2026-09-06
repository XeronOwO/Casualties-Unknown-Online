# Sync player pain vocalizations and B-key bark to remote players

- Status: In Progress
- Priority: Medium
- Category: Character audio / player presentation sync
- Source: User report (2026-09-04) — the host's pain scream/groan and the sound triggered by pressing B are not heard on the guest client. The reverse direction (guest → host) was not tested by the user and is covered by the same star-relay path during the fix.
- Review rejection (2026-09-06): the first review pass was rejected because the lockpick-failure pain sound is still not heard on the guest. When the host fails to pick a lock, `LockpingMinigame.Update` plays `gore2` at the body and raises limb pain / damages claw health (`LockpingMinigame.cs:144-156`); that path is outside the existing `PantSound` scopes, so the first delivery only covered PantSound pain/yawn/growl/bark, not this lockpick-failure pain source.

## Goal

Make the host's pain vocalizations and B-key bark audible to the guest (and verify the reverse direction). The fix fits the existing dedicated character-sound event path; no per-frame audio stream is introduced. The lockpick-failure `gore2` pain sound must also ride that same event path.

## Reproduction / acceptance matrix

Reproduction: open a lockable entity with a lockpick minigame, hold a wrong
angle until the lock gets stuck (`timeWasStuck > 0.5f`); the game plays
`Sound.Play("gore2", ...)` on the acting player's body. A remote player must
hear that one-shot pain sound.

| Scenario | Expected |
|---|---|
| Host fails lockpick → guest view | guest hears `gore2` |
| Guest fails lockpick → host view | host hears `gore2` (guest report → host apply) |
| Third guest view of another guest's failure | third guest hears it via host relay |
| Lockpick success `unlock` | not replayed as a Pain event |
| Remote replay | not captured/reported again (RemoteApply guard, no echo) |
| Solo / inactive session | no network report, no behavior change |

## Implementation

- Added `CharacterSoundKind.Pain` (7), `Bark` (8), `Growl` (9), `Yawn` (10) to the existing `CharacterSoundMsg` dedicated one-shot event family.
- Added `PantSoundPatches` with `CallContext` scopes around the local-body one-shot vocalization paths:
  - `PantSound.Update` → pain (AudioClip) and yawn (string),
  - `PantSound.Bark` → B-key bark (AudioClip),
  - `PantSound.TryGrowl` → low-happiness growl (string).
- Extended `SoundPlayPatch` / `SoundPlayAudioClipPatch` to map the new scopes into `CharacterSoundPolicy`, which classifies them into the new kinds.
- Added `LockpingSoundPatches.LockpingMinigamePainPatch` around `LockpingMinigame.Update`, with a new `CallContext.Origin.CharacterLockpickPain`.
- Added `CharacterSoundPolicy.Origin.LockpickPain`; it classifies only the lockpick-failure clip `gore2` as `CharacterSoundKind.Pain`, and deliberately leaves the success `unlock` sound unreported.
- Reuses the existing Pain wire kind; no additional `ProtocolVersion` bump beyond the original PantSound vocalization bump.
- The continuous pant loop is not captured: the pant loop is an `AudioSource`, not a `Sound.Play` call, so it cannot be captured.
- Remote clones keep `PantSound` disabled; the one-shot vocalizations replay on the owner's clone through the existing `CharacterSoundSync` path under `RemoteApply` (no echo, no double audio).
- `ProtocolVersion.Current` bumped 1 → 2 because the wire gains new `CharacterSoundKind` values (active decision #137).

## Evidence

- `tests/.../Session/CharacterSoundPolicyTests.cs` — new kinds and origin classification.
- `tests/.../Session/CharacterSoundSyncTests.cs` — protobuf roundtrip and follow-owner facts for Pain/Bark/Growl/Yawn.
- `tests/.../Patching/CharacterSoundPatchTests.cs` — PantSound patch contracts and reflective presence, plus the new `LockpingMinigamePainPatch` contract/scope shape.
- `tests/.../Session/CharacterSoundPolicyTests.cs` — `LockpickPain` origin classifies `gore2` as Pain and rejects `unlock`.
- Red→green: the new tests were observed failing (3 failures) on the pre-fix code before the implementation was re-applied.
- Independent adversarial review (fresh subagent) passed.
- `docs/evidence/selfchecks/presentation/speech-sound-frequency-selfcheck.md` — updated from local-only residual to one-shot event path.
- `docs/evidence/selfchecks/presentation/owner-local-body-auto-events-selfcheck.md` — clone suppression remains; one-shot vocalizations now evented.
- `docs/evidence/selfchecks/players/character-sound-selfcheck.md` — note added for the new kinds.

## Acceptance status

Red→green complete: the regression tests first failed on the pre-fix code (3 failures), then passed after the implementation. Full solution test suite passes (2337 tests + 16 normative-gate tests), `dotnet format` passes, independent adversarial subagent review passes, and the latest DLLs are deployed to the real game directory with SHA-256 hashes matching the build output. Moved to `review/` for the unified acceptance pass.

## Non-goals

- Not syncing the continuous pant loop or physiological per-frame audio.
- Not adding voice chat.
- Not expanding to every remaining local-only one-shot body sound unless a later observed-data ticket re-opens them.
