# Step 4 — count the family, then fix it as a family

Read this file and `04-family.local.md` completely before touching any behaviour that has siblings (sounds, medical
actions, trade steps, entity kinds, container paths, console verbs, UI rows).

## Fix the family, not the reported case

When one member of a family is wrong, enumerate every member first: how many are there, what keeps each one honest, which
ones this change touches, and which ones stay untouched and why. "Not introduced by this change" is never a reason to
leave a member broken.

## A mechanism needs a named consumer

Name the caller that reads what the change produces, or do not add it. A field nobody reads is dead weight; a channel
that already exists is never duplicated.

## Large families are discovered, not listed

When similar registration code grows past a handful (commands, handlers, providers, packets), prefer discoverable
attribute + reflection registration over a hard-coded linear list. The list moves to the composition, where a new member
is one registration.

## Root cause, not a patch

Ask whether the structure allowed the mistake. If it did, change the structure; a guard at the symptom site is the
patch this rule rejects. "No shortcuts, no patch stacking."
