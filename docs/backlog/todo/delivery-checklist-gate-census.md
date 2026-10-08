# The delivery-checklist gate reads no census, so an emptied checklist passes it

- Status: Todo
- Priority: Low
- Category: Repository gates (process-record integrity)
- Source: the independent NARROWED adversarial review of the entry-mapping record cycle (2026-10-08),
  finding n4 — a gate-robustness nit, filed rather than fixed because it is a `tests/` change.
- Related: `docs/evidence/delivery-checklist.md`, `docs/en/contributing/gates-and-rules.md`
  ("Writing a new gate": keep a census floor), `docs/evidence/normative-gates.md`

## The gap

`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` walks the checklist's lines, fails on
every `- [ ]` box that is neither the deployment line nor the FORBIDDEN line, and fails a checked
FORBIDDEN line. It never asserts how many boxes the file declares, so a checklist that lost its boxes —
a bad edit, a "clean-up" that dropped the cycle's required steps — passes the gate by having nothing
left to fail on. The gate is what makes the checklist binding, so its own scan surface should fail when
the checklist stops being a checklist.

## What a fix has to decide

- **What the floor counts.** The boxes DECLARED (`- [ ]` plus `- [x]` lines), never the checked ones: the
  boxes are legitimately unchecked while a cycle runs, and the gate has to keep failing for exactly that
  state rather than passing an empty file.
- **Whether a count is enough.** A floor over the box count catches deletion but not reordering or
  replacement; a stronger check enumerates the required items by a stable token and reports the missing
  one by name. The stronger shape needs the item names to be stable across cycles (the current file's
  items are rewritten by every cycle's evidence suffix, so the check must key on something that is not
  the suffix).
- **The gate's own contract cases.** The matcher needs its positive and negative samples (a checklist
  whose boxes were deleted fails; the standing file with a cycle's boxes still unchecked passes; a
  checked FORBIDDEN still fails), and the fix must not turn the legitimate mid-cycle red into a second
  failure mode.
- **Where the number lives.** A hard-coded floor drifts the moment a cycle adds a box (the entry-mapping
  box arrived that way); the fix should say whether the floor is the measured count or the required
  item tokens.
