# Speech-Blips / Per-Frame Character Sound-Frequency Pass — Self-Check

Owner cycle: the backlog's character/presentation/combat bullet that kept "Speech
blips and other per-frame/per-step character sounds" local-only pending a
deliberate sound-frequency pass. Decision after the pass: **stay local-only, no
new `CharacterSoundMsg` event, and no new wire message.** The player-character
step/landing/attack/throw/exert/gunfire slice is already evented (see
`docs/character-sound-selfcheck.md`, `docs/weapon-fire-recoil-selfcheck.md`,
`docs/footstep-sound-selfcheck.md`); the remaining continuous/per-blip character
sounds are produced natively on each side from already-synced facts (speech text)
or are local UI/body effects whose volume does not justify per-event wire traffic.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence | Frequency / classification |
|---|---|---|---|
| 1 | Speech blips are played by `Talker.Update` while a bubble types out — per visible letter, `Sound.Play("speech")` or `"speechbad"` for a body, `talkSoundCustom` for a non-body trader. | `reversing/.../Talker.cs:380-414` (line 384 letter gate, 388-395 speech/speechbad, 404 talkSoundCustom) | Per-character bursts during dialogue; not a continuous loop |
| 2 | Remote speech bubbles are already synced as text: `SpeechMsg` (NetMsg 74) reports player bubbles / broadcasts trader bubbles, and `SpeechSync.Replay` writes the FINAL `currentString` + `timeSinceTalked = 0` into the peer's clone/trader Talker. | `src/.../World/SpeechSync.cs:35-66,84-144`; `src/.../Patches/TalkerPatch.cs:21-58` | One message per bubble; reliable |
| 3 | After a `SpeechSync.Replay`, the SAME `Talker.Update` path types the text out and therefore plays the SAME speech blips locally on the receiving side — no dedicated sound event is needed. | Inventory row 1 + row 2; the replay runs outside `RemoteApply` for the sound (only the bubble write is scoped) | Reconstructed locally, identical clip/cadence |
| 4 | Remote clones/guest traders are blocked from starting their OWN divergent bubbles (`TalkPatch.Prefix` returns false for `RemoteBodyDriver` clones and guest-side traders), so the only remote speech source is the synced text. | `TalkerPatch.cs:32-43` | Suppression, not audio suppression |
| 5 | Panting is a continuous looping `AudioSource` (`Sounds/pant`) on the local player's body, volume/pitch driven every frame by local stamina/consciousness; pain groans, yawns and growls are one-shot `Sound.Play` calls from the same component at 30-60 s timers or random chances. | `reversing/.../PantSound.cs:8-82`; `Body.cs:3434` (TryGrowl), `PlayerCamera.cs:982` (Bark) | Continuous loop + sparse one-shots; local physiological state is not a wire fact |
| 6 | Heart-thump is a screen-space 2D sound gated on `PlayerCamera.main.woundView` / critical dying — a local monitor/UI presentation. | `Body.cs:907-924` | Local UI; not spatial/peer-visible |
| 7 | Other one-shot player-body sounds are discrete action/damage effects: climb start (`Body.cs:477`), hand-switch (`Body.cs:1131/1427`), water pour (`Body.cs:1250`), combine (`Body.cs:1284`), gore (`Body.cs:2445`), nap stretch (`Body.cs:2510`), dog shake (`Body.cs:2553`), burp (`Body.cs:3142`), limb damage/break (Limb.cs:98/201/231/369/388/393), last-stand laugh (`Body.cs:1005`). | Decompiled call sites | Discrete one-shots, not per-frame; either ride their owning domain or are accepted local presentation |

## 2. Decision

- **Speech blips:** no new event. They are already reproduced on every side by
  the native `Talker.Update` typing animation whose `currentString` came from the
  synced speech text. Eventing them would double the work and, worse, risk
  double-audio if the local clone is ever replayed.
