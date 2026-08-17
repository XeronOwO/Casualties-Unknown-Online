# Weapon-Fire Direction + Recoil Self-Check (#193)

Remote render clones should show the owner's gun the way the owner sees it:
the weapon points where the owner aims, and the barrel visibly kicks on each
shot. This cycle closes #193 after the #119 held-light direction fix.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The clone's gun AIM is already synced. `Body.HandleVisuals` computes `gunangle` from `(targetLookPos - limbs[1].position)` (Body.cs:3271) and `SessionStatePump` writes the peer's 20 Hz `LookPos` into the clone's `targetLookPos` (SessionStatePump.cs:63-67) | Body.cs:3271; SessionStatePump.cs |
| 2 | The only per-frame local-mouse item orientation calls are the three hand-slot light items fixed by #119 (`CustomItemBehaviour.cs:439/512/526`); the gun render path reads `targetLookPos`, not the mouse | CustomItemBehaviour.cs; Body.cs:3271 |
| 3 | The one missing piece is the shot-time recoil kick: `GunScript.Fire` adds `knockBack * 8` to the OWNER's `armsAnimator` `gunangle` on every shot (GunScript.cs:221) | GunScript.cs:221 |
| 4 | A render clone's `GunScript.Update` never fires (the clone is non-interactive, `RemoteBodyDriver` skips simulation), so the kick never happens on the clone | BodyPatches.cs; GunScript.cs:105-179 |
| 5 | The fire sound is a discrete one-shot event (`Sound.Play(this.fireSound, ..., twoDimensional: true, follow: null, ...)`, GunScript.cs:207) — it can ride the same dedicated character event channel as the attack/throw/exert sounds | GunScript.cs:207; CharacterSoundMsg.cs |

## 2. Design

- `CharacterSoundKind.GunFire` is a new kind on the existing **CharacterSound** event (NetMsg 94, already bidirectional star relay). A new `GunFirePatch` Postfix on `GunScript.Fire` reports the shot on the source side: exact clip name (`this.fireSound.name`), position, volume 1, 2D, `followOwner=false`, and `RecoilDegrees = knockBack * 8`.
- `CharacterSoundMsg` gains `[ProtoMember(8)] RecoilDegrees` so the receiver knows how far to kick the clone's weapon.
- `CharacterSoundSync` already replays the sound; for `GunFire` it additionally adds `RecoilDegrees` to the owner's clone `armsAnimator.gunangle`. `Body.HandleVisuals` lerps that extra angle back to the synced aim on the next frame (Body.cs:3271) — exactly the natural recoil transient.
- The clone's direction needed no new sync bit: it already follows the peer's target look position. No new NetMsg id, no direction-table change, no message routing change — ProtocolVersion 17→18 because an old peer would miss the new kind/field.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| gun direction | no code change — already synced via `targetLookPos` -> `gunangle` | Body.cs:3271; SessionStatePump.cs:63-67 |
| recoil capture | `GunFirePatch` Postfix on `GunScript.Fire` reports `CharacterSoundKind.GunFire` with clip name + `RecoilDegrees = knockBack * 8`; remote-clone guard | GunFirePatch.cs; GunScript.cs:221 |
| wire event | `CharacterSoundMsg.RecoilDegrees` ProtoMember(8), `CharacterSoundKind.GunFire`, ProtocolVersion 18 | CharacterSoundMsg.cs; CharacterSoundKind.cs; ProtocolVersion.cs |
| sound replay | existing `CharacterSoundSync.OnReceived` plays the clip under `RemoteApply` | CharacterSoundSync.cs |
| recoil apply | receiver adds `RecoilDegrees` to the owner clone's `armsAnimator.gunangle` when a clone exists | CharacterSoundSync.cs |
| no echo | source-side capture is inside a real `GunScript.Fire` and guarded by `RemoteBodyDriver`; receiver replay never calls `Fire` | GunFirePatch.cs; CharacterSoundSync.cs |
| lost-event degradation | one-shot presentation event, no persistent state to heal; snapshot never carries recoil | CharacterSoundMsg.cs |

## 4. Verification design

- L0 simulation: `CharacterSoundSyncTests` round-trips the new `RecoilDegrees` field and the GunFire kind; `GunFirePatchTests` locks the patch shape, the `PatchInventory` contract, and the protocol field/enum.
- Static evidence: decompiled call-site inventory above (the gun render path reads `targetLookPos`; the only local-mouse item calls are the #119 three).
- Runtime evidence: development-period rule — L0 simulation + static evidence, no manual acceptance (user 2026-08-16 mandate).
- Accepted residual: if the owner's clone does not exist yet at the receiver, the shot sound still plays at the position fallback but the recoil kick is skipped (a clone appears on the next clone-render pass).