# Step 0 — the working mode

The frame of every round. The checks are the rules; [why.md](why.md) holds what measured them.

## The round opens here

- The round's first act is `tools/act.ps1 -Step 0`; it opens the record and prints this file. A step printed before it
  is void.
- A turn ends only when: the item is finished; the user must decide (question tool); a destructive or other-affecting
  action needs confirmation; or a subagent is running.
- Read a background job back with `job_output(wait: true)` in the same step; an unread job is discarded.

## Between those ends: tool calls

- No progress reports, no summaries of what was just read, no lists of what remains.
- The test is mechanical: if the last thing written describes a result and no call follows, that is the violation -
  delete it and make the call.
- Answering the user does not end the turn: answer, then continue with a tool call.
- Never name the budget or end a step on it.

## Purpose before depth

- These rules are boundaries, not maximization targets. The user's purpose and the change's impact set how far each one
  runs: a clear path goes through as one chain; an uncertain or high-risk link is split and verified before the next step;
  a question gets an answer, not a work item.
- When the next step would be a guess, would repeat evidence already held, or costs more than it can return, say what is
  known and what decision is needed and hand back. That is not the budget stop, and it is not a licence to leave work
  unfinished.

## One work item, one chain

- Read → judge → change → build → gates → suite → commit → deploy → verify on the running game: one chain of calls,
  with no report between the links.
- A red gate, a compile error, a dirty tree, a long conversation or a hard fixture is a reason to continue, never to
  stop - and never a licence to revert, weaken a check or declare done. The chain is sized by the work: a clear link
  runs; an uncertain link is split and verified; it never expands into repeated evidence or unrelated cleanup.

## What counts as progress

- A judged acceptance row, a fixed defect, a moved ticket, a green gate. Recording a finding or writing a document is
  not progress on its own. A direct answer to the user's question is progress when the question is the task.

## Reading the rules

- Read the step you are about to work in, both files, in full, at the moment you enter it - every round. A head, a
  tail, a grep of headings or a piped print is not a read; a truncated print is repeated before anything else.
- A step is entered with `-Intent "<the plan and the rule that binds it>"`; without it the printer refuses. The plan is
  stated before the rules print and recorded with them.
- The printer records the file's SHA for the commit hook; a commit whose change touches an unread step is refused.
- A correction from the user is a rule change: land it where it is read, in checkable words, and sweep every file that
  states or contradicts it - in the same round.

## The documents outrank the plan

- Before touching a surface, read the page that owns it: the design corpus (`docs/design/...`), the ticket, the recipe
  or tool contract. Where a plan, a habit or a tool disagrees with the page, the page wins and the plan is rewritten.
- The read names the entry, the mechanism or the contract before the first staged input or edit; work built without it
  is not evidence and is redone from the page.

## Reading is verification
- Check every name a page states against the game's own data; a missing fact is read before work starts, never filled
  from the prior.

## Two self-checks

- Before writing anything that is not one of the ends above, the next action must be a tool call.
- The round's first call prints step 0 in full; if the output did not reach the last line, it was truncated.
