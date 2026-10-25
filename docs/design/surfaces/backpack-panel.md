# Backpack panel: a teammate's pack as a panel

The panel a teammate's backpack becomes on the viewer's screen.

## What the player does

Opens a teammate's pack, looks through it and closes it. Inside it the player can take an item (if the rules allow it),
give one into it, and open a container that is inside it.

## What it is

A panel shaped like the game's own inventory view, showing the pack's slots and, inside them, any container the pack
holds. Its entries are the viewed body's items, as views of that body's state.

## Rules and states

- The panel shows the OWNER's items: the body's slots and the contents of any container it carries.
- The panel is live: the owner's changes appear without the viewer reopening it.
- A view-only pack accepts no change in either direction, and the gesture is refused before anything is sent.
- The panel closes when its owner leaves the world, and a container opened inside it closes with it.
- Taking from the panel is the taking player's request; giving into it is the giving player's request; both are the host's
  to validate.

## Refusals

| Condition | The player reads |
|---|---|
| the pack is view-only | the backpack notice's gesture refusal |
| the item cannot be named (no identity) | the gesture is refused before the wire |
| the destination no longer holds | the answer names the destination |

## Who judges

The pack's contents are the owner's state, projected to every client; a change is validated by the host and applied to the
owner's side.

## What travels

The owner's inventory state, live; a take or a give as a request naming the item, the owner and the destination.

## What the player sees and hears

The pack with the game's own slot layout and item icons, refreshing as its owner's inventory changes.

## Records

`../../architecture/surfaces.md` (inventory give/take row). Accepted:
`../../backlog/done/remote-backpack-access-policy.md`, `done/tab-backpack-open-close-immediately.md`.

## Gaps

- The panel's exact geometry and the keys that scroll or page it are not recorded here yet.
