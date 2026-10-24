# The critical heartbeat is cut off before the clip ends

- Status: Todo — opened 2026-10-25 by the user's own listening report.
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

## Acceptance this ticket owes

A two-client session: the host's body forced critical (`body-force bloodPressure=…`), the host's log read for
the critical source's clip and playback state across a window of one clip length, then the same reading with a
remote medical panel open. The row passes when the clip plays through in BOTH states; the failing reading names
the line that restarts it.
