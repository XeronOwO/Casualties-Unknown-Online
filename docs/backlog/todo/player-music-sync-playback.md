# A player's own music, heard by the group

- Status: Todo
- Priority: Medium
- Category: Feature / audio / bulk transfer
- Source: the user's 2026-10-07 backlog request — a music player (MP3 and the like) whose playback can be
  shared with the other players, so one player can play local music for the group. The user states the design
  preference themselves: the player who starts playback should first transfer the complete audio file to the
  others, and only then start playing, trading transfer time for a listening experience that survives high
  latency and an unstable network.
- Related: `docs/en/reference/mod-api.md` (the mod-facing surface a player like this could ship as),
  `review/pinyin-search-standalone-mod.md` (the precedent for a CUO-family mod that is not the core plugin),
  `review/global-adaptive-report-rate-flow-control.md` (bandwidth is a governed resource in this project),
  `todo/mod-defined-wire-packets.md` (a mod-owned packet family, if the player ships as a mod)

## What is asked

- Play local audio files, and share a track with the session so every member hears it.
- The user's own constraint on the mechanism: **transfer first, then play**. No streaming-with-buffering
  scheme where a slow peer hears gaps; the file arrives whole (or the playback waits), so the group's
  experience is not hostage to the slowest link's jitter.
- The transfer must behave under bad networks: high latency, packet loss, a member joining mid-song, a member
  whose transfer stalls, and a member who leaves. It must also be bounded — this project treats bandwidth as a
  governed resource (`review/global-adaptive-report-rate-flow-control.md`), so an unbounded push is not an
  option.

## What is not known yet

- **There is no bulk-transfer mechanism in the tree today** (a search for a file/asset/bulk transfer type
  under `src/` finds none), so this ticket owns inventing one: chunking, resume, integrity, and a rate that
  yields to gameplay traffic.
- Where the audio is played from. The game has its own audio pipeline; a mod-supplied clip can either be
  loaded from disk by each client (the file the transfer delivers) or pushed into Unity's audio system
  directly. The choice decides whether the transfer is a file or a decoded buffer, and it is the first thing
  to settle.
- What a listener may do about it (mute, volume, "only during rest"), which is a player-facing decision the
  cycle has to bring to the user before it ships.

## Required work

1. Read the project's own constraints first (bandwidth governance, the mod surface, the wire-change policy)
   and write the design down before code: file or buffer, chunk size, resume, integrity, the queue when more
   than one member plays, and the start rule ("everyone has it, now play" — or "play now, the rest catch up
   to the same offset" for a member who joins late, stated explicitly).
2. Build the transfer as its own capability, not inside the music feature: a second consumer is what proves it
   is a transport rather than one feature's private path.
3. Make the experience measurable: a slow peer must not delay the others' playback beyond the agreed rule, a
   stalled transfer must be observable and must not wedge the session, and the transfer's rate must be visible
   next to the existing traffic accounting.
4. Verify with three clients under a degraded link (throttled or high-latency peer): every listener hears the
   same track at the same offset, and the session's own traffic stays inside its budget.

## Non-goals

- Not a streaming service and not a media library: local files on one player's machine, shared for a session.
- Not a copyright or content policy surface — but the cycle must state where the files come from and what is
  shipped with the mod (nothing).
- Not part of the core plugin unless the design says so: the precedent for this family is a separate mod
  (`review/pinyin-search-standalone-mod.md`).
