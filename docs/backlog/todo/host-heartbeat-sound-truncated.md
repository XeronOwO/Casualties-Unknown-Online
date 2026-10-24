# The critical heartbeat is cut off before the clip ends

- Status: Todo — opened 2026-10-25 by the user's own listening report.
- Priority: Medium-High — it is the sound a player hears while their body is dying.
- Category: Audio / local body presentation
- Source: the user, playing the host: the CRITICAL heartbeat ("状况危急的心跳声", the sound that plays while the
  body is in a critical state) does not finish before it is cut off — the truncation is obvious and unpleasant.
  Reported mid-session; this ticket is the record of the report and the investigation it needs.

## The lead this repository already contains

The critical loop is the game's own `MusicManager.critSource` AudioSource, and two CUO paths touch that exact
object:

- `SpectatorCameraService` reads `MusicManager.main.critSource` and plays its clip on a source it owns
  (`_crisisSource`), stopping it when the watched body recovers — that one is the spectator's, and it must never
  be the local body's sound.
- The remote medical panel's alarm follows the VIEWED player (done ticket
  `remote-medical-panel-alarm-follows-viewed-player.md`). If that follow re-points or re-plays the single
  `MusicManager.critSource` while a remote view is open, then on the HOST it is restarted every frame — and the
  host hears its own critical heartbeat cut mid-clip, which is exactly the report.

## What this ticket must establish

1. Which source actually plays the sound the user heard: read `MusicManager.critSource`'s clip, its loop flag and
   its `isPlaying`/`time` on the host while the body is critical, before and after a remote view is opened.
2. Whether the panel's alarm writes that same source page (the follow's implementation), and whether it also
   touches it while the view is closed.
3. Whether the clip is long enough that a per-frame restart is audible (the truncation the user reports implies
   it is).

## Acceptance this ticket owes

A two-client session: the host's body forced critical (`body-force bloodPressure=…`), the host's log read for
the critical source's clip and playback state across a window of one clip length, then the same reading with a
remote medical panel open. The row passes when the clip plays through in BOTH states; the failing reading names
the line that restarts it.
