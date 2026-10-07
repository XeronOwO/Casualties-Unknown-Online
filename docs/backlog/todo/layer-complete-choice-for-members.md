# The end-of-layer choice must be reachable by every member

- Status: Todo
- Priority: High
- Category: Layer progression / session flow
- Source: the user's 2026-10-07 backlog request — finishing a layer offers a save-or-continue choice; they ask
  whether both the host and the guests can see it and click it, so that a dead host cannot leave the guests
  unable to reach the next layer.
- Related: `review/save-layer-end-save-and-restore.md` (S2 — the layer-end save itself),
  `review/save-system-mid-run-and-layer-end.md`, `review/save-mid-run-consistent-cut.md`,
  `todo/layer-change-member-recovery.md` (a member out of the world at the boundary),
  `done/layer-mod-baseline-divergence-on-continue.md` (a Continue that reopened the wrong world)

## What is asked

One requirement with two halves:

1. **Visibility and reachability**: when the end-of-layer choice appears, every member who is in the world
   sees it and can act on it, not only the host.
2. **No host-shaped dead end**: if the host is dead (or otherwise unable to act) at that moment, the remaining
   members can still move the session to the next layer — the run must not be stuck on one client's state.

The user's framing is a stuck-session report: the choice may exist, but if it is only present or only
clickable on the host, a dead host ends the run for everyone.

## What is not known yet

- Which native surface offers the choice (an end-of-layer panel, the elevator, a timer expiry), on which
  client it is created, and whether it already appears on a guest today. The user asks for a check, so the
  first step is a reading, not an implementation.
- What "the host is dead" means for the session's authority at that moment: CUO is host-authoritative, so a
  guest continuing the layer is a decision about who may drive a world-generation step, not only about UI.
- Whether the save/continue decision is already a CUO-owned message or purely native local UI
  (`review/save-layer-end-save-and-restore.md` owns the save half of that question).

## Required work

1. Attribute first, on three clients: drive a layer to its end with all members alive and read who sees the
   choice, who can act, and what each client logs. Then repeat with the host dead at the boundary.
2. Decide the authority answer with the repository's own rule in hand — the acting side judges its own
   experience and the host arbitrates conflicts — and write it down before implementing: a guest driving the
   next layer is a first-writer-wins arbitration, not a new authority model.
3. Implement the reachable half: the choice appears and works on every in-world member, and the session's
   next-layer step is carried out exactly once when two members act at the same time.
4. Cover the failure paths: a member out of the world at the boundary (`todo/layer-change-member-recovery.md`),
   a member who is dead, and a member who joined late.
5. Verify with the exact scenario the user described: host dead at the end of a layer, two members alive, and
   the group still reaches the next layer.

## Non-goals

- Not a rework of the save system: `review/save-layer-end-save-and-restore.md` owns that.
- Not host migration: the host stays the host; this ticket is about a step of the run not being reachable when
  one client cannot act.
