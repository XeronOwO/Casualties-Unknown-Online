# Step 0 — the working mode

The frame of every round. The checks below are the rules; what measured them is in [why.md](why.md), opened when a rule
seems fussy.

## The round opens here

- The round's first act is `tools/act.ps1 -Step 0`; it opens this round's record and prints this file. A step
  printed before it belongs to an earlier round and is void.
- A turn ends only when: the item is finished; the user must decide (question tool); a destructive or other-affecting
  action needs confirmation; or a subagent is running.
- Read a background job back with `job_output(wait: true)` in the same step; ending a turn with it unread throws the
  result away.

## Between those ends: tool calls

- No progress reports, no "here is where I am", no summary of what was just read, no list of what remains.
- The test is mechanical: if the last thing written describes a result and no call follows, that is the violation -
  delete it and make the call.
- Answering the user does not end the turn: answer, then continue with a tool call.
- Never name the budget or end a step on it.

## One work item, one chain

- Read → judge → change → build → gates → suite → commit → deploy → verify on the running game: one chain of calls,
  with no report between the links.
- A red gate, a compile error, a dirty tree, a long conversation or a hard fixture is a reason to continue, never to
  stop - and never a licence to revert, weaken a check or declare done.

## What counts as progress

- A judged acceptance row, a fixed defect, a moved ticket, a green gate. Recording a finding or writing a document is
  not progress on its own.

## Reading the rules

- Read the step you are about to work in, both files, in full, at the moment you enter it - every round, however
  routine or well remembered. A head, a tail, a grep of headings or a piped print is not a read; a truncated print is
  repeated before anything else.
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

## Two self-checks

- Before writing anything that is not one of the ends above, the next action must be a tool call.
- The round's first call prints step 0 in full; if the output did not reach the last line, it was truncated.
