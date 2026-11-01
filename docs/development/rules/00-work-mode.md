# Step 0 — the working mode

**Every round starts here.** Read this file and `00-work-mode.local.md` completely before the first tool call, even
if the previous round read them.

## One work item is one continuous stream

A work item runs from reading evidence to a verified result as ONE chain of tool calls: read → judge → change → build →
gates → full suite → commit → deploy → verify on the running game → next item. Between those steps **no report, no
"done so far", no question you can answer yourself**. Text ends a turn only when:

1. the whole objective is done;
2. a decision only the user can make (ask with the question tool);
3. a destructive or other-affecting action needs confirmation;
4. a subagent or background job is still running.

There is no fifth state. Tiredness, length of the conversation, a red gate or a dirty tree are reasons to keep working,
never to stop.

## Budget

The machine's budget is unlimited. "Out of budget" is not an ending, is never written into a rule, and is never
reported. There is no "context exhausted" either: a long conversation is not a stopping condition.

## A red gate is evidence, not a verdict

A red gate, a failing test, a compile error or a dirty tree means the work is not finished YET. It licenses continuing.
It never licenses reverting, weakening a check, or declaring done.

## What counts as progress

A judged acceptance row, a fixed defect, a ticket that moved, a gate that turned green. Recording a finding, writing a
doc, or adding a tool is not progress on its own.

## Every step re-reads its rules

At the start of each step, read that step's two files — `<step>.md` here and `<step>.local.md` beside it — in full.
Not a skim, not from memory: the files, every round. This is the one mechanism that works; skipping it is how a rule
gets broken by someone who knows it.
## One big thing, run to its end

The unit of work is the thing the user asked for, not the smallest step that can be reported. A work item runs until it is
judged, committed, deployed and verified — however many tool calls that takes. Re-reading a rule file costs seconds;
stopping halfway costs the next round re-learning everything and is how the same defect gets discovered twice.