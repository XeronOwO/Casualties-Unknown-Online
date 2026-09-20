# Patch bridge: per-domain ports with the aggregate frozen

- Status: Todo
- Priority: Medium
- Category: Architecture / adapter seam
- Source: Loomi architecture review (2026-09-20), item 4
- Related: `todo/adapter-capability-ports.md`

## Problem (evidence)

`src/CasualtiesUnknownOnline.GameAdapter/IPatchBridge.cs` is 554 lines with 94 member declarations,
and `src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs` is 569 lines. Every Harmony patch
class in the tree (113 files carrying `[HarmonyPatch]`) reads the same static seam. The design is
deliberate — patches never touch the global container — but the shape has four costs:

- any domain change edits the central interface;
- a version difference cannot replace one domain's behaviour;
- test doubles grow with the whole surface;
- optional capability cannot be expressed, because everything is one contract.

## Goal

Per-domain ports — `IItemPatchPort`, `ICharacterPatchPort`, `IWorldPatchPort`, `IRunPatchPort`,
`IInteractionPatchPort`, `IModContentPatchPort` — installed by the patch unit that owns them
(the install unit arrives with `todo/adapter-capability-catalog.md` stage 3).

**Rejected: the one-shot migration the review's item 4 implies.** Moving 113 patch classes at once
is a mechanical change whose regression surface is every patch in the tree, and no test in the suite
can prove it equivalent; the win (smaller merge conflicts, per-domain replacement) does not require
a big-bang. Do it a domain at a time instead.

**Rule this ticket establishes:** the aggregate interface is frozen. No new member may be added to
`IPatchBridge` / `GameAdapterBridge`; new patch work registers through a domain port.

## Stages

1. Port skeleton plus ONE domain migrated end to end as the template, with the aggregate still
   serving every other domain.
2. One domain per change afterwards, taken when that domain is being touched for its own reasons.

## Acceptance

- A shape gate fails on a new member added to `IPatchBridge` or `GameAdapterBridge`.
- The migrated domain's patch contracts and tests are green; unmigrated domains are untouched, and
  the change states which domains remain.
- The port surface is narrower than the aggregate for that domain — a port that carries the whole
  interface under a new name does not count as a split.

## Notes

Grammar note for the implementer: extension methods use the C# 14 `extension` block syntax, and the
patch classes must keep reporting only verified writes (a Prefix that swallows a write must not let
the same Postfix report it).
