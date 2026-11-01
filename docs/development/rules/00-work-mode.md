# Step 0 — the working mode

## No intermediate reporting. Ever.

A turn ends in one of three states only:

1. **the item is finished**;
2. **the user must decide** — a destructive action needing confirmation is one of these, not a separate state: it is still
   a decision, put to the user through the question tool;
3. **a subagent is running** — that is the only asynchronous thing a turn may end on, because its completion notice is the
   result channel.

A background JOB is not that: it is read back inside the same step with `job_output(wait: true)`. Ending a turn with a job
still running leaves its result unread and makes the goal fire early, so waiting on it is the work, not a pause.

Everything else is a tool call. A progress message, a
"here is where I am", a summary of what was just read, a list of what remains — all of them are the same violation: they
hand the work back unfinished.

**Substantial work is not an end state.** Landing one real result — a fix, a measurement, a ticket — is the most common
moment to stop, and it is not one of the three. The test is mechanical: **the turn's last action must be a tool call whose
result you have not yet described.** If you are about to describe what the last call returned, you are ending the turn, and
unless the item is finished you are breaking this rule. Measured 2026-11-02: this failed nine times in one session, every
time right after a result worth reporting, and every time the next round had to re-learn where the work stood. The reader does not want the middle, they want the result; an intermediate result is not a
result. If a sentence would not change what the user does next, it must not be written — and after deleting it, the next
action is a tool call, not a shorter sentence about why.

Measured 2026-11-02: a batch was driven for hours and reported after every two or three calls; every message was a stop,
and the work only advanced between them.
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

## The first call of every round reads both files WHOLE

The round''s first tool call is a `read` on `<step>.md` and on `<step>.local.md`, in full, and the transcript shows each
file''s LAST line together with its total line count. That is the only accepted shape, because it is the only one that cannot
be faked:

- a `Get-Content … | Select-Object -First N`, a head, a tail, or a grep of the headings is NOT a read — it is a quote of the
  rule, and a quote is what someone who already knows the rule writes. Measured 2026-11-02: many consecutive rounds printed
  three lines of a 104-line step file, then cited that file correctly. The read never happened.
- if the first call did anything else, the round has not started: stop, make the read, and only then touch the repository.
- if the read''s output does not reach the last line, it was truncated: repeat it before anything else.

Acting on the rules is the other half. A round that prints the files and then does what the files forbid has still failed;
the two failures are measured separately and both are failures.
## Read the step's rules at the MOMENT you step into it - work is forbidden until then

The read is not a once-a-round ritual: it is the gate in front of EVERY step. The instant work moves from one step to the
next - framing to design, design to implementation, implementation to verification, verification to self-check, self-check
to commit, commit to acceptance - the FIRST action of the new step is to read that step's two files, in full, and the
transcript shows it. Editing, building, verifying, committing or driving the game inside a step whose rules have not been
read THIS round is forbidden work: what it produces is not evidence, and it is redone.

A round that spans several steps prints several files. That is the cost of doing the work, and it is small next to doing it
wrong. A step file that has never been opened is not being followed; it is being guessed at.

Measured 2026-11-02, twice in one round: the working-mode file was read at the round's start and the source was then edited
before steps 2, 4, 5, 6 and 7 had been opened, and later a catalogue was edited on the strength of a read taken before the
edit rather than before the step. The direction is exact: every step forward reads that step's rules first, and work
without them is forbidden.
## Every step re-reads its rules

At the start of each step, read that step's two files — `<step>.md` here and `<step>.local.md` beside it — IN FULL.
Not a skim, not from memory, and not a piece: a `Select-Object -First N`, a head, a tail, or a grep of the headings is NOT a
read. The transcript has to show the file's LAST line, because that is the only proof the whole file was printed. This is the
one mechanism that works; skipping it is how a rule gets broken by someone who knows it.

Measured 2026-11-02: the step-0 read was `Get-Content ... | Select-Object -First 3` for many consecutive rounds — three lines
of a 104-line file — while the same rounds quoted this very paragraph correctly. The rule was known and not obeyed, which is
exactly what "the files, every round" exists to prevent.
## One big thing, run to its end

The unit of work is the thing the user asked for, not the smallest step that can be reported. A work item runs until it is
judged, committed, deployed and verified — however many tool calls that takes. Re-reading a rule file costs seconds;
stopping halfway costs the next round re-learning everything and is how the same defect gets discovered twice.
## A correction is a rule change, and it lands in the whole family

When the user points at a defect — in the work, or in how the work is being done — the round writes the RULE, not an
acknowledgement. Two obligations, both in the same round:

1. **Land it where it is read.** The rule goes into the file that decides the behaviour, in words that make it
   checkable. "I understand", "I will be more careful", and an assurance with no diff are the same non-delivery.
2. **Sweep the family.** The same discipline usually lives in several files (this step, the user-level instructions, the
   step that owns the area, the ticket's own limits). Every one of them gets the sentence or a pointer to it. A rule that
   exists in one place and is contradicted by another is worse than no rule, because the next round obeys whichever it
   reads first.

**Be proactive inside the rule, too.** When a correction names a pattern, fix the pattern everywhere it appears in the
same round: the other tickets with the same shape, the other probes making the same assumption, the other step files
saying the opposite. Waiting to be told a second time is the failure this section exists for.

Measured 2026-11-02: the same correction ("do not stop between steps") was acknowledged in prose three times and each
round still ended mid-item — because the rule had been written once, narrowly, in one section, and no round ever asked
which files decide whether a round may end.
## Two self-checks that catch the two ways this step is actually broken

Both failures are measured, not hypothetical: a round that reported after two calls, and a round that worked from memory
without reading its step files.

1. **Before writing any message that is not one of the four end states, the next action must be a tool call.** The test is
   mechanical: if the item is not finished and no tool call follows this sentence, the message IS the violation — delete it
   and make the call. A "progress" message and a stop are the same act.
2. **The round''s first call prints the current step''s two files, IN FULL.** Reading them is not a memory task: the call
   prints them, and the transcript shows the LAST line of each — a `-First N` head does not count and is the measured way this
   check was faked. A round whose first call did anything else has not started yet.
## The budget is never an input

Do not name the budget, the context, the round count, or "running out" of anything — not in a report, not in a plan, not as
the reason a step ended, not as a reason to record something quickly. There is no upper limit to spend, so a sentence about
one is either false or a disguised stop, and it is the stop that is the violation: delete the sentence, and the next action
is a tool call. Measured 2026-11-02: a batch narration said it was nearly out of context and then wrapped the work up on
that basis; the wrap was the damage, the sentence was the evidence.
