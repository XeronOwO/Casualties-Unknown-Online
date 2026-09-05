# Trap destruction drops desync in item quantity between host and guest

- Status: Todo
- Priority: Medium
- Category: Item sync / entity destruction drops
- Source: User report (2026-09-05) — host destroys the support block under a jump-pad trap; the two sides see different drop sets for a while, then periodic sync restores the count.

## Observed symptom

- The host destroys a support block, breaking a jump-pad trap.
- Host view shows the dropped items as one circuit board and two metal scraps.
- Guest view initially shows only one circuit board.
- Later the guest's missing items are restored by the periodic/authoritative
  sync, so the total eventually matches.

## Assessment

This is a destruction-drop quantity/identity desync: the guest is missing some
drops from the same entity/trap destruction event and only catches up through a
later periodic correction. That is a surface symptom of an event/projection or
materialization path that does not deliver the complete drop set immediately.

## Investigation direction

- Compare the host-side destruction capture with the guest-side materialization
  for the same jump-pad/trap event:
  - the full `EntityEventMsg.Drops` list at the event source,
  - the kernel/projection path for trap/building death drops,
  - the guest replay/materialization path and whether it creates every listed
    item or silently drops unknown/missing entries.
- Determine why the guest sees one item instead of three:
  - missing events/ordering, duplicate id suppression, unknown item id rejection,
    a state-stream snapshot that only carries a subset, or a race between the
    entity-event replay and the kernel projection.
- Check whether the periodic catch-up is the existing full-table item snapshot
  or a separate correction; the goal is to remove the visible gap before the
  periodic sync, not to rely on it.
- Cross-check with `todo/entity-destruction-drop-guest-fresh-state-loss.md`: the
  same scenario also has fresh-drop presentation rejection on the guest.

## Non-goals

- Not accepting "periodic sync eventually fixes it" as a completed fix.
- Not treating the missing item as purely cosmetic if the guest materialization
  path is the cause.
