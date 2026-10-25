# Backpack view: another player's pack on your screen

Opening a teammate's backpack, looking inside it, and closing it.

## What the player does

Right-clicks a teammate (or drags an item onto them) to open their backpack on their own screen, looks through the pack's
contents — the body's slots and any container inside it — and closes it by the same means or by walking away. The player
expects to see exactly what that teammate carries, live, without touching it unless the host's rules allow a hand-over.

## What the game itself does

The displayed pack is the game's own inventory view, rebuilt from the viewed body's state: the slots the body carries and
the contents of any container it holds. The items shown are VIEWS of the teammate's items, not the viewer's.

## Rules and states

- The view is read-only unless the host's rule allows remote manipulation; a view-only pack accepts neither a give nor a
  take, and the gesture is refused on the viewer's own client before anything is sent.
- The pack follows its owner: when the owner drops or moves an item, the viewer's pack changes with it, because every
  client holds the owner's state as state, not as a copy taken when the pack opened.
- A pack that closes because the owner left the world or died closes the operations that belonged to it.
- The viewer's own drag gesture keeps working while the pack is open, so the same gesture can give into it.

## Refusals

| Condition | The player reads |
|---|---|
| the host rule forbids remote inventory manipulation | the refusal names the rule |
| the pack is view-only | the gesture is refused before the wire |
| the item has no identity | the gesture is refused before the wire |
| the pack's owner left the world | the pack closes and the operation is abandoned |

## Who judges

The pack's contents are the owner's state, owned by the host's kernel and projected to every client. A hand-over
attempted from the pack is a request the host validates; a refusal reaches the viewer, not the owner.

## What travels

The owner's inventory state, live, as part of the character data every client holds. A give or a take travels as a
request naming the item, the owner and the destination.

## What the player sees and hears

The pack appears where the game's own inventory view appears, with the game's own slot layout and item icons; the items it
shows change as the owner's inventory changes, with no stale copy.

## Records

`../../architecture/surfaces.md` (inventory give/take row), decisions 155 and 263. Accepted:
`../../backlog/done/remote-backpack-access-policy.md`, `done/local-item-into-remote-display.md`,
`done/tab-backpack-open-close-immediately.md`.

## Gaps

- A container buried inside another container (a pouch inside a pack) has no page and no acceptance record yet.
- Which keys open and close the pack, and whether the same keys work while an item is dragged, is not recorded here yet.