- **Pant loop / pain / yawn / growl / bark:** local-only. They are continuous or
  long-timer personal-body sounds driven by physiological state that is not a
  peer-visible fact; a dedicated event stream would be the first per-frame sound
  domain and has no observed volume evidence (the backlog's explicit
  prerequisite).
- **Other one-shot body/UI sounds:** accepted local presentation or already
  owned by their sync domains (item slot, crafting, limb state, block/building
  hit). No new wire traffic in this pass.
- **No protocol change:** `ProtocolVersion` stays at 20.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Speech blips on the speaking side | None (native `Talker.Update`) | `Talker.cs:380-414` |
| Speech blips on the receiving side | None (existing `SpeechSync.Replay` → native `Talker.Update`) | `SpeechSync.cs:131-144` + `Talker.cs:384-405` |
| No divergent re-talk on clones/guest traders | None (existing `TalkPatch.Prefix` suppression) | `TalkerPatch.cs:32-43` |
| Continuous pant / pain / yawn / growl / bark | None (local-only, recorded) | `PantSound.cs:8-82`; `Body.cs:3434`; `PlayerCamera.cs:982` |
| Local UI sounds (heart-thump) | None (local-only, recorded) | `Body.cs:907-924` |
| Wire format | Unchanged | `ProtocolVersion` unchanged; `DirectionTests` green |
| Patch-surface guard | New reflective tests lock the speech replay surface | `SpeechBlipReplayContractTests` (see §4) |

## 4. Verification design

- **L0 patch surface (reflective):** new `SpeechBlipReplayContractTests` locks
  the `Talker.Talk` patch contract, the `TalkPatch` Prefix/Postfix shape that
  carries the old/final bubble string, and the `SpeechSync.Replay(Talker,
  string)` static entry that feeds the native typing/audio path.
- **Existing contract guards:** `GameFieldContractTests` already locks the Talker
  fields `SpeechSync.Replay` writes (`currentString`, `timeSinceTalked`, `text`);
  `SpeechChannelTests` covers the bubble wire path.
- **Static evidence:** decompiled call sites in §1; `SpeechSync.Replay` and
  `TalkerPatch` source lines.
- **Runtime evidence:** development-period rule — L0 reflection + static
  evidence; **no manual acceptance** (user mandate 2026-08-16).
- **Gates:** `dotnet build`, `dotnet test`, `dotnet format`,
  `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1`.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 965 passed / 0 failed |
| `SpeechBlipReplayContractTests` focused filter | 3 passed (SpeechBlipReplayContractTests × 3) |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| architecture / event-replay / entity-event gates | all passed |
| ProtocolVersion | unchanged (20) |

## 6. Accepted residuals (recorded, not re-discovered)

- **Pant-loop on remote clones is a local approximation at best**: the peer's
  physiological fields (stamina/pain/energy) are not on the 20 Hz or 1 Hz
  character wire, so a remote clone's `PantSound` uses template/default state.
  It is normally silent (default stamina = 100 → volume 0) and is accepted as a
  local presentation boundary, not a sync bug.
- **One-shot body sounds not already owned by a sync domain** (stretch, dog
  shake, burp, hand-switch foley, gore, etc.) remain local-only; re-open only
  with observed runtime volume data showing they are audible-missing on peers.
- **Limb latch sounds** (bone break, dislocation, dismember, head hit, limb
  impact body-falls) are discrete damage sounds, not per-frame; they are not
  part of this frequency pass and remain tracked with the limb-presentation
  family if a deliberate damage-audio pass is ever scheduled.

## 7. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `docs/backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 8. Structure review

- Touched code: none in production paths (docs + tests only). The new test file
  is a single top-level type `SpeechBlipReplayContractTests`; no class crosses
  the 600-line gate, no new state bools, no dead mechanisms. The speech/bubble
  path remains the only source of remote speech text and therefore the only
  source of remote speech blips.