# Step 8 — verify on the real thing

Read this file and `08-verify.local.md` completely before any verification claim.

## The order

Focused test or gate first, then the full suite once, then `dotnet format`, then commit, then deploy, then verify the
deployed artefact's identity, then run it. A claim made before that order is a guess.

## Green tests are not acceptance

Behaviour only a real two-client session can confirm is judged from that session. Acceptance is agent-run: every row is
judged from evidence the agent produced, and a row nobody can judge is a named residual, never a weaker check.

## Visual rows need a frame

Look at the picture. A row judged from a flag or a field is not judged. A batch with no frame records that batch as
failed for that row.

**A full-window capture is not the picture.** It is a 1280-pixel-wide photograph of everything, and the detail a layout row
turns on is a few pixels of it: cropping the region and enlarging it is the difference between judging and guessing. So a
visual row is judged from a CROP of the thing under test, enlarged enough to see its edges, and — where the question is a
POSITION, a WIDTH or an ALIGNMENT — from a measured number beside the crop (the rects the code drew, a pixel column found in
the crop, a HotRepl reading of the same field). The capture alone, at full size, is never the evidence.

Measured 2026-11-02: a console list was declared aligned from a full-window frame, twice, and was wrong both times — the
second time because the button style''s own margin still moved the rows a few pixels. The crop showed it in one look; the
measurement (`rowInkX=330` against `inputTextX=328`) said by how much.

## Report what was measured, with the number

"Works" is not a reading. A count, a frame, a log line with its values. And a count question is answered by the counter
that increments per event, never by a list that deduplicates.
