# Why the step rules read the way they do

The evidence behind the step files. Read it when a rule seems fussy, when a rule is about to change, or when the reason
a check exists is not obvious from the check. Each entry names what was measured and what the measurement changed; the
rule itself lives in its step file, so this page never restates a check.

## The round's opening read (step 0)

- **2026-11-02.** A round read step 0 and then implemented, verified and gated without opening steps 2, 4, 6, 7, 8 and
  11. The violation was not skipping a step the round did not need; it was walking steps unread.
- **2026-11-02, twice in one round.** The source was edited before steps 2, 4, 5, 6 and 7 were opened, and a catalogue
  was later edited on the strength of a read taken before the edit rather than before the step.
- **2026-11-02.** The step-0 read was `Get-Content … | Select-Object -First 3` for many consecutive rounds - three lines
  of a 104-line file - while those same rounds quoted the rule correctly.
- **2026-11-03, by the user.** Every round for more than a day used `tools/step.ps1 -Enter <n> | Select-Object -Last 2`:
  the file was printed and the output thrown away, so step 0 had not actually been read once.
- **2026-11-05.** A commit moved backlog files on the strength of a step record whose own header still read two days
  earlier; the round had printed three steps and the record's older lines satisfied the hook. The record now starts
  over at step 0.
- **2026-11-05.** That same round read all fourteen step files - and broke three of its own rules anyway (a tracked file
  written by a shell round trip, the delivery checklist filled in one catch-up pass, the split landed before its test).
  This is why the size gate exists as well: a long read costs every round and does not by itself buy compliance.

## Ending a turn and reporting (step 0)

- **2026-11-02.** The no-intermediate-reporting rule failed nine times in one session, every time right after a result
  worth reporting, and every time the next round had to re-learn where the work stood.
- **2026-11-02.** A batch was driven for hours and reported after every two or three calls; every message was a stop,
  and the work only advanced between them.
- **2026-11-02, the user's own observation.** A reply followed by tool calls continued the session without
  interruption, while replies treated as an ending cost a whole round each time - so answering does not end the turn.
- **2026-11-02.** A batch narration said it was nearly out of context and then wrapped the work up on that basis; the
  wrap was the damage and the sentence was the evidence.

## A correction is a rule change (step 0)

- **2026-11-02.** The same correction ("do not stop between steps") was acknowledged in prose three times and each
  round still ended mid-item, because the rule had been written once, narrowly, in one file, and no round asked which
  files decide whether a round may end.

## The design frame (step 2)

- **2026-11-02.** A trader revive was implemented as "heal the body where it lies". The operator revived a teammate at
  the trader and the teammate stood up where the operator could not see them. The follow-up fix was then written as
  "add two floats and set the position" - the same mistake one layer down: the position was right and the reasoning was
  still the code's.
- **2026-11-02.** The amputation acceptance fixture was built by trial for hours; the infection gate, the coordinate
  flip, the moving cutting band and "progress is the hand's SPEED" were all already written at
  `docs/design/mechanics/medical-view.md:77-82`. A fixture that ignores the corpus reports numbers that mean nothing.

## The acceptance batch (step 13)

- **2026-11-02.** A hover stopped moving the wound view's selection after a heal, and the clients were killed and
  relaunched twice to get a fresh view - no source change, no deploy. The fresh view was available from the game's own
  close/open, and the two restarts cost the session that was being measured.
- **2026-11-02.** A recorder file was added to the acceptance project while two clients were mid-batch, and the deploy
  that followed refused because the file it had to overwrite was held open.
- The batch procedure itself is `docs/acceptance/workflow.md`; its own measured pitfalls (the `plays`-not-`names`
  count, the one-line recipe body, the settings menu that must exist before a tab is selected) live there, where a run
  reads them.
