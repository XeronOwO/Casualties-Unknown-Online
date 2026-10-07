# The off-screen arrow's label escapes the screen edge

- Status: Todo
- Priority: Low
- Category: Online UI / world overlay
- Source: the user's 2026-10-07 backlog request — on the host's view, a member off the right edge is marked by
  an arrow, and the label under it (the name and the distance in metres) is long enough that a part of it
  runs past the right border. The user also asks for the other screen edges to be checked.
- Related: `src/CasualtiesUnknownOnline.Runtime/OnlineUi/OffScreenArrowGeometry.cs` (the edge placement),
  `src/CasualtiesUnknownOnline.Runtime/OnlineUi/OnlineUiWorldMarker.cs` (the marker and its `OffScreenText`
  label), `src/CasualtiesUnknownOnline.GameAdapter/OnlineUi/OnlineUiWorldOverlayView.cs` (the drawing),
  `src/CasualtiesUnknownOnline.Runtime/OnlineUi/NameplateLayout.cs` (the above-head box, which has the same
  edge problem in its on-screen form), `review/online-ui-layout-and-input-detail-pass.md`

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
