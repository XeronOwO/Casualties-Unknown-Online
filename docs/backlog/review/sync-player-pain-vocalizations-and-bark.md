# Sync player pain vocalizations and B-key bark to remote players

- Status: Review
- Priority: Medium
- Category: Character audio / player presentation sync
- Source: User report (2026-09-04) — the host's pain scream/groan and the sound triggered by pressing B are not heard on the guest client. The reverse direction (guest → host) was not tested by the user and is covered by the same star-relay path during the fix.

## Goal

Make the host's pain vocalizations and B-key bark audible to the guest (and verify the reverse direction). The fix fits the existing dedicated character-sound event path; no per-frame audio stream is introduced.

## Implementation

- Added `CharacterSoundKind.Pain` (7), `Bark` (8), `Growl` (9), `Yawn` (10) to the existing `CharacterSoundMsg` dedicated one-shot event family.
- Added `PantSoundPatches` with `CallContext` scopes around the local-body one-shot vocalization paths:
  - `PantSound.Update` → pain (AudioClip) and yawn (string),
  - `PantSound.Bark` → B-key bark (AudioClip),
  - `PantSound.TryGrowl` → low-happiness growl (string).
- Extended `SoundPlayPatch` / `SoundPlayAudioClipPatch` to map the new scopes into `CharacterSoundPolicy`, which classifies them into the new kinds.
- The continuous pant loop is not captured: the pant loop is an `AudioSource`, not a `Sound.Play` call, so it cannot be captured.
- Remote clones keep `PantSound` disabled; the one-shot vocalizations replay on the owner's clone through the existing `CharacterSoundSync` path under `RemoteApply` (no echo, no double audio).
- `ProtocolVersion.Current` bumped 1 → 2 because the wire gains new `CharacterSoundKind` values (active decision #137).

## Evidence

- `tests/.../Session/CharacterSoundPolicyTests.cs` — new kinds and origin classification.
- `tests/.../Session/CharacterSoundSyncTests.cs` — protobuf roundtrip and follow-owner facts for Pain/Bark/Growl/Yawn.
- `tests/.../Patching/CharacterSoundPatchTests.cs` — PantSound patch contracts and reflective presence.
- `docs/evidence/selfchecks/presentation/speech-sound-frequency-selfcheck.md` — updated from local-only residual to one-shot event path.
- `docs/evidence/selfchecks/presentation/owner-local-body-auto-events-selfcheck.md` — clone suppression remains; one-shot vocalizations now evented.
- `docs/evidence/selfchecks/players/character-sound-selfcheck.md` — note added for the new kinds.

## Acceptance status

Code-complete; moved to `review/` for the final unified acceptance pass. The reverse direction uses the same bidirectional star-relay (`CharacterDataStore.SendCharacterSound` → host broadcast/relay) and is covered by the existing `CharacterSoundSyncTests` guest-report/relay scenarios.

## Non-goals

- Not syncing the continuous pant loop or physiological per-frame audio.
- Not adding voice chat.
- Not expanding to every remaining local-only one-shot body sound unless a later observed-data ticket re-opens them.
