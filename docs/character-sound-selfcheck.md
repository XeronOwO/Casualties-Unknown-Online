# Character / Block Sound Sync — Self-Check (2026-08-16)

Delivery fact sheet for the "Character sound / block sound sync" backlog item.
The low-frequency player-action sounds (attack swing / throw swing / exert)
now travel as one dedicated reliable `CharacterSoundMsg` and replay on the
owner's remote clone; the block hit/break half was already synchronized by
the native `DamageBlock` apply path and is now recorded with evidence instead
of remaining an open question; remote building-entity hit sounds replay on
the existing `BuildingEntityDamaged` relay (no extra message).

> Note (ProtocolVersion 18): the same `CharacterSoundMsg` event was later
> extended with `CharacterSoundKind.GunFire` + `RecoilDegrees` so weapon shots
> and recoil replay on the owner's clone — see
> `docs/weapon-fire-recoil-selfcheck.md`.
> Note (ProtocolVersion 19): the same `CharacterSoundMsg` event was later
> extended with `CharacterSoundKind.Footstep` + `LandingImpact` — see
> `docs/footstep-sound-selfcheck.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `Body.Attack` plays its weapon swing sound from the item's `AttackInfo.swingSounds` at the body position, 3D, pitch-shifted, parented to the body, volume `atk.volume` | Body.cs:1912 |
| 2 | `Body.ThrowItem` plays one of `BSSwing1..4` at the body position, 3D, pitch-shifted, no follow | Body.cs:1668 |
| 3 | `Body.TryExertSound` plays one of `exert1..4` when its `Random.value < chance` gate passes, 2D, parented to the body | Body.cs:2103-2109 |
| 4 | The string `Sound.Play` overload is the only character-sound call shape for 1-3 (the building hit sound uses the `AudioClip` overload) | Sound.cs:19-31; Body.cs:1912/1668/2107/1953 |
| 5 | Capture identity is a `CallContext` scope: `CharacterAttack` around `Body.Attack`, `CharacterThrow` around `Body.ThrowItem`, `CharacterExert` around `Body.TryExertSound`; clones never open a scope (`RemoteBodyDriver` guard) | CallContext.cs; BodyPatches.cs (Attack + TryExertSound); BodyItemPatches.cs (ThrowItem) |
| 6 | `SoundPlayPatch` reports the EXACT clip captured from the real string `Sound.Play` call; block hit sounds never reach it because `WorldGeneration.DamageBlock` runs in the innermost `DamageBlockOrigin` scope | SoundPlayPatch.cs; WorldGenerationDamageBlockPatch.cs |
| 7 | The pure classifier maps scope + clip to `CharacterSoundKind`; empty clips and unknown scopes are never reportable | CharacterSoundPolicy.cs |
| 8 | `CharacterSoundMsg` carries owner, kind, exact clip, position, volume, follow-owner and 2D mode — one sound = one message | CharacterSoundMsg.cs; CharacterSoundKind.cs |
| 9 | Star relay: guest → host report, host fires the received event (adapter replays) and broadcasts to the others source-excluded; the host's own sound broadcasts to every handshaken guest | CharacterSoundHandler.cs; CharacterDataStore.SendCharacterSound |
| 10 | The receiver replays inside `RemoteApply` (the capture patch cannot echo) and parents the sound to the owner's render clone when the source followed the body | CharacterSoundSync.cs |
| 11 | Block hit/break sounds are ALREADY remote-applied natively: every `BlockDamaged` apply calls `world.DamageBlock(cell, dmg, hitSound: true, …)`, and `WorldGeneration.DamageBlock` plays the block `hitsound` while the block survives and both `hitsound` + `RandomStepSound` on a break | BlockBreakSync.cs:222/249; WorldGeneration.cs:742-743, 846 |
| 12 | Building-entity hit sound: the source plays `buildingEntity.hitSound` at the raycast point (Body.cs:1953); the existing `BuildingEntityDamaged` relay now plays the entity's own `hitSound` when it applies the damage | WorldEventSync.cs (OnRemoteBuildingEntityDamaged) |
| 13 | Wire compatibility: a v16 peer would silently miss every remote action sound — `ProtocolVersion` 16→17 refuses mixed-version sessions | ProtocolVersion.cs; NetMsg.cs (CharacterSound = 94) |

Whole-family audit of the covered triggers: in the decompiled assembly the
string-sound calls inside `Body.Attack` are exactly the swing call
(Body.cs:1912) plus `TryExertSound` (Body.cs:2107, nested and covered by its
own patch); the block-hit strings fire inside `WorldGeneration.DamageBlock`
(WorldGeneration.cs:742-743/846), which already opens `DamageBlockOrigin`.
`Body.ThrowItem`'s only string sound is Body.cs:1668. No other string sound
can run in these scopes: building-entity hit sound is the `AudioClip`
overload (Body.cs:1953) and is covered through the damage relay.

## 2. Design

- **One sound = one dedicated message.** The patch reports from the real
  `Sound.Play` call, so the wire carries the exact chosen clip (`BSSwing3`,
  `laser`, `exert2`, …) — never a re-derived approximation. Reliable channel:
  the sound is a one-shot trigger, never the snapshot stream.
- **Capture is call-identity, not guessing.** The patch never inspects
  `AttackInfo` or re-rolls `Random`; the call-identity scope makes the real
  call the only evidence, and the innermost `DamageBlockOrigin` scope
  naturally excludes block sounds.
- **Replay is application, never re-simulation.** The receiver calls
  `Sound.Play` with the wire's clip/position/volume/spatial mode under
  `RemoteApply`; `FollowOwner` re-parents to the owner's render clone when it
  exists, otherwise the position fallback still plays.
- **Building hit rides the existing message.** `BuildingEntityDamaged` is the
  operation's message already; adding a second sound message would violate
  one-operation-one-message. The receiver plays the local entity's own
  `hitSound` — semantic replay, no asset path on the wire.
- **Block sounds needed no change.** The block-damage relay already applies
  through the game's own `DamageBlock`, which is the game's sound trigger;
  re-adding a sound event would double-play on every receiver.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Attack swing sound | dedicated event, exact clip from the real `Sound.Play` call | SoundPlayPatch.cs; BodyPatches.cs AttackScope |
| Throw swing sound | dedicated event, throw scope | BodyItemPatches.cs ThrowItemPatch |
| Exert sound (Attack/Jump/body actions) | dedicated event, one patch on `TryExertSound` covers every call site | BodyPatches.cs BodyTryExertSoundPatch |
| Clone echo | capture patch skips `RemoteApply`; replay wraps in `RemoteApply` | SoundPlayPatch.cs; CharacterSoundSync.cs |
| Block sounds inside an attack | excluded by the innermost `DamageBlockOrigin` scope | WorldGenerationDamageBlockPatch.cs; WorldGeneration.cs:846 |
| Star relay | host applies (fires adapter event) + relays source-excluded; host's own sound broadcasts | CharacterSoundHandler.cs; CharacterDataStore.SendCharacterSound |
| Follow semantics | attack/exert follow the owner's clone; throw/position fallback | CharacterSoundSync.cs |
| Building hit sound | plays the local entity's `hitSound` on remote damage apply (semantic, no wire asset path) | WorldEventSync.cs OnRemoteBuildingEntityDamaged |
| Block hit/break sound | verified already native via remote `DamageBlock` | BlockBreakSync.cs:222/249; WorldGeneration.cs:742-743/846 |
| Protocol direction/version | NetMsg 94 bidirectional in `DirectionTests`; ProtocolVersion 16→17 | DirectionTests; ProtocolVersion.cs |
| Structure | GameAdapter split into `GameAdapter.CharacterSound.cs`; both previously-boundary files stay ≤600 | tools/check-architecture.ps1 |

## 4. Verification design

- **L0 wire/simulation**: `CharacterSoundSyncTests` — full-field protobuf
  roundtrip, guest report → host fires event + relay to the other guest, host
  own sound → both guests, relay fires on the other guest.
- **L0 pure machine**: `CharacterSoundPolicyTests` — attack/throw/exert
  classification, empty/unknown never reportable.
- **L0 patch surface (reflective)**: `CharacterSoundPatchTests` —
  `BodyTryExertSoundPatch` prefix/postfix shape, `ThrowItemPatch` throw-state
  scope shape, `CharacterSoundSync.Report` capture-fact shape, and every
  target (`Sound.Play`, `Body.Attack`, `Body.ThrowItem`,
  `Body.TryExertSound`) declared in `PatchInventory` (the same contracts the
  game-update guard resolves).
- **Contract guards**: `PatchContractTests` automatically covers the new
  `[HarmonyPatch]` class (`BodyTryExertSoundPatch`); the existing
  `SoundPlayPatch` contract now also locks the new prefix parameter names.
- **Static evidence**: the decompiled call-site inventory and
  `WorldGeneration.DamageBlock` sound lines above.
- **Runtime evidence**: development-period rule — L0 simulation + static
  evidence + the real-game-dir deploy; **no manual acceptance**
  (user 2026-08-16 mandate).

## 5. Accepted residuals (recorded, not re-discovered)

- **High-frequency/continuous character sounds**: footsteps and landing
  impacts are now evented (ProtocolVersion 19) — see
  `docs/footstep-sound-selfcheck.md`. Speech blips and other per-frame/
  per-step sounds remain local-only and stay tracked in `docs/backlog.md`.
- **Building hit replay uses the entity centre**, not the source's raycast
  hit point — the position-keyed relay carries only the entity position and
  the difference is inaudible for entity-local hit sounds.
- **Pitch shift stays local random**: the wire carries pitch 1 + pitch-shift
  enabled, so each receiver rolls its own `Random.Range(pitch ± 0.15)`; sound
  variation is presentation, not state.