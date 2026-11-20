# Step 0 — the working mode

The frame of every round; [why.md](why.md) holds what measured these checks.

## The round opens here

- First act: `tools/act.ps1 -Step 0`; it opens the record and prints this file in full. A step printed before it is
  void; a print that stops before the last line was truncated and is repeated before anything else.
- A turn ends only when: the item is finished; the user must decide (question tool); a destructive or other-affecting
  action needs confirmation; or a subagent runs.
- Read a background job back with `job_output(wait: true)` in the same step; an unread job is discarded.

## Between those ends: tool calls

- No progress reports, no summaries of what was read, no lists of what remains. Test: if what was last written describes
  a result and no call follows, delete it and make the call.
- Answering the user does not end the turn: answer, then continue with a tool call. Never name the budget or end a step
  on it.

## Purpose before depth

- Rules are boundaries, not maximization targets: purpose and impact set depth. A clear path runs as one chain; an
  uncertain or high-risk link is split and verified; a question gets an answer, not a work item.
- When the next step would be a guess, repeat evidence held, or cost more than it returns, say what is known and what
  decision is needed, then hand back - not the budget stop, nor a licence to leave work unfinished.

## One work item, one chain

- Read → judge → change → build → gates → suite → commit → deploy → verify on the running game: one chain, no report
  between links.
- A red gate, compile error, dirty tree, long conversation or hard fixture is a reason to continue, never to stop - and
  never a licence to revert, weaken a check or declare done. Progress is a judged row, a fixed defect, a moved ticket or
  a green gate; writing a document is not, and a direct answer is when the question is the task.

## Reading the rules

- Read the step you are about to work in, both files, in full, at entry - every round; a head, tail, grep or piped
  print is not a read. Enter it with `-Intent` (the plan and the rule that binds it) or the printer refuses.
- A user correction is a rule change: land it where it is read, in checkable words, and sweep every file that states or
  contradicts it - in the same round.

## The documents outrank the plan

- Before touching a surface, read the page that owns it: the design corpus, the ticket, the recipe or tool contract.
  Where a plan, habit or tool disagrees, the page wins and the plan is rewritten.
- The read names the entry, mechanism or contract before the first staged input or edit; work built without it is not
  evidence and is redone from the page. Check every name the page states against the game's own data; a missing fact is
  read before work starts, never filled from the prior.
