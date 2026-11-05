# Step 0 — the working mode

The frame of every round. The checks below are the rules; what measured them is in [why.md](why.md), opened when a rule
seems fussy or is about to change.

## The round opens here

- The round's first act is `tools/act.ps1 -Step 0`. It opens this round's step record and prints this file; a step
  printed before it belongs to an earlier round and is void.
- A turn ends only when: the item is finished; the user must decide (ask with the question tool); a destructive or
  other-affecting action needs confirmation; or a subagent is running.
- A background job is read back inside the same step with `job_output(wait: true)`; ending a turn with it unread throws
  its result away.

## Between those ends: tool calls

- No progress reports, no "here is where I am", no summary of what was just read, no list of what remains.
- The test is mechanical: if the last thing written describes what a call returned and no call follows, that message is
  the violation - delete it and make the call.
- Answering the user does not end the turn: answer, then continue with a tool call.
- The budget is never an input: do not name it, do not write "out of budget" or "out of context", and never end a step
  on one.

## One work item, one chain

- Read → judge → change → build → gates → suite → commit → deploy → verify on the running game: one chain of calls,
  with no report between the links.
- A red gate, a compile error, a dirty tree, a long conversation or a hard fixture is a reason to continue, never to
  stop. A red gate never licenses reverting, weakening a check, or declaring done.

## What counts as progress

- A judged acceptance row, a fixed defect, a moved ticket, a gate that turned green. Recording a finding, writing a
  document or adding a tool is not progress on its own.

## Reading the rules

- Read the step you are about to work in, both files, in full, at the moment you enter it - every round, however
  routine or well remembered. A head, a tail, a grep of headings or a piped print is not a read; a truncated print is
  repeated before anything else.
- The printer records the file's SHA for the commit hook, and a commit whose change touches an unread step is refused.
  The record proves the printer ran, never that the file was read: the reading is the reader's.
- A correction from the user is a rule change: land it where it is read, in words that make it checkable, and sweep
  every other file that states or contradicts it - in the same round, not the next.

## Two self-checks

- Before writing anything that is not one of the ends listed above, the next action must be a tool call.
- The round's first call prints step 0 in full; if the output did not reach the last line, it was truncated.
