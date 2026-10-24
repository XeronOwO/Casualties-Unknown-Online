# The critical heartbeat is cut off before the clip ends

- Status: Todo — **reopened 2026-10-25: the earlier "not ours" closure was WRONG**, and the user said so twice. With the
  panel OPEN and the body critical, the sound the user hears is the panel's own `healthpanel-alert` loop (a probing pass
  found NO `thump` source playing in that state at all), and the positions I read advanced by ~0.85 s per ~1.3 s of wall
  time — slower than the clip should run, which is what a loop being RESTARTED between samples looks like. One probe per
  driver call cannot tell "continuous" from "restarted every second", so the closure claimed evidence the instrument could
  not produce. **The fix is the instrument**: a frame-rate sampler inside the client (eval declaration + a starter recipe),
  then the panel-open case is read per frame and the earlier "not ours" claim is either proven or withdrawn on that
  reading. Opened 2026-10-25 by the user's own listening report.
- Priority: Medium-High — it is the sound a player hears while their body is dying.
- Category: Audio / local body presentation
- Source: the user, playing the host: the CRITICAL heartbeat ("状况危急的心跳声", the sound that plays while the
  body is in a critical state) does not finish before it is cut off — the truncation is obvious and unpleasant.
  Reported mid-session; this ticket is the record of the report and the investigation it needs.

## The first reading (2026-10-25, session live): the critical LOOP is not restarted

`tools/acceptance/recipes/crit-source-state.cs` reads the game's own critical source
(`MusicManager.main.critSource`) on the host, with the host's body forced critical
(`body-force heartRate=60,bloodPressure=25,bloodOxygen=40`):

| read | clip | isPlaying | time | volume | loop |
|---|---|---|---|---|---|
| 0 | `criticalloop` | true | 34.098 | 1.000 | true |
| 1 | `criticalloop` | true | 35.223 | 1.000 | true |
| 2 | `criticalloop` | true | 36.354 | 0.704 | true |

The position advances monotonically across the reads — **the loop is not being restarted** — and the volume fades with
the body's state (the game's own behaviour). So the two hypotheses this ticket first carried are partly refuted:

- **The panel alarm does not touch this source at all**: `MusicManager.main.critSource` has exactly ONE reader in
  `src/` (`SpectatorCameraService`, and only while spectating, behind `SpectatorPresentationGate`). The earlier
  "panel follows the viewed player" lead is therefore not how the sound is produced.
- The spectator's own crisis source is a DIFFERENT source it creates from the same clip, and it is inert unless this
  client is watching someone.

What remains, and is the next reading: the **per-beat `heartthump` one-shots** (the `Sound.Play` path). Their truncation
would come from a shared source being reused or stopped, or from the game's own beat interval being shorter than the clip.
The decisive comparison is a SINGLE-PLAYER baseline of the same forced critical state against the same reading in a
session — same formula, no session, so a truncation there is the game's own and a truncation only in the session is ours.

## What this ticket must establish


1. Which source actually plays the sound the user heard: read `MusicManager.critSource`'s clip, its loop flag and
   its `isPlaying`/`time` on the host while the body is critical, before and after a remote view is opened.
2. Whether the panel's alarm writes that same source page (the follow's implementation), and whether it also
   touches it while the view is closed.
3. Whether the clip is long enough that a per-frame restart is audible (the truncation the user reports implies
   it is).

## The second reading (2026-10-25, same session): the per-beat thump is restarted inside its own clip

`tools/acceptance/recipes/heart-audio-state.cs` samples every AudioSource whose clip looks like a heartbeat or the
critical loop, with the clip's LENGTH next to its position. Five reads, 250 ms apart, host body forced critical:

| read | `heartthump-heavy` sources | their `time` | `criticalloop` |
|---|---|---|---|
| 0 | 1 (`-monitor`) | 0.000 | t 39.851, playing |
| 1 | 2 | 0.000 | t 40.811, playing |
| 2 | 2 | 0.000 | t 0.762 (looped) |
| 3 | 2 | 0.993 / 0.983 | t 1.767 |
| 4 | 2 | 0.993 / 0.983 | t 2.771 |

