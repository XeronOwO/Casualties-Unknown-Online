# The behaviours a step is judged by

Read this before acting, and again whenever a tool result tempts a shortcut. It is the action-level form of the rules in
the step files: the same obligations, short enough to hold in mind while working. `tools/act.ps1` prints it at every step
entry, after the rules, so the last thing in view before the work is what the work will be held to.

1. Before the first `src/` edit: write the chain with file:line (origin → caller → scope → how it reaches the player),
   and close every client first. No chain, no edit; no closed clients, no edit.
2. Entering a step: state `-Intent` (the plan and the rule that binds it) before the rules print; read both files whole.
3. A rule print is never piped, filtered or truncated. A cut print is an unread step; reprint it.
4. Never ask what to advance. Pick the next item and start. Ask only for a hand motion, a product decision, or a
   destructive step.
5. Talk to the player in plain Chinese, in the things they see: never indices, slot numbers or internal ids. The three
   clients are 主机, Steam1, Steam2.
6. A row only the player's hand can read: give one exact instruction through the question tool. Never drive the user's
   mouse, and never leave a virtual input pressed.
7. A checklist box is a confirmed observation. Read the gate, the log line or the number before ticking it; never tick
   for ceremony.
8. A correction from the user preempts the step that is running: do the corrected thing first, then write the rule, in
   the same turn.
9. Committed content is English. A Chinese quote is translated and marked "(translated)".
10. One work item, one chain: build → gates → suite → format → commit → deploy → verify on the real game. No report
    between the links, and no stopping on a red gate.
