# Remote backpack item projection acceptance issues (projection rework needed)

- Status: Todo
- Priority: High
- Category: Remote inventory / item projection / container sync
- Parent: `docs/backlog/review/remote-backpack-native-interaction-parity.md`
- Source: User acceptance findings (2026-09-06) on the remote-backpack review ticket. Record only; no code change in this cycle.

## Context

During acceptance of the "Remote backpack native interaction parity" review
ticket, the user found multiple issues in the remote item projection layer.
The user's judgment: the projection mechanism was not designed for these
interactions and feels fundamentally weak; it needs a thorough audit or a
complete rework rather than more point patches.

## Reported issues

1. **Durability projection mismatch**
   - Host holds metal scrap at 75% durability.
   - Guest opens the host's backpack and sees the same metal scrap as 100%
     durability.
   - The remote projection does not carry the current item durability/health
     state.

2. **Only some item types can be moved into the host's trash bag**
   - Guest cannot put the host's water bottle, dog food, or lantern into the
     host's trash bag.
   - Guest can put metal scrap into the same trash bag.
   - After putting metal scrap in, the guest sees 75% durability correctly, and
     can also take it back out.
   - This suggests per-item-type state/projection or native container acceptance
     is incomplete, not a single missing move path.

3. **Double-Tab transfer to the guest's own inventory fails**
   - Guest cannot take a host item and use the double-Tab transfer gesture to
     move it to the guest's own body/inventory.

4. **Pour and edge-drop operations fail on the host's water bottle**
   - Guest cannot pour out the host's water bottle.
   - Guest cannot drop the host's water bottle from the backpack to the world
     edge.
   - The operation map claims pour/edge drop are implemented, but the accepted
     behavior is not present for this item/role path.

## Likely scope

- Remote item projection state (durability, container membership, slots,
  interactions) needs to be checked as a family, not as four isolated fixes.
- The four symptoms may share a common projection root: remote proxies carry
  too little authoritative item state and/or the native projection is not
  reconstructed faithfully enough for non-trivial item behaviors.
- A deep audit or redesign of the remote backpack projection is expected before
  acceptance can pass.

## Acceptance matrix placeholder

| Scenario | Expected | Current observed |
|---|---|---|
| Host holds 75% metal scrap; guest opens host backpack | guest sees 75% | guest sees 100% |
| Guest moves host water bottle into host trash bag | succeeds | fails |
| Guest moves host dog food into host trash bag | succeeds | fails |
| Guest moves host lantern into host trash bag | succeeds | fails |
| Guest moves host metal scrap into host trash bag | succeeds; 75% shown; removable | succeeds |
| Guest double-Tab transfers a host item to self | succeeds | fails |
| Guest pours out host water bottle | succeeds | fails |
| Guest edge-drops host water bottle | succeeds | fails |

## Non-goals

- No code change in this cycle; this ticket is a record.
- Not proposing a specific new projection design yet.
