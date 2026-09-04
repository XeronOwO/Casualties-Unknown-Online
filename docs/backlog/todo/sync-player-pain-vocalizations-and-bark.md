# Sync player pain vocalizations and B-key bark to remote players

- Status: Todo
- Priority: Medium
- Category: Character audio / player presentation sync
- Source: User report (2026-09-04) — the host's pain scream/groan and the sound triggered by pressing B are not heard on the guest client. The reverse direction (guest → host) was not tested by the user and must be self-checked during the fix. Record only; no code action taken yet.

## Goal

Make the host's pain vocalizations and B-key bark audible to the guest (and then verify the reverse direction too). The fix should fit the existing dedicated character-sound event path, not introduce a per-frame audio stream.

## Current behavior

The existing player-character sound sync covers only a curated set of one-shot action sounds:

- `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/CharacterSoundKind.cs` — `AttackSwing`, `ThrowSwing`, `Exert`, `GunFire`, `Footstep`, `LandingImpact`.
- `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/CharacterSoundPolicy.cs` — classification only understands those action scopes.
- `src/CasualtiesUnknownOnline.GameAdapter/Patches/SoundPlayPatch.cs` — reports a sound only when it fires inside a known call-identity scope (`Body.Attack`, `Body.ThrowItem`, `Body.TryExertSound`, `Body.FootStep`, `Body.HandleGroundedState`).
- `src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterSoundSync.cs` — replays reported sound on the owner's render clone and already handles star relay, source exclusion and RemoteApply echo suppression.

The user-reported pain/B sounds are not covered by any of those scopes/enum values. Relevant local-only paths:

- `PantSound.cs:8-82` — continuous pant loop plus one-shot pain groans, yawns and growls.
- `Body.cs:3434` — `TryGrowl`.
- `PlayerCamera.cs:982` — Bark (likely the B-key sound).
- `docs/evidence/selfchecks/presentation/speech-sound-frequency-selfcheck.md:21-22,31-32,48` — this was previously recorded as an accepted local-only residual: "Pant loop / pain / yawn / growl / bark: local-only ... a dedicated event stream would be the first per-frame sound domain and has no observed volume evidence."
- The same selfcheck's accepted-residual section also says these should be re-opened "only with observed runtime volume data showing they are audible-missing on peers." The user has now observed that.

## Scope for implementation

- Identify the exact native call sites for:
  - pain scream/groan triggers (likely one-shot `Sound.Play` calls driven by body pain/physiology timers or random chances),
  - B-key bark (`PlayerCamera.cs:982` or related).
- Add the appropriate `CharacterSoundKind` value(s) and extend the classification/scoping so these one-shot vocalizations are captured without capturing continuous pant-loop audio.
- Follow the existing one-shot event pattern: exact clip + position + volume + follow-owner semantics, reliable message, remote clone replay, no echo, source exclusion.
- Decide whether a rate limit/dedup is needed: pain/groan/bark can be sparse timers or random chances, but if any path can fire frequently, avoid flooding the reliable channel.
- Check protocol compatibility: adding enum values to `CharacterSoundMsg` may require a version bump/mixed-version handling decision; document whatever is chosen.
- **Both directions must be verified**: host → guest (reported) and guest → host (not tested by the user). During the fix, self-check the reverse path and add evidence for it, not assume it works.

## Acceptance criteria (for the later implementation cycle)

- A guest hears the host's pain vocalizations and B-key bark.
- After the fix, the reverse direction (guest → host) is verified to work the same way (not left untested).
- No double-audio, no echo of the owner's own sound, no clone-side local reproduction of the same vocalization.
- The continuous pant loop is not accidentally synced as a stream/event storm.
- Existing character-sound tests and repo gates remain green.
- New tests cover the added kind/classification and, where possible, the relay/replay path.
- If the protocol/version changes, the decision is recorded.

## Non-goals

- Not adding voice chat.
- Not necessarily syncing every remaining local-only one-shot body sound (stretch, burp, etc.) unless they are in the same observed vocalization family.
- Not implementing in this cycle — this ticket is a backlog record only.
