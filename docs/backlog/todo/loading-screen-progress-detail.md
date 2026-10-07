# The loading screen should say what the session is waiting for

- Status: Todo
- Priority: Low-Medium
- Category: Presentation / session loading
- Source: the user's 2026-10-07 backlog request — reuse the loading screen's own bottom-right text and let it
  describe the process in more detail than it does today: what we are waiting for the host to send, and that
  we are waiting for the other member(s) to finish loading, with seconds.
- Related: `src/CasualtiesUnknownOnline.GameAdapter/Run/StartGateCoordinator.cs` (the gate text CUO writes
  today: `Waiting for {n} player(s) to load… ({seconds}s)` and `Waiting for the host to load…`),
  `src/CasualtiesUnknownOnline.Plugin/OnlineUiOverlay.cs` (the waiting presentation), and the native loading
  screen the user is looking at

## What is asked

The loading screen already has a text area of its own (the user points at the bottom-right line). The ask is
to reuse that surface for the session's own progress instead of a bare or generic string, and to make it
specific enough that a waiting player can tell what is happening:

- which direction we are waiting on (host → guest, or guest → host),
- who is still loading, when it is more than one,
- what we are waiting for in CUO's own terms (a baseline, a run-fact packet, a generation segment),
- elapsed seconds next to it.

The requirement is diagnostic honesty, not decoration: a player stuck on a black window should be able to
read what the session is waiting for from the screen alone.

## What is not known yet

- Which native surface that bottom-right line is, whether it is free text or a resource string, and whether
  it survives scene-load transitions (CUO's own waiting overlay exists today, so the two must not fight).
- Which of CUO's waiting states are already exposed as a message and which would have to be named first;
  `StartGateCoordinator`'s two strings are today's whole vocabulary.
- Whether the text should be localized: CUO has a locale catalogue, and a new line of player-visible text
  belongs in it.

## Required work

1. Attribute first: find the native loading-screen text surface, how it is set, and when it is visible
   relative to CUO's own waiting overlay; write down which of the two the player saw.
2. Name the waiting states in one place (a small state → text projection) so the screen, the Online UI and
   the log all describe the same state, and a new waiting state cannot be added without a line.
3. Report the wait as a duration, not only as a condition: elapsed seconds, and the member list when the wait
   is per member.
4. Verify on three clients with an artificially slow member: the waiting side reads who and what it waits
   for, and the text clears when the wait ends.

## Non-goals

- Not a new loading UI: reuse the screen the game already shows.
- Not a progress bar for an unmeasurable step: if a step cannot report progress, the text says what it is
  rather than inventing a percentage.
