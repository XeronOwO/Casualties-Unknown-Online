# Footstep / Landing-Impact Sound Sync — Self-Check (2026-08-16)

Delivery fact sheet for the high-frequency character sound slice: the local
player's footsteps and landing impacts now travel as dedicated `CharacterSoundMsg`
events and replay on the owner's remote clone. This is the deliberate
sound-frequency pass the backlog's high-frequency/continuous sound bullet asked
for; speech blips and other per-frame/per-step sounds remain local-only and stay
tracked in `docs/backlog.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `Body.FootStep` is the single entry for every player step sound: animation events, jump/walljump take-off (Body.cs:2136/2174) and the landing roll (Body.cs:2739). | Body.cs:1169-1184, 2136, 2174, 2739 |
| 2 | Fallback step (no surface) plays `BSFootstep1..4` as a **string** `Sound.Play` call. | Body.cs:1183 |
| 3 | Material/water steps play `WorldGeneration.RandomStepSound(step)` as an **AudioClip** `Sound.Play` call; the clips are loaded from `Sounds/footstep/<step>/` (WorldGeneration.cs:127-133). | Body.cs:1175/1180; WorldGeneration.cs:132, 305-308 |
| 4 | Landing impacts play `impactSmall/Medium/Large.PickRandom()` as AudioClip `Sound.Play` calls on the `lastTimeStepVelocity.y` thresholds. | Body.cs:2729-2737 |
| 5 | The landing-impact AudioClip names are root `bodyFall1..5` resources (`resources.assets` contains the strings adjacent to `resources.resource`), so a wire `Clip = bodyFallN` is directly loadable by the string overload (`Resources.Load("Sounds/bodyFallN")`). | asset-string evidence from `resources.assets`; Sound.cs:52-54 |
| 6 | `Sound.Play(string)` internally calls `Sound.Play(AudioClip)`; a capture on both overloads must skip the internal call when the string overload already reported it. | Sound.cs:8-54 |
| 7 | The remote clone never runs `Body.Update`/`HandleGroundedState` (BodyUpdatePatch skips clones), so the capture scopes are opened only on local bodies (`RemoteBodyDriver` guard). | BodyPatches.cs BodyUpdatePatch, BodyFootStepPatch, BodyHandleGroundedStatePatch |

Whole-family audit for the covered triggers: every normal step, jump take-off,
walljump take-off and landing roll funnels through `Body.FootStep`; every landing
impact funnels through the three AudioClip calls in `Body.HandleGroundedState`.
Block/item/Limb step sounds (`BuildingEntity.cs:131`, `Limb.cs:388`, `Item.cs:244`)
are NOT covered: they are world-object sounds, not player-character sounds, and
would be a separate sound domain.

## 2. Design

- **One step / one landing = one message.** The existing `CharacterSoundMsg`
  chain is reused unchanged (no new fields; two new enum kinds
  `Footstep` and `LandingImpact`). The source plays the sound natively, the
  capture reports the exact clip/position/volume/spatial mode, and the receiver
  replays it on the owner's clone under `RemoteApply`.
- **Call-identity scopes, not guessing.** `Body.FootStep` opens
  `CharacterFootstep`; `Body.HandleGroundedState` opens
  `CharacterLandingImpact`. The nested `FootStep` call inside `HandleGroundedState`
  reports as a footstep, the impact AudioClips before it report as landing.
- **Material step paths are made loadable on the wire.** The AudioClip patch
  cannot send `clip.name` alone for a material step (the clip lives under
  `Sounds/footstep/<step>/`); the FootStep patch stores the category prefix and
  the AudioClip patch sends `footstep/<step>/<clip.name>`.
- **No double-report from the string overload's internal AudioClip call.** A
  `SoundCaptureContext` thread-static flag is set after a string report and
  cleared in the string patch's postfix; the AudioClip patch skips that same
  physical call.
- **Volume evidence (the backlog's explicit prerequisite):** footsteps are not
  per-frame emissions. They are animation-event / jump / landing calls — at
  human movement cadence a few Hz per player, comparable to the existing 1 Hz
  character snapshot + 20 Hz state stream. A lost message is a missed one-shot
  sound (acceptable degradation), so no batching or rate limit is introduced
  without runtime volume data.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Footstep capture | CallContext scope on `Body.FootStep` + string/AudioClip `Sound.Play` capture | BodyPatches.cs BodyFootStepPatch; SoundPlayPatch.cs; SoundPlayAudioClipPatch.cs |
| Landing-impact capture | CallContext scope on `Body.HandleGroundedState` + AudioClip `Sound.Play` capture | BodyPatches.cs BodyHandleGroundedStatePatch; SoundPlayAudioClipPatch.cs |
| Exact clip on the wire | fallback `BSFootstepN` string; material `footstep/<step>/<clip.name>`; landing `bodyFallN` | CharacterSoundKind.cs; FootstepSoundCapture.cs; SoundPlayAudioClipPatch.cs |
| No string→AudioClip double report | `SoundCaptureContext` skip flag set in string Prefix / cleared in Postfix | SoundCaptureContext.cs; SoundPlayPatch.cs |
| No remote-clone echo | scopes open only on local body; replay runs under `RemoteApply` | BodyPatches.cs; CharacterSoundSync.cs |
| Wire compatibility | ProtocolVersion 18→19 refuses mixed-version sessions (v18 peer would silently miss steps/landings) | ProtocolVersion.cs |
| Structure | no file crosses the 600-line gate; `BodyPatches.cs` stays one top-level type with nested patch classes | tools/check-architecture.ps1 |

## 4. Verification design

- **L0 pure machine:** `CharacterSoundPolicyTests` — footstep and landing-impact
  classification, empty clips never reportable.
- **L0 wire/simulation:** `CharacterSoundSyncTests` — new kinds round-trip with
  their exact clip paths through `NetPacket`.
- **L0 patch surface (reflective):** `CharacterSoundPatchTests` — `BodyFootStepPatch`
  scope + prefix state shape, `BodyHandleGroundedStatePatch` prefix/postfix
  shape, `FootstepSoundCapture` surface, and the `Sound.Play` AudioClip overload
  contract in `PatchInventory`.
- **Contract guards:** `PatchContractChecker` automatically covers every new
  `[HarmonyPatch]` class (Body.FootStep, Body.HandleGroundedState, Sound.Play
  AudioClip overload) — a game update that breaks a target fails `dotnet test`
  before launch.
- **Static evidence:** decompiled call sites above + the `bodyFallN` root-asset
  string evidence from `resources.assets`.
- **Runtime evidence:** development-period rule — L0 simulation + static
  evidence + the real-game-dir deploy; **no manual acceptance**
  (user 2026-08-16 mandate).

## 5. Accepted residuals (recorded, not re-discovered)

- **Speech blips and other per-frame/per-step character sounds** (Talker speech
  blips, panting, dog-shake, etc.) remain local-only and stay open in
  `docs/backlog.md`.
- **World-object step sounds** (`BuildingEntity`, `Limb`, `Item` RandomStepSound
  calls) are outside this player-character sound slice.
- **Pitch shift stays local random** as before — the wire carries pitch 1 +
  pitch-shift enabled, so each receiver rolls its own variation.