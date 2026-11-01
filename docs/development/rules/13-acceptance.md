# Step 13 — run an acceptance batch on the real game

Read this file and `13-acceptance.local.md` completely before starting a batch.

## One session, many rows

Start the clients once and drive every row the batch set out to judge. A batch that judged no row is a failed batch: the
missing capability is built first, then a second batch runs. Two batches on the same row without a reading means the
problem is in the code, not in the batch.

## Do not restart for convenience

The clients stay up for the whole work item. They are stopped for exactly two reasons: a source change, or a deploy. A
declaration or recipe change is re-declared under a new name and the same session continues.

**A stuck fixture is not a third reason.** A UI state that will not re-open, a hover that stops moving the selection, a value
that looks stale - none of them licenses killing the client, because the restart throws away the very session the reading was
about and it hides the defect behind a fresh process. The game's own state paths are the way out (close and re-open the view
through its own toggle, re-enter the world, run the fixture on another client that is still clean), and when none of them
reaches it, the stuck state is what the batch RECORDS: "the fixture stopped at X for reason Y" is a reading, and it is the next
batch's starting point.

Measured 2026-11-02: a hover stopped moving the wound view's selection after a heal, and the clients were killed and relaunched
twice to get a fresh view - no source change, no deploy, exactly the convenience restart this section forbids. The fresh view it
bought was available from the game itself (the view's own close/open), and the two restarts cost the session that was being
measured.

## Every row judged from its own evidence

Machine rows from a reading, visual rows from a CROP that was actually opened and enlarged (a full-window capture is not evidence for a layout row: crop the region, and for a position or alignment read a measured number beside it — see step 8). The instrument is named in the batch
scope before it runs, so a row cannot be quietly judged by a weaker check afterwards.

## A fixture is DERIVED from the corpus and the code, never discovered by trial

Before staging anything for a game mechanic, read what the project already knows about it: `docs/design/mechanics/`,
`docs/design/surfaces/` and `docs/design/items/` carry the mechanic, the UI it lives in and the item that drives it, each with the
game's own file and line. Then read that code yourself. Only then write the fixture, and put the mechanic's input contract in the
ticket before the first staged input: what the player's gesture is, which value it feeds, what gates it.

Measured 2026-11-02: the amputation fixture was built by trial for hours — the infection gate, the coordinate flip, the moving
cutting band and the "progress is the hand's SPEED" line were all rediscovered one experiment at a time, and every one of them was
already written down at `docs/design/mechanics/medical-view.md:77-82`. A fixture that ignores the corpus does not just cost time;
it reports numbers that mean nothing (a hand moving at 0.7 units a frame was described as "a ten-minute cut" for a gesture the
game finishes in seconds).
## A blocked row never becomes the whole cycle

At most two batches per row, then the next ticket. The block is recorded with its reading, not as a story.

## The two ways to drive the game, and which one needs permission

They are not interchangeable, and only the second one is restricted.

**1. In-process driving — the default, and what most rows need.** The batch asks the client to run a recipe: call the
game's own entry point, or answer the game's own input query from a staged value (`VirtualInput`: staged keys and a
staged pointer, patched into `Input.GetMouseButton*`, and call-site-replaced inside `PlayerCamera.HandleInput`,
`MinigameBase.UpdateHandVisuals` and `SettingsMenu.WaitForKeyPress`). Nothing outside the client is touched: the user's
keyboard, mouse and focus are unaffected, so this can run while the user works.

- **Extending this channel is ordinary tooling work. Build it when a row needs it; no permission is needed.**
- The one rule that does apply: the game's own logic still decides what the input MEANS. A staged value answers a
  query; it never replaces the decision the game makes after reading it (the keybind capture binds the key it read, the
  recipe never binds it for the game).

## Close the clients before touching `src/` — the batch channel is not exempt

Every change under `src/` waits until the game processes read zero, and that includes the batch-only assembly. It is easy
to think otherwise ("it is only the acceptance plug-in"), and doing it anyway leaves a running client holding the file the
next deploy must overwrite — the deploy then refuses and the batch is dead. Measured 2026-11-02: a new recorder file was
added to the acceptance project while two clients were mid-batch.

**2. OS-level input — needs the user's authorisation, per batch.** `tools/acceptance/send-input.ps1` and anything like it
(`SendInput`, cursor warps, synthetic key events) takes over the user's real keyboard and mouse: it steals focus, moves
their cursor and types into whatever window is focused. Ask for it in the batch that uses it, only after in-process
driving provably cannot reach the row, and say in the batch scope why.

So the restriction is on the second kind alone, and it is a restriction on taking it without asking — never on
automation. A batch that frees the user's hands by staying in-process is exactly what this channel is for.
## Reading a job is the work, not a pause

`run_in_background` is for a command that outlives a single call (a long build, a batch run), and the step that started it
finishes by READING it: `job_output(wait: true)` on that job id before anything else. A turn that ends with the job still
unread throws the result away and lets the goal fire on a step that was never finished.

A subagent is the one thing a turn may end on: its completion notice is the result channel, and polling it is the
violation, not waiting for it.