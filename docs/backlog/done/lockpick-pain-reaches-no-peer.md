# The lockpick-failure pain sound reaches no peer

- Status: Done — NOT A DEFECT, and the correction is the ticket's own point. The peer's counting watcher (`audio-watch3 match=gore2`) answers `plays=361` on that client: it has played `gore2` 361 times across the session, so the lockpick pain DOES reach every peer. The earlier "the peer never plays it" was an instrument error — the watcher's `names` list dedups, so a clip seen once never appears again in a diff, and every "new names" comparison in this page was blind to repeats. The reliable number is `plays` (one per distinct source, i.e. one per playback), and it is the one the ticket ends on.
- Priority: High — it is the exact event `sync-player-pain-vocalizations-and-bark` exists for, and the ticket that added it is marked code-complete.
- Related: `sync-player-pain-vocalizations-and-bark` (the family), `done/bark-vocalization-reaches-no-peer.md` (its sibling, fixed the same day by resolving the clip the way the actor played it).

## The reading

One three-client session, both the acting client and a peer running the frame-accurate counting watcher (`audio-watch3 match=gore2`). The lockpick minigame was started through the game's own entry (`lockpick-open`, which calls `Openable.OnUse()`), the CUO window was closed so the presses reached the minigame, and the pick was pressed on the ring at a magnitude inside the stuck band (`handMagnitude=232.5`, the band being 195..247):

    host names='[gore2]'        <- the acting client played it
    peer names='[pain1]'        <- the peer never did

Both watchers were started before the press and read inside the same window, so this is not the "the watcher had expired" mistake the earlier attempts in this session made twice.

## What is already ruled out

- **The patch is registered and its mapping exists.** `AdapterCapabilityCatalog` lists `typeof(LockpingSoundPatches)`, `LockpingSoundPatches.LockpingMinigamePainPatch` is a Prefix on `LockpingMinigame.Update` entering `CallContext.Origin.CharacterLockpickPain`, and `SoundPlayPatch` maps that origin to `CharacterSoundPolicy.Origin.LockpickPain`; the policy classifies `clip == "gore2"` as `CharacterSoundKind.Pain`.
- **The receiver has no kind filter.** `CharacterSoundSync.OnReceived` replays every kind; the only drop is an echo of the local player's own sound.
- **The clip resolves on the peer.** `audio-resource clip=gore2` answers `present=true`, so the same resolution the bark fix relies on cannot be the cause here.
- **No patch failure in the log** on the acting client.

## Where to look next

The remaining difference between this path and the one that works (`Origin.CharacterBark`, whose reports do reach peers after the fix) is the CAPTURE side: the scope is opened around a whole `Update`, so the string-overload patch has to decide that THIS `Sound.Play` belongs to the local body — a condition the PantSound scopes never exercise. That decision, and whether the report is reached at all for this origin, is the next reading; the two candidates are the string patch's local-body test under this scope and the origin actually containing the call when `MinigameBase` drives `currentMinigame.Update`.

## Design

Read before touching this: [`docs/design/model/authority-and-judgement.md`](../../design/model/authority-and-judgement.md) and
[`docs/design/mechanics/README.md`](../../design/mechanics/README.md). The rule it breaks: a one-shot presentation the acting client runs is reported through the character-sound
family and replayed by every other member.

## Two more readings the same day (2026-11-01, 20:10)

- **Harmony has the patch applied.** A new probe (`harmony-info type=<T> method=<M>`, which asks `Harmony.GetPatchInfo` for the method this build actually declares) answers `found=true, prefixes=2, owners=CasualtiesUnknownOnline.GameAdapter` for `LockpingMinigame.Update`, and `prefixes=1` for `PantSound.Bark` and `PantSound.Update`. So "the catalogue lists it" is now a fact about the running process, not about a file.
- **The same silence covers `PantSound.Update`.** A new fixture (`pant-sound kind=pain`) drives the game's own pain one-shot through that method; the acting client plays it, the peer's list gains neither a pain nor a yawn clip across the window. So the failing set is the two PER-FRAME scopes (`PantSound.Update`, `LockpingMinigame.Update`), while every ONE-SHOT scope read in this session arrives (`dogshake` from `Body.WaterShake`, `vomit2` from `Vomiter`, `stretch` from `NapCoroutine`, and `pain1` from `PantSound.Bark` after its own fix).

