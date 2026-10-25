# Drag cue: the use circle and its label

The hover state that tells a player what releasing a dragged item will do to a teammate.

## What the player does

Holds the right button on an item, drags it over a teammate, and reads the cue: the use circle at the centre of the
displayed body, and the label naming the player the gesture will act on.

## What it is

A cue drawn while an item is dragged in the world: the circle marks the use target, its highlight marks that a release
here would act, and the label names the player whose body the release will use the item on.

## Rules and states

- A cue is shown only when the release would actually act: the item is usable on a player, the pointer is inside the use
  centre, and the operator's own eligibility passes.
- The label names the player the release would pick — the one whose range the cursor entered last — and uses the
  player's display name, falling back to an identity form when no name is known.
- **Naming is the point, and the target is the DISPLAYED backpack's owner.** `src/CasualtiesUnknownOnline.GameAdapter/Items/DragUseCuePolicy.cs`
  gives both reasons: "two players standing on each other cannot be told apart by a highlight, so a named label is what makes
  a release a decision instead of a guess" (user ruling 2026-10-06), and the target "is the player whose backpack is DISPLAYED,
  passed only while the pointer is on its centre — the use/wear target" (user ruling 2026-10-23), so "a release on a slot is a
  give and shows no use label, and the detection range no longer names a use at all". The cue also "appears only for an item
  the family gate ACCEPTS … so the cue itself teaches what can be handed over".
- The cue's state and its drawing are one fact: a cue reported as shown but not drawn is a defect, not a rendering
  detail.
- A cue never promises an action the release would refuse; the refusal is the status line's job.

## Refusals it expresses

None of its own: the cue's absence is the refusal signal, and the release's refusal is reported on the status line.

## Who judges

The operator's own client decides whether a cue is shown and what it says; nothing about the target's body is decided
here.

## What travels

Nothing: the cue is computed on the operator's client from state it already holds.

## What the player sees and hears

The use circle highlights under the pointer and the label names the teammate, so the operator knows what releasing will
do before releasing.

## Records

`../../architecture/surfaces.md` (world drag row). Open:
`../../backlog/review/cross-player-drag-use-feedback.md` (the cue's own acceptance rows).

## Gaps

- The highlight's rendering is not yet confirmed on a frame; the review ticket above carries the open rows.
