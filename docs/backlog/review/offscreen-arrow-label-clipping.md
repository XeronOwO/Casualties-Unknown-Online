# The off-screen arrow's label escapes the screen edge

- Status: Review — implemented and gated; the per-edge frames are what is left.
- Priority: Low
- Category: Online UI / world overlay
- Source: the user's 2026-10-07 backlog request — on the host's view, a member off the right edge is marked by
  an arrow, and the label under it (the name and the distance in metres) is long enough that a part of it
  runs past the right border. The user also asks for the other screen edges to be checked.
- Related: `src/CasualtiesUnknownOnline.Runtime/OnlineUi/OffScreenArrowGeometry.cs` (the edge placement),
  `src/CasualtiesUnknownOnline.Runtime/OnlineUi/OnlineUiWorldMarker.cs` (the marker and its `OffScreenText`
  label), `src/CasualtiesUnknownOnline.GameAdapter/OnlineUi/OnlineUiWorldOverlayView.cs` (the drawing),
  `src/CasualtiesUnknownOnline.Runtime/OnlineUi/NameplateLayout.cs` (the above-head box, which has the same
  edge problem in its on-screen form), `todo/online-ui-layout-and-input-detail-pass.md`

## What is observed

The off-screen marker is clamped to the screen edge by `OffScreenArrowGeometry`, and the arrow glyph is fixed
width (`OffScreenArrowText`). The **label** beside/below it is text whose width depends on the player's name
and the distance, so a long name at the right edge overflows. The same shape should be checked on the left,
top and bottom edges, where the label has less room than the arrow itself.

## What is not known yet

- Whether the label box is measured against the canvas at all today, or drawn at a fixed offset from the
  clamped arrow position; and what the canvas width actually is on the user's aspect ratio (the overlay is an
  IMGUI surface over a window that can be resized, so a hard-coded margin would only move the problem).
- Whether the nameplate (the on-screen form, `NameplateLayout.AboveHead`) has the identical defect when a head
  is near an edge — the user reported the arrow's label, but the two share one marker pipeline.

## Required work

1. Make the marker's label a measured box: the label is clamped into the screen rectangle the same way the
   arrow is, per edge, and the clamp is a Runtime rule beside `OffScreenArrowGeometry` so it is unit-testable
   without the game (that is the shape the existing geometry and layout helpers already use).
2. Decide and implement one overflow behaviour for a label that cannot fit at all (shrink, ellipsize, or move
   the label to the opposite side of the arrow); the user's report is about a partial overflow, so the
   behaviour for a total overflow must be chosen rather than left implicit.
3. Apply the same rule to the on-screen nameplate when its head is near an edge, or record why the two differ.
4. Verify with frames on all four edges and with a long name plus a three-digit distance, captured per client
   window (window-level capture, never the desktop), and record the corner cases (a member exactly at a corner
   is two clamps at once).

## Non-goals

- Not a redesign of the indicator: arrow, name and distance stay as they are.
- Not a name-truncation policy for the UI in general; only the marker label's placement is in scope.
## Implemented (2026-10-17) — awaiting the frames

- **Item 1, a measured box.** `OffScreenLabelGeometry.Place(...)` is the Runtime rule the ticket asks for (pure,
  unit-tested without the game, beside `OffScreenArrowGeometry`), and the three call sites now MEASURE their own
  text (`Marker.LabelTextWidth()` → `OnlineUiControlSizing.CaptionTextWidth`, the same engine-side
  `GetPreferredValues` the window family already uses for a caption that ran into its border). The first cut
  handed the rule the old fixed-width constants, which would have left the reported overflow in place for any
  name past ~15 characters; the review caught it before the commit.
- **Item 2, the chosen overflow behaviour.** When a label is wider than the room the screen has, the rule pins
  it to the canvas's centre on that axis, so the overflow is shared by both edges instead of landing on one,
  and the caller writes ONE warning naming the label's width and the canvas's width. Truncating is deliberately
  not the answer here: this overlay's own fit policy is overflow rather than a clip or an ellipsis, because a
  name the player cannot finish reading is worse than one that spills.
- **Item 3, the on-screen nameplate.** The same rule, horizontally. Its vertical axis is deliberately NOT
  clamped: the box above a head cannot leave the top of the screen while the head itself is inside the guarded
  rectangle, and clamping it moved a nameplate down onto its own head (found in review, before the commit).
- **Item 4 is what a run still owes**, which is why this ticket moves to `review/` rather than `done/`: frames
  per edge with a long name plus a three-digit distance, captured per client window, and the corner case (a
  member exactly at a corner is two clamps at once). No frame has been taken yet.

## Limits this cycle names

- The bottom edge is not symmetric with the other three: the arrow's own box legitimately spills past the margin
  line (its CENTRE is pinned there), so its label goes ABOVE it rather than further out. A frame per edge is
  what confirms the reading; the arithmetic is in the rule's and the call site's own comments.
- One rule serves three labels whose HEIGHTS are still the game's own fixed rows; only the widths are measured.
- The view was 633 lines after the first cut and its construction helpers moved out to
  `OnlineUiWorldOverlayParts` (580 + 70). It is still the largest file in that folder, and the next addition
  there should plan a split rather than spend the last 20 lines.