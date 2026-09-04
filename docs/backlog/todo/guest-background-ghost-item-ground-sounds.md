# Guest background window plays ghost item friction/ground sounds

- Status: Todo
- Priority: Medium
- Category: Guest item physics / audio / background focus
- Source: User report (2026-09-04) — when the guest game is switched to the background, intermittent item friction/ground-drop sounds are heard; after switching back to the foreground the sound disappears. User recalls a possible earlier mention; current backlog search did not find an existing matching ticket. Treat this as a new record / reopen if an older historical ticket is located. Record only; no code action taken yet.

## Goal

Eliminate the spurious item friction/ground sounds that a backgrounded guest client hears. In the foreground the same sound is not present, and it should not depend on window focus.

## Current behavior / likely context

- CUO deliberately keeps the game running while the window loses focus:
  - `src/CasualtiesUnknownOnline.Plugin/Plugin.cs:191-192` — `Application.runInBackground = true`.
- Guest world items are simulated locally and corrected by the host:
  - `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemPositionFollow.cs` — guest copies run local physics (ground-layer only), then the host's 10 Hz item position stream soft-corrects or snaps them.
  - `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemMotionState.cs` — settled/threshold rules.
- When the window goes to background, frame/audio/physics timing can change while the game continues to run. If the guest's local non-authoritative item copies drift or run their native contact/ground logic, they can produce sounds that are not present in the foreground and are not part of the host's authoritative presentation.

## Investigation needed

1. Identify the exact sound source:
   - likely a native item/ground or item-item contact/random-step clip from a guest-local world item copy, rather than a `CharacterSoundMsg` or dedicated synced event.
   - confirm whether the sound belongs to an item that is supposed to be settled/kinematic, or to an item the host has already moved/removed.
2. Determine why foreground vs background differs:
   - does the guest's `ItemPositionFollow` receive fewer host corrections in background (timing, frame rate, network processing cadence) and let local copies diverge enough to play contact/ground sounds?
   - does Unity's `AudioListener`/physics/`FixedUpdate` cadence change while unfocused, causing sounds that are not audible in the foreground?
   - is there any existing focus-aware code path? Search found none beyond `Application.runInBackground`.
3. Verify whether this is only a guest-side local simulation artifact or whether the host would also produce the sound if the same motion were visible.
4. Review the guest item collision/layer isolation and any native audio triggers on `Item`/`Limb`/container objects that should be suppressed for non-authoritative guest copies.

## Required design direction (for the implementation cycle)

- Avoid blanket audio muting while backgrounded; the session should continue running and normal cooperative audio should not be lost.
- Either:
  - suppress non-authoritative guest-local world-item contact/random-step sounds (the guest item copies are not the simulation authority), or
  - align/freeze the guest local copies while unfocused so they do not drift into audible states, or
  - fix the underlying item-position correction/background cadence issue so local copies never become audible on their own.
- Keep the existing star/host-authority model intact; do not make guest item physics authoritative.

## Acceptance criteria (for the later implementation cycle)

- Backgrounding the guest no longer produces intermittent item friction/ground sounds that are absent in the foreground.
- After returning to foreground, behavior matches the pre-background state; no lingering audio.
- Normal host-authoritative sounds and the existing item position stream continue to work in background mode.
- No wire/protocol/save authority change unless demonstrated necessary.
- Existing tests, gates, and runtime-focused evidence cover the background/foreground transition.

## Non-goals

- Not blocking multiplayer from running while the window is in the background.
- Not implementing in this cycle — this ticket is a backlog record only.
