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
  `done/global-adaptive-report-rate-flow-control.md` (bandwidth is a governed resource in this project),
  `done/mod-defined-wire-packets.md` (a mod-owned packet family, if the player ships as a mod)

## What is asked

- Play local audio files, and share a track with the session so every member hears it.
- The user's own constraint on the mechanism: **transfer first, then play**. No streaming-with-buffering
  scheme where a slow peer hears gaps; the file arrives whole (or the playback waits), so the group's
  experience is not hostage to the slowest link's jitter.
- The transfer must behave under bad networks: high latency, packet loss, a member joining mid-song, a member
  whose transfer stalls, and a member who leaves. It must also be bounded — this project treats bandwidth as a
  governed resource (`done/global-adaptive-report-rate-flow-control.md`), so an unbounded push is not an
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

## The design (written before code, 2026-10-24)

**The transfer is its own capability, and it landed first.** `src/CasualtiesUnknownOnline.BulkTransfer.Core/`
is game-free (it references nothing): `BulkSender` slices a byte array into bounded chunks and tracks which are
outstanding, `BulkReceiver` reassembles by index and verifies the whole file, and `BulkTransferPolicy` holds the
rails. Nine tests pin the rules: a round trip byte for byte, the short last chunk, a lost acknowledgement
keeping its chunk missing and the window full (a retry re-offers it — that IS the resume story, no separate
resume handshake), duplicates not applied twice, an out-of-shape chunk refused, an incomplete file not
assembled, and a file whose hash does not match never handed over.

**File over the wire, loaded per client.** The wire carries the FILE's bytes, not a decoded buffer: every
listener writes the same bytes to disk and decodes locally, so the transfer has one integrity value and the
player's own decode path is the only variable. Chunk size: 16 KB by default, capped at 24 KB — the mod packet's
binary leaf carries at most 32 KB (`ModValueCodec.MaxBinaryBytes`), and a chunk stops short of it to leave room
for the envelope a mod adds around the payload.

**Resume and loss.** The sender keeps a window (8 chunks) of unacknowledged chunks; a lost chunk simply stays
outstanding and is offered again, and a receiver that restarted asks for `Missing()`. Nothing is re-negotiated,
and a stalled peer costs its own window, never the session.

**Integrity.** One SHA-256 of the whole file, announced with the identity. The receiver only hands the bytes
over when the assembled hash matches; a corrupted song never plays.

**The start rule (stated explicitly, as the ticket demands).** Playback starts only after EVERY in-world member
has acknowledged the whole file — "everyone has it, now play" — because the user's constraint is transfer first,
then play. A member joining mid-song receives the file as a normal transfer and starts at the CURRENT offset,
computed from the shared start instant and the track's position, rather than at zero.

**Rate.** The sender's window is the in-flight bound; the mod additionally spaces its sends (one chunk per
configurable interval) so gameplay traffic keeps priority, and the transfer's own byte count is reported next to
the existing traffic accounting.

**Audio format, staged.** Stage A plays WAV (PCM) through `AudioClip.Create` + `SetData`, needing no decoder.
Its decode half landed in the same capability (`BulkTransfer.Core/Audio/WavFile`): RIFF/WAVE PCM (8- or 16-bit)
and IEEE float 32, mono or stereo, with the pad byte between chunks respected, and everything else refused with
its reason — a compressed payload behind a WAV header, a truncated chunk, an unsupported channel count — so a
corrupted transfer is never turned into noise; seven tests pin it.
MP3/OGG need a decoder dependency or the game's own runtime audio loader, and that choice is Stage B; the
ticket's "MP3 and the like" is therefore partly a later stage rather than a silent omission.

**What still needs the user before shipping:** the listener controls (mute, volume, "only during rest") — a
player-facing decision the ticket already names as the user's. A mod-owned panel would draw through `IModUi`;
the position and the interaction are the user's call, so none is built yet.

## Stage A's local half landed (2026-10-24) — the player plays a WAV

`src/CasualtiesUnknownOnline.Music/` is the satellite mod: a BepInEx shell, the `[CuoMod]` entry and a local
console verb. It binds no game assembly at all (the engine's audio API plus `BulkTransfer.Core` and the
abstractions), and `MusicDevice` decodes a WAV through the capability's decoder into
`AudioClip.Create` + `SetData` on a hidden `AudioSource`, so a file on this machine's disk is audible without a
session.

**Read on a real client** (the console driven in-process):

| command | answer |
|---|---|
| `/music status` | `not playing.` |
| `/music play <a 2 s 440 Hz test tone>` | `playing <path> (2.0s).` and the log line `playing test-tone.wav: 2.0s at 44100 Hz, 1 channel(s).` |
| `/music status` (a moment later) | `playing (0.5s of 2.0s).` — the clip really advances |
| `/music stop` | `stopped.` |
| `/music play <missing path>` | `refused: no file at …` |
| `/music play <a text file>` | `refused: not playable: the file is not a RIFF/WAVE container` |

What remains: the SHARING half — the mod's declared packets (`music.announce`/`music.chunk`/`music.ack`), the
transfer driving those packets per receiver, the shared start instant and the late-joiner offset, and then the
two-client row (a listener hears the same track at the same offset). The listener controls stay the user's
decision.

**Where the files come from:** the player's own disk, picked through the mod's own path field (the game has no
file picker for this); nothing is shipped with the mod and nothing is uploaded beyond the session's own peers.
