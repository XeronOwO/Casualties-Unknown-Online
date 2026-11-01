# The lockpick-failure pain sound reaches no peer

- Status: Review — found 2026-11-01 on the deployed build, in the same session as `bark-vocalization-reaches-no-peer` (which was the same family and is fixed): the acting client plays `gore2` when its lockpick sticks, and no other client ever does.
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