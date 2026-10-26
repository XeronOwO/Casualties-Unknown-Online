# Member menu: what a teammate offers

The menu a player opens on a teammate, and the actions it offers.

## What the player does

Right-clicks a teammate's body in the world and reads a short menu: open their medical view, open their backpack, and the
carry actions the game offers for that body. The player expects the menu to name the player it will act on and to offer
only actions that apply to that player's current state.

## What it is

A small panel drawn at the pointer, listing the actions for one player. It is CUO's own panel (the game has no such
menu), and its entries are the entries of the corresponding surfaces: the medical view, the backpack view, and the
carry actions.

## Rules and states

- The menu acts on ONE player, decided when it opens: the body under the pointer at that moment.
- The title names that player, and an unidentified player is named by an identity form rather than by an empty title.
- An action that does not apply to the current state is not offered: a medical action on a body with nothing to treat, a
  carry action on a body already carried.
- A player who is not in the world cannot be the menu's target; the menu closes instead of acting on a stale target.
- The menu closes on a pick, on a click elsewhere, and when its target leaves.
- **A world item under the pointer owns the press; the menu does not open on it.** The game's own order is the standard:
  `PlayerCamera.TryPickupFromWorld` raycasts the `Item` layer FIRST on a right-press (`PlayerCamera.cs:1423-1441`) and returns with that item as the
  current drag, so an item under the pointer already has an owner for the gesture. CUO asked only "is a teammate within 48 px of the pointer"
  (`OnlineUiPlayerContextMenu.TryFindRemoteCandidatesAt` → `RemoteTargetPicker.Find`), so right-clicking an item that happened to lie on a teammate
  started the pickup AND opened the menu beside it (user report, 2026-10-25). The menu now asks the same question first, through the narrow `IWorldItemPointerQuery` seam (`GameAdapter.IsWorldItemUnderPointer` asks BOTH shapes
  the game itself accepts: a `Physics2D.OverlapPoint` on the `Item` layer, and an `EventSystem.RaycastAll` hit carrying an `ItemLabel` — the modifier
  overlay draws nearby items as those labels, and the game's own press branch reads them before any collider), with the pointer converted from GUI
  space, and stays closed when the answer is yes. A bare body keeps opening the menu; so does the empty ground.- **Overlap is resolved by asking, not by taking the first.** `src/CasualtiesUnknownOnline.Runtime/OnlineUi/RemoteTargetPicker.cs`
  "returns every candidate inside the click radius ordered by distance, so the context menu can present an explicit selector
  when several remote players overlap on screen" — the earlier behaviour "returned the first matching remote player" — and
  "ties are broken by SteamId so the result is deterministic".
- **A quick panel keeps the target it had while that player is still there.** `QuickPanelTargetPicker.cs`: "the panel keeps
  its previously chosen target while that player is still an in-world remote; when the target is gone or no target was chosen
  yet, the nearest in-world remote wins (ties by SteamId for determinism)" — so the panel does not jump to whoever walked
  closer between two presses.

## Refusals

| Condition | The player reads |
|---|---|
| the target left the world while the menu was open | the menu closes; no action is sent |
| an action is refused after the pick | the status line carries the refusal |

## Who judges

Opening the menu is a client-side decision about what the player is pointing at. Every action the menu offers is judged by
its own surface's rules once picked.

## What travels

Nothing for the menu itself; a picked action travels as that action's own request.

## What the player sees and hears

A compact menu at the pointer, titled with the player's name, with the applicable actions listed.

## Records

`../../architecture/surfaces.md` (player-mounted body view row). Accepted:
`../../backlog/done/remote-context-menu-medical-visible-when-target-not-visible.md` (when the medical entry appears).
Open: `../../backlog/review/context-menu-name-wraps-behind-actions.md` (the title's fitting).

## Gaps

- The exact ordering and iconography of the entries, and the keys that open and close the menu, are not recorded here yet.