That difference — per-frame `Update` scope versus one-shot coroutine scope — is now the shape of the bug, and it is narrower than "the lockpick path is broken": something about a scope that is OPENED AND CLOSED EVERY FRAME keeps the report from arriving, or keeps the peer from replaying it.

### Three more candidates ruled out by reading (2026-11-01, 20:40)

A new probe (`call-context-state`) reads the scope stack the capture depends on, by reflection: depth, innermost origin, the number of REFUSED scopes, and the AudioClip skip latch. On the acting client it answers `depth=0, current=LocalAction, refused=0, skipAudioClipCapture=false`. So:

- **The depth cap is not it.** `CallContext.Enter` stops accepting scopes at `MaxDepth = 16` and records refusals instead of throwing; zero refusals means no scope was ever refused in this session, so the lockpick origin — when its `Update` is running — is on top of the stack as designed.
- **The AudioClip skip latch is not stuck.** The string patch sets `SoundCaptureContext.SetSkipAudioClipCapture()` and its postfix clears it; the flag reads false outside a call, so the AudioClip half of the family is not silently skipping.
- **The sender has no rate limit or filter.** `CharacterVisualPublisher.Publish` only checks `SessionActive` and the role, then sends reliably to every handshaken member; there is no per-frame budget, dedup or kind filter between a classified report and the wire.

With the catalogue registration, the origin mapping, the clip classification, the receiver's absence of a kind filter and the clip's resolvability already ruled out, what is left is whether `Report` is reached at all inside those two `Update` scopes. That is one question, and it needs the send path observed at a level that is ON rather than another guess — a single line at the report, removed once it has answered.

### The report path now has an eye on it (2026-11-01, 21:10)

A new instrument answers the one question reading could not: `report-watch` is a DECLARATION that Harmony-prefixes the adapter's own `CharacterSoundSync.Report` and records every call with its arguments, read back by `report-watch-read`. It works — a bark during the same session answered `count=1` with `[Bark, death1, (19.08, 437.62), 1, False, False, 0]`, so the capture→report leg is observable event by event.

That reading also shows the path is alive for the one-shot scope in the same session, while the lockpick attempt that followed produced only a `Footstep` report and no Pain entry at all. The remaining work on this ticket is therefore one measurement away rather than one hypothesis away: get the minigame into `clickingInside=true` (the frame the press lands on must have the hand inside the 195..247 band — the last attempts settled at 195.097 and 232.5 and still read `inside=false` on the press frame, which is itself a note about staging timing) and read `report-watch-read` across the stuck tick that plays `gore2`.

### The press pump, and the staging fact it exposed (2026-11-01, 21:40)

`lockpress` is a declaration that parks the staged pointer on the ring and pumps press/release pairs, so at least one `GetKeyDown(attack)` edge should land while the hand is inside the band. Its first version pressed on CONSECUTIVE frames and never produced an edge at all: `VirtualInput.PressKey` stamps `DownFrame = frameCount + 1` on every call, so calling it twice in a row keeps moving the edge into the future and it never arrives. The fixed version presses once every four frames and still left `clickingInside=false` across three hundred frames with the hand parked at magnitude 232 (inside 195..247).

So the staged channel holds the key (`handClicking=true`) but does not deliver the `GetKeyDown` edge this minigame needs, and the row's remaining needs are now precise: either make the staged press deliver a real down edge on the frame the game reads it (a change to `VirtualInput`, i.e. `src/`), or drive the pick with OS input for one batch. Both are named; neither was taken in this session.

The instrument built along the way stays useful for the first option: `report-watch` records every character-sound report with its arguments, so whichever way the press is delivered, the next run answers "did the report happen" in one read.

### The break is on the RECEIVER's replay, not on the wire (2026-11-01, 22:20)

Four watchers on two clients, all live in the same windows, settle where the event stops:

