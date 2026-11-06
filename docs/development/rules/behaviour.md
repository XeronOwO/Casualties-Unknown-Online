# The behaviours a step is judged by

Read this before acting, and again whenever a tool result tempts a shortcut. It is the action-level form of the rules in
the step files: the same obligations, short enough to hold in mind while working. `tools/act.ps1` prints it at every step
entry, after the rules, so the last thing in view before the work is what the work will be held to.

Before writing anything the user will read, run the five-form test: the item is finished, a decision is needed, a
destructive step needs consent, a hand motion is needed, or the user just asked a question. If none of the five holds,
the next output is a tool call, not words. A message ends the turn, so a mid-item report stops the work.

When the reader says "reflect" (translated), everything in hand stops. Name what was actually done against the rule and
why it happened,
then make the change that prevents it — with every means available, not a promise — and only then resume. (2026-11-07:
a status message mid-item, a step record reused across rounds, a tracked file written by shell — each had a rule that was
already printed.)

1. Before the first `src/` edit: write the chain with file:line (origin → caller → scope → how it reaches the player),
   and close every client first. No chain, no edit; no closed clients, no edit.
2. Entering a step: state `-Intent` (the plan and the rule that binds it) before the rules print; read both files whole.
3. A rule print is never piped, filtered or truncated. A cut print is an unread step; reprint it.
4. Never ask what to advance. Pick the next item and start. Ask only for a hand motion, a product decision, or a
   destructive step.
5. Talk to the player in plain Chinese, in the things they see: never indices, slot numbers or internal ids. The three
   clients are the host desktop, Steam1, Steam2.
6. A row only the player's hand can read: give one exact instruction through the question tool. Never drive the user's
   mouse, and never leave a virtual input pressed.
7. A checklist box is a confirmed observation. Read the gate, the log line or the number before ticking it; never tick
   for ceremony.
8. A correction from the user preempts the step that is running: do the corrected thing first, then write the rule, in
   the same turn.
9. Committed content is English. A Chinese quote is translated and marked "(translated)".
10. One work item, one chain: build → gates → suite → format → commit → deploy → verify on the real game. No report
    between the links, and no stopping on a red gate.
