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
## What landed (2026-10-18), and what an adversarial review changed

The wait is now a value before it is a sentence: `SessionWaitState` (a kind, the names being waited on, and
the seconds) is built by `StartGateCoordinator.WaitingState`, and `SessionWaitText` is the ONE place it
becomes the line the screen shows. Six cases pin the wording, and the projection's switch has no silent
fall-through, so a wait kind with no line fails loudly instead of being worded as some other stage.

**The review's most valuable finding was that the first wording asserted facts the printing client does not
have**, in exactly the scenario step 4 verifies:

- `"Everyone is here — starting the run."` was reached on a MEMBER whenever no third member was in its
  presence table — and a third member arrives there ALREADY in-world, so a member waiting while someone else
  generates read that it was all done. It now says `"Waiting for the host to start the run…"`, which is what
  that client can observe.
- `"…it is still generating the layer."` claimed the host's phase from a flag (`InWorld`) that is false for
  "in the menu", "still loading" and "no report seen yet" alike. It now says `"The host is not in the world
  yet."` The anchor that would let it name the phase is a host-authored "generating" fact on the wire, which
  does not exist.

The review also caught that the host's roster named members the GATE is not waiting for: the gate arms on
`Handshaken && !InWorld` while the line listed every `!InWorld` member, so an unconfirmed peer appeared on
screen beside a promise ("the group starts together") that was not true for them. The roster now uses the
gate's own predicate.

## Limits this stage leaves

- **Step 1 is not answered, and the code aims at the other surface.** The game's own bottom-right line is
  `WorldGeneration.loadingText` (`SetLoadingText` writes it; the field is declared in the same decompiled
  file) and CUO never writes it — the wait line is CUO's own IMGUI panel, positioned to imitate that slot,
  and it draws OVER a native string that the game left holding its last generation phase. Writing the wait
  into the native `TextMeshProUGUI` would satisfy this ticket's own words ("reuse the loading screen's own
  bottom-right text") where a parallel panel satisfies CUO's; that is a user-visible choice about which
  surface the player reads, so it is recorded here rather than taken.
- **The composition is now tested, and the seam is the fix.** The review's point was that both blockers lived in
  the getter while only the wording had cases. The decision moved out of the coordinator into
  `SessionWaitPolicy.Decide`, which takes `SessionWaitInput` — plain values, so a case can drive the two
  scenarios that failed (a member whose table holds no third player, which is the ordinary three-client case, and
  a member whose host flag is false) plus the roster the host prints. Seven cases, and
  `StartGateCoordinator.WaitingState` now only gathers the facts.
- **The Game Adapter now grants its internals to the test assembly**, which opens 671 internal type
  declarations across 482 files to reach three pure types. The method is precedented in this repository, but
  the `Runtime` grant was reviewed with a census and a recorded reason; this one has a comment. The
  established shape for reaching adapter internals is reflective lookup, and two comments claiming the suite
  never compile-references the adapter are now stale.
- **The log does not read the same state.** The ticket's step 2 wants the screen, the Online UI and the log to
  agree; the log still prints its own sentence, so they can disagree.
- **The new sentences are not in the locale catalogue.** Recorded rather than decided.

**Step 4 ran (2026-10-18)**: the record is `docs/evidence/acceptance/loading-screen-wait-text-20261018.md`.
Two clients on the deployed artifact read the overlay's own line, and all three of the claims the review
faulted the first wording for now hold live: a member is told what IT waits for (`Waiting for the host to
start the run…`) and nothing about the host's phase, the last stage claims no arrival, and the host names
rather than counts. What the run did not reach is recorded there too: the NAMED form of the host's line (the
member was already in the world by the time it was read, so the roster was empty and the fallback showed), and
whether the longer lines fit the panel. The batch's own first attempt is kept in the record because it read the
OLD wording off a stale deployment and its verification step said exactly why — a batch that reads a deployed
artifact has to stop on a failed verification rather than log it.

## Decision point (2026-10-24) — waiting on the user

Two surfaces can show the wait, and the ticket's own words ("reuse the loading screen's own bottom-right
text") point at the native one:

1. **Write the native `WorldGeneration.loadingText`** (recommended: it is the surface the user named, and it
   stops the screen drawing CUO's panel over a stale native phase). It needs an adapter binding for that
   `TextMeshProUGUI` and a rule for when the game's own phase text owns the line again.
2. **Keep CUO's own IMGUI panel** (today's implementation): no new binding, but it draws over the native string
   and does not satisfy "reuse the loading screen's own text".

The wording itself is already landed and tested; only the surface is open. No work is started because the
surface is a user-visible choice.