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

## Report what was measured, with the number

"Works" is not a reading. A count, a frame, a log line with its values. And a count question is answered by the counter
that increments per event, never by a list that deduplicates.
