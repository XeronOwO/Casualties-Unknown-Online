# A null collection in a content payload must mean "none"

- Status: Todo
- Priority: Medium
- Category: Mod platform / content surface
- Source: found by the independent review of `review/mod-crafting-quality-labels.md` (2026-10-07), which
  asked why an explicitly-null `Qualities` list made the definition fail. The answer is a payload-wide
  pattern, not one field: `DataContractSerializer` runs no property initializer, so a mod that assigns null
  to a list round-trips an explicit nil, and every provider that dereferences such a list without a guard
  turns "I did not set this" into a failed definition.
- Related: `review/mod-crafting-quality-labels.md` (fixed the two sites it touched and records the
  remaining sweep here), `docs/en/reference/mod-api.md` + `docs/zh/reference/mod-api.md` (the DTO tables),
  `docs/backlog/todo/mod-content-ceiling.md` (the surface this belongs to)

## What is asked

One rule for every mod-authored DTO collection: **null means empty**, decided where the payload is decoded,
not by each consumer. Today it is decided per consumer and unevenly.

## Evidence (measured 2026-10-07)

- The trap is real and pinned: `ModItemDefinitionTests.RoundTrip_ExplicitNullQualities_ComesBackAsNull`
  asserts that a definition built with a null `Qualities` list comes back with a null list, which is why
  the two providers that read it normalise it (`definition.Qualities ??= []`).
- Unguarded dereferences that remain (`grep "foreach (var … in definition\." src/CasualtiesUnknownOnline.GameAdapter`):
  `GameAdapterBuildingContentProvider` iterates `DropOnDestroy` and `AlwaysDrop`;
  `GameAdapterStatusContentProvider` iterates `LimbMoodles`; `GameAdapterTileContentProvider` iterates
  `Drops` — and the SAME file already guards the same field elsewhere (`foreach (var drop in definition.Drops ?? [])`),
  which is the unevenness in one screen of code.
- Unguarded `.Count` reads remain on `SpawnComponents` in `GameAdapterItemContentProvider` and
  `GameAdapterBuildingContentProvider` (the log line after a template is built), and
  `CustomComponentAttach.Attach` iterates the same list.
- A failed dereference is not silent: `ModContentBinder` catches what a provider throws and logs it as an
  error, then skips the whole definition — so the outcome is a mod whose content does not exist, for a field
  the mod never set.
- `Qualities` (item and liquid) and `Ingredients` (recipe) are already normalised; they are the shape to
  copy.

## Required work

1. Sweep every DTO in `CasualtiesUnknownOnline.Abstractions` for collection members and decide the rule per
   member: nullable-means-empty (the default), or genuinely required with its own refusal message.
2. Put the rule where the payload is decoded if one seam can own it (a shared normalisation step for the
   typed DTOs), rather than repeating `?? []` per consumer — the point of the ticket is that the decision
   lives in one place.
3. Negative samples in the tests: one per collection member, a definition with that member explicitly null
   must still bind (or must be refused with the message that says why), never with the binder's logged
   exception.

## Non-goals

- Not a general "tolerate anything" pass: a member that is required keeps its refusal, and a member that is
  invalid keeps its validation.
- Not a change to any payload shape: this is about what an absent or nil member means, not about adding or
  removing fields.