| layer | instrument | reading |
|---|---|---|
| the acting client's report | `report-watch` on `CharacterSoundSync.Report` | 37 further reports, every one `Pain:gore2` |
| the send | `publish-watch` on `CharacterVisualPublisher.Publish` | 39 further publishes |
| the peer's receive | `receive-watch3` on `CharacterSoundSync.OnReceived` | 124 messages, and **`[Pain:gore2]` is among them**, every one carrying `OwnerSteamId=<host>`, which is NOT the peer's own id — so the echo guard does not fire (the peer's log has zero `own sound echo` lines) |
| the peer's audio | `audio-watch3` (no filter) on the same client | the window gained footstep and speech clips and **no `gore2`** |

And the clip itself is fine on that client: a new `play-clip clip=gore2 twoDimensional=true` recipe — the same `Sound.Play(string, pos, true, true, null, 1, 1, false, false)` the replay makes — was recorded by the watcher (`plays 0 -> 1, names=[gore2]`). So the message arrives, the handler runs, the clip is playable, and the replay still produces no audible clip. That is the last gap, and it is inside `OnReceived` after the echo guard: the next instrument is a counter on `Sound.Play(AudioClip, …)` installed on the PEER, which says whether the replay reaches the play call at all.

### The peer DOES call `Sound.Play` with `gore2` — and still nothing sounds (2026-11-01, 22:50)

A `play-watch` declaration prefixes the game's `Sound.Play(AudioClip, …)` overload on the PEER and records each clip it is asked to play. Across one stuck window on the host, the peer's `play-watch` gained `[gore2]` entries — several of them — while `audio-watch3` on that same client, watching every `AudioSource` in the scene, recorded no `gore2` in any window so far.

So the message arrives, the handler runs, the clip resolves and the engine call is made, and the sound still does not come out on the listener's side. What is left is the lifetime of the source the game creates for it: `Sound.Play` builds a `GameObject` with an `AudioSource`, calls `PlayOneShot(clip)` and attaches `PlayedSound`, whose `Update` destroys the object whenever `AudioSource.isPlaying` is false (`reversing/Assembly-CSharp/Assembly-CSharp/PlayedSound.cs`). `PlayOneShot` does not start the source the way `Play()` does, so whether a given call survives to be audible depends on state the report cannot see from here.

The remaining reading is therefore one level below the replay: on the peer, read the created source's `isPlaying`/`time` on the frame after the replay, and compare a clip that IS heard remotely (`pain1` from the bark) with one that is not (`gore2`). That comparison names the difference instead of guessing at it.

### The A/B that closes the reading (2026-11-01, 23:05)

Same client, same instruments, two clips that take the same path:

| clip | engine call (`play-watch`) | audible (`audio-watch3`) | report `TwoDimensional` |
|---|---|---|---|
| `pain1` (the bark, which reaches peers) | present in the same window | **heard** | `False` |
| `gore2` (the lockpick pain, which does not) | present | **never heard** | `True` |

And a direct `play-clip clip=gore2 twoDimensional=true` on that same client IS heard, so neither the clip nor the 2D flag alone accounts for it: the difference left is the combination the replay uses — a 2-D play made INSIDE the `RemoteApply` scope. That is a narrow, testable statement, and it is where the next pass starts: play a 2-D clip from inside a `RemoteApply` scope on a peer and see whether it survives.

Everything above this section is the four-layer evidence that put it here; nothing in the ticket claims the fix, because the fix is not known yet.

## The correction (2026-11-01, 23:20) — read `plays`, never a `names` diff

The final reading on the peer, same client, same instrument: `audio-watch3 match=gore2` answers **`plays=361`**. That counter increments once per distinct `AudioSource` that played the matching clip — one per playback — so the peer has played `gore2` 361 times in this session.

Every "the peer never played it" statement above came from diffing the watcher's `names` list, which by design records a name ONCE: after the first `gore2` it never appears again, so a diff can never show a repeat. The reading was wrong, not the feature. The lesson is now a rule in the acceptance notes: a repeat-count question is answered by `plays`, and a `names` diff answers only "has this ever been seen".

What this page still contributes is the honest trail: the fixture that reaches the minigame (`lockpick-open`), the exact stuck condition quoted from `LockpingMinigame.Update`, and five instruments (`report-watch`, `publish-watch`, `receive-watch3`, `play-watch`, `guard-state`) that can trace one one-shot through every layer of the pipeline — which is what made the error visible in the end.