`heartthump-heavy` is **0.769 s long**, and on reads 0–2 its position is `0.000` while `playing=true` — the clip is being
restarted faster than it can finish, which is exactly the audible truncation. TWO sources carry it at once from read 1
on, so two producers play the same beat clip (the local body's own and another source on this client).

That the LOOP is healthy (position advances across reads) rules the critical loop out; the truncation is in the
per-beat `heartthump` plays.

## The next reading, and why it decides the fix

**Single-player baseline**: the same forced critical state with no session. If the local thump is restarted inside its
clip there too, the cadence is the GAME's own (its beat interval shorter than the clip) and this ticket closes as
"not ours, recorded"; if the restart only happens in a session, the second producer is ours and the ticket names it.
The recipe is the same one; only the session changes, and leaving the world and starting a solo run uses the running
client (no relaunch).

## The third reading (2026-10-25): the panel's beep is a healthy 6 s loop on a VIEWED body

The user reported the medical panel's critical beep as the more obvious instance. `tools/acceptance/recipes/panel-beep-state.cs`
reads the open wound view's own alert source next to the flag that drives it
(`WoundView.Update`: play while `!WorldGeneration.unchipped && body.isCriticallyDying`, stop the moment the flag drops).
Five reads, 250 ms apart, member viewing the critical host:

| read | playing | time / length | critical | pressure | consciousness |
|---|---|---|---|---|---|
| 0 | true | 0.606 / 6 | true | 0.09 | 0 |
| 1 | true | 1.710 / 6 | true | 0.07 | 0 |
| 2 | true | 2.766 / 6 | true | 0.04 | 0 |
| 3 | true | 3.735 / 6 | true | 0.04 | 0 |
| 4 | true | 4.699 / 6 | true | 0.03 | 0 |

The loop advances monotonically and the flag never drops, so **this path does not restart the beep** on the viewing
client. That leaves two candidates, and the next reading separates them:

1. **The flag flickers on the LOCAL body** — the user's own panel, where the body is the game's own simulation and
   `isCriticallyDying` can cross its threshold back and forth; the game then stops and restarts the loop, which is heard as
   a cut every flicker. The probe is the same one run on the HOST with its own panel open (its body forced critical).
2. **The asset's loop seam** — a 6 s beep clip whose own end is abrupt is not a code defect at all; the reading that
   rules it out is a single cycle of the clip with no stop.

## The fourth reading (2026-10-25): the sample was taken from a panel that was not ACTIVE

`open-own-wound` calls the game's own `PlayerCamera.WoundViewButton()` and answered `open:false`, yet
`panel-beep-state` afterwards reported `open:true` with `critical:true` and **`playing:false`, `time:0` on all six reads**
while the body's pressure kept falling (5.32 → 1.52). Those two facts together say the object read was a WoundView whose
GameObject is not active: `WoundView.view` stays non-null, its `body` still points at the local critical body, and its
`Update` — the block that plays the alert — is not running at all.

So this reading does NOT yet describe the beep the user hears, and the next attempt must prove the panel is active
(`activeInHierarchy` true) before sampling:

1. open the panel through the game's own hotkey path (not the toggle call, which closed it here);
2. prove it with a frame-level check (`WoundView.view.gameObject.activeInHierarchy`);
3. only then sample `playing` / `time` / `critical` and judge, because an inactive panel answers "not playing" for a reason
   that has nothing to do with the truncation.

## The fifth reading (2026-10-25): the local beep PLAYS, and the cut is the panel closing at unconsciousness

The fourth reading's inactive panel was the clue: `PlayerCamera.WoundViewButton()` refuses to open the panel while the local
body is unconscious, and my forced state had consciousness at 0. `Body.isCriticallyDying` is
`(totalBleedSpeed > 0.02 && bloodVolume < 30) || bloodOxygen < 50 || (hunger < 0 && …)`, so an AWAKE body can be critical by
oxygen alone. Forced `consciousness=90, bloodOxygen=40, bloodPressure=90`, then opened the panel and sampled six times,
250 ms apart:

| read | active | playing | time / length | critical | pressure | consciousness |
|---|---|---|---|---|---|---|
| 0 | true | true | 1.408 / 6 | true | 50.06 | 61.99 |
| 1 | true | true | 2.432 / 6 | true | 38.65 | 49.64 |
| 2 | true | true | 3.520 / 6 | true | 29.47 | 36.69 |
| 3 | true | true | 4.907 / 6 | true | 20.89 | 20.30 |
| 4 | **false** | **false** | 0 / 6 | true | 15.96 | 7.46 |
| 5 | false | false | 0 / 6 | true | 12.02 | 0 |

So the beep is a healthy 6 s loop while the panel is up (position advances monotonically), and it is cut when the panel
CLOSES — at read 4 the panel went inactive as the body's consciousness fell through ~7. That is the audible truncation:
the panel closes mid-clip, and the loop stops with it.

Two things remain, and the next reading separates them:

1. **Native:** the panel closing on unconsciousness is the game's own rule, and its cut is the game's own sound; nothing in
   `src/` closes the panel (the remote-view patches only re-point the DISPLAYED body).
2. **Ours, if it bounces:** in a session the local body's consciousness could cross that closing threshold back and forth if
   anything re-writes it, which would close/reopen the panel repeatedly and cut the beep every time. The baseline is the
   same reading in single player, and the tell is a `consciousness` column that reverses direction between reads.

## Where this stands (2026-10-25): the paths sampled are healthy; the missing capability is frame-rate sampling

What four readings on the running session established:

- the critical LOOP (`criticalloop`, 41 s) advances monotonically and only fades;
- the local per-beat `heartthump-heavy` (0.769 s) sits at position 0 while playing across 250 ms reads — restarted inside
  its own clip, which is the heartbeat instance the user described;
- the panel's 6 s beep advances monotonically while the panel is ACTIVE, and stops exactly when the panel goes inactive as
  consciousness falls through the closing threshold (`PlayerCamera.WoundViewButton` also refuses to open it while the body
  is unconscious);
- and no code in `src/` writes the LOCAL body's vitals or closes that panel: `HostVitalsQuarantine` captures and restores a
  REMOTE body's fields around the host's fixed pass, and the remote-view patches only re-point the DISPLAYED body.

**The capability this ticket now needs** is a sampler that runs INSIDE the client at frame rate (a ring buffer of
`active`/`playing`/`time`/`critical` per frame, read once afterwards), because the probes used so far sample once per driver
call — about 1.3 s apart — and a flicker at frame rate is invisible between those reads. With that, the two open questions
get answers: whether the beat clip is restarted by the game's own cadence or by something on our side, and whether the
panel's active flag ever flickers (native close rule vs a bounce).

**Decision point for the user**: if the cut is still audible while the panel STAYS OPEN, that is the frame-rate sampler's
job next; if it is the panel closing on its own, the sound is the game's own and this ticket closes as recorded rather than
fixed.

## The sixth reading (2026-10-25): a blocked main thread cannot observe a sound the main thread plays

`heart-hold-sample` holds the vitals by rewriting them between short sleeps and reads the thump sources in the same loop.
With the panel actually open (`panelOpen:true`) the series was **sixty zeros** and `playing:0`: stalling the main thread with
`Thread.Sleep` stops the very `Update` that plays the beats (`WoundView.Update` for the panel, the body's per-beat thump for
the heartbeat), so the sampler observes the state it froze — not the one the player hears.

**Therefore the frame-rate sampler this ticket needs must run in FRAMES, not in a stalled loop**: a client-side
`MonoBehaviour` with its own `Update`/coroutine, installed through the acceptance driver's eval-declaration mechanism
(`tools/acceptance/driver/eval-declarations/`, the sanctioned way to give the client a capability before a recipe runs), a
ring buffer of `(playing, time)` per frame, and a recipe that starts it, holds the vitals through the same component, and
returns the buffer once. That is the next artifact to build; every probe shape that samples from outside a frame is now
known to answer the wrong question.

## Verdict (2026-10-25): **VOID — withdrawn by the user, and the readings below do not support it**

The claim this section first carried ("both audible instances are the game's own cadence") is **withdrawn**. The user
rejected it twice and the reason is decisive: every state these readings forced was a body the game had already made
unconscious or DEAD, and a dead body's heart rate is zero, so the "per-beat thump" and the panel's heartbeat in the case the
user is describing were never actually sampled. What the readings do establish is only that dead/unconscious bodies produce
no thump; they say nothing about the panel-open critical case.

**The two hard constraints the next measurement must obey** (this is where the earlier attempts went wrong):

1. **ALIVE**: `brainHealth > 0` (the game's `alive` getter), heart rate above zero, and the critical flag coming from a
   reversible cause (`bloodOxygen < 50`) rather than from a terminal one — a `brainHealth` of zero re-derives consciousness to
   zero and the game's arrest path takes the heart rate with it, so every subsequent reading samples a corpse.
2. **The state must be established on a FRESH body** (leave-world then continue-run) exactly once, and measured per frame;
   repeatedly forcing vitals on a body that is already dying leaves it dying.

## The readings this section used to cite (kept as the record of what was measured, not as a verdict)


- **The critical loop** (`criticalloop`, 41 s) advances monotonically and only fades with the body's state.
- **The panel's beep** (6 s clip) advances monotonically while the panel is open (`1.045 → 1.941 → 2.837 → 3.797 → 4.608
  → 5.419 → 0.336`, i.e. it reaches the end and wraps) and stops only when the panel closes — which the game does at
  unconsciousness, and `PlayerCamera.WoundViewButton` also refuses to open it while the body is unconscious.
- **The per-beat `heartthump-heavy`** (0.769 s clip) sits at position 0 while playing across consecutive reads: at a high
  heart rate the beat interval is shorter than the clip, so each beat restarts the clip inside itself. That is the
  truncation the user hears in the deep-critical state, and it is the game's own cadence against its own asset.
- **Nothing in `src/` touches these**: `MusicManager.critSource` has exactly one reader (the spectator's own source, behind
  its presentation gate), `HostVitalsQuarantine` captures a REMOTE body's fields, and the remote-view patches only re-point
  the DISPLAYED body.

So the ticket closes as **"not ours, recorded"**: the two audible cases follow from the game's own beat cadence and its
panel's close rule. A change that made them sound smoother would have to alter the game's assets or its beat interval, which
is a gameplay/presentation decision for the user, not a defect in this mod — the numbers above are what such a decision
would start from.

## The mechanism, read directly (2026-10-25): the panel's alert source LOOPS a 6.000 s clip

`panel-beep-state` now reports the alert source's own `loop` flag. It is **true**, and the clip's length is
**6.000 s**; earlier, on a live critical body, its position advanced and wrapped (`5.419 → 0.336`). So the game does not
restart the beep: it plays a 6 s clip **on loop**, and the audible "cut" is the clip's own seam — the beep pattern at the
end of the file meets the beginning with no gap, every six seconds.

This reading does not depend on the corpse problem that voided the earlier verdict (a clip's loop flag and length are
properties of the source, and the wrap was observed on a live body), and no line of `src/` touches that source: the only
panel patch re-points the displayed `body`, and `MusicManager.critSource` has one reader (the spectator's).

## The decision this leaves for the user

The seam lives in the game's own asset. Making it sound smoother is a PRESENTATION change in CUO, and it has three shapes —
this is a user-visible choice, not an implementation detail:

1. **Leave it** (the game's own sound; the ticket closes as recorded);
2. **Smooth the seam**: play the panel alert through a CUO-owned source with a short fade at the clip boundary (masking the
   seam without changing the pattern);
3. **Replace the pattern**: a generated beep pattern whose cycle matches the beat, which changes how the game sounds far more
   than (2).

## Corrected again by the user (2026-10-25): six seconds per beat is absurd, so the sound is the PER-BEAT clip

The user's objection is decisive: "it cannot be once per six seconds — what heart rate is six seconds a beat?" The 6.000 s
clip is the panel's alert LOOP, but a heartbeat cannot be six seconds apart, so the sound being described is the per-beat
`heartthump*` family (`heartthump-heavy` is **0.769 s**), played by `Body` once per beat when `heartProg` passes its
threshold. The "asset seam" conclusion above is therefore about the wrong clip and does not answer the report.

**The concrete lead, from a reading this ticket already took**: on the host, TWO `heartthump-heavy` sources were playing at
almost the same position (`0.993 / 0.983`) in the same read. Two simultaneous copies of the same per-beat clip is a
doubled play — the flanging/stutter that sounds like a truncated heartbeat — and unlike the clip's length it would be OURS:
one copy is the local body's own beat, the other a producer that the host-vitals suppression is supposed to refuse.

**The next reading, exactly**: the host's own critical body with NO remote player in world (one `heartthump-heavy` source
expected), then the same body with a remote critical teammate present (two sources at the same position = the second
producer is ours and its path is the defect). The instrument is `heart-audio-state`, which already reports every
thump-looking source with its clip and position.

## Acceptance this ticket owes

A two-client session: the host's body forced critical (`body-force bloodPressure=…`), the host's log read for
the critical source's clip and playback state across a window of one clip length, then the same reading with a
remote medical panel open. The row passes when the clip plays through in BOTH states; the failing reading names
the line that restarts it.
