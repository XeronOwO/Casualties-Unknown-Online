# Player-facing surfaces: the design map

The architecture documents say how the system is BUILT. This page says what a player can touch, which design governs
each surface, and where its accepted record lives — so a change starts from the design that was agreed, not from a few
source files that happen to compile.

It exists because of a measured failure: on 2026-10-24/25 a family of changes was made by reading the code that was at
hand, and every one of them was wrong in the same way — a cross-player item was handed to the game's use call as a
stand-in, a give replicated a transfer without the native path, a console behaviour was judged against the GAME's
console instead of the Minecraft-shaped design this project accepted, and a selector change broke 32 behaviour tests
because nobody asked what the selector family means. The design was recorded the whole time. What was missing was one
page that says *which* record governs *which* surface, and a rule that the page is read before work starts.

## How to use it

Before a work item touches a surface below:

1. Read the row. It names the **design authority** — the thing the behaviour is judged against — and the **record**.
2. Write the comprehension statement into the ticket (the checklist refuses the commit without it): the surface, the
   authority, the native call site or recorded design the row names, and what the change must NOT alter.
3. If the row says `GAP`, the first work item on that surface is to read and record the authority, not to change the
   behaviour. A `GAP` row is a legitimate answer and must be closed rather than guessed.

## The map

| Surface | What the player does | Design authority | Native counterpart / entry | Accepted record |
|---|---|---|---|---|
| Console (command + chat overlay) | `/`, types, Enter/Tab, ESC | **Minecraft template** (rule 17): `@a` = all players including the executor, `@s` executor, `@p` nearest, empty Enter closes | CUO's own IMGUI overlay; the game's own console is only the host surface | `docs/backlog/done/in-game-command-console-interactive.md` (batch `20261018-t`); decisions 228, 264, 269 |
| Online UI window + panels | opens the window, pages, member actions | **Native-UI reuse** (rule 8): reuse the game's controls; one action table per frame | `CommandConsoleOverlay`-free: `OnlineUi*` views on the game's canvas | `docs/backlog/done/online-ui-art-and-controls-overhaul.md`; decision 230 |
| World drag gesture (item on a player) | holds the right button, drags an item onto a player, releases | **Native parity** (`docs/development/native-parity.md`): the release IS a native call site | `PlayerCamera.TryPerformRadialAction` (`body.UseItem` / `WearWearable`) and the drag bracket | `docs/backlog/done/give-gesture-own-ui-and-center-use.md`, `done/drink-measurement-destructive-use-action.md`; decision 184 |
| Inventory give/take (backpacks) | moves an item to/from another player's displayed pack | **Native parity**: the native container/pickup path, not a CUO copy of its effects | `Container.LoadItem` / `Body.PickUpItem` / `Container.UnloadItem` | `docs/architecture/remote-inventory-native-parity.md`; `docs/backlog/todo/give-into-backpack-plays-no-sound.md` |
| Cross-player item use (eat, drink, wear, limb tool, topical, injection) | releases an item on a teammate's use circle | **Native parity**: the game's own `useAction` on the affected side's client | `Body.UseItem` → `Stats.useAction` (`Item.cs` delegates) | `docs/backlog/todo/cross-player-eat-skips-native-steps.md` (the design error named there) |
| Medical view / wound view | opens the wound view, applies items per limb | **Native parity**: the game's own limb action | `PlayerCamera.TryPerformSpecialUIAction`'s `WoundViewLimb` branch | `docs/backlog/done/` medical family; decision 246 |
| Player-mounted body view (backpack/medical of a member) | right-click a member, picks an action | **Native-UI reuse**: the member menu is CUO's, the actions it offers are native | `OnlineUiPlayerContextMenu` → native calls | `docs/backlog/todo/context-menu-name-wraps-behind-actions.md` (layout), `done/` member-menu design |
| Status lines / notices / refusals | reads a line after an action | **Localization**: every player-visible string rendered by the receiver in its own language | `ISessionNotices` → HUD line; wire carries KEYS | `docs/backlog/todo/player-facing-text-localized-per-receiver.md`; `done/console-command-replies-bilingual.md` |
| Selectors (`@a`/`@s`/`@p`/`@r`/`@e`, filters) | types a selector into a cheat verb | **Minecraft template** (rule 17) | `CommandSelectorResolver` (CUO's own, Unity-free) | `docs/backlog/todo/selectors-exclude-the-executor.md` (the 32-test fallout named there) |
| World overlays (nameplates, network readout, pings) | sees labels in the world | **Native UI** on the game's canvas (decision 232) | game canvas labels | decision 232 |

Rows marked `GAP` do not exist yet; every other row names a record that exists in the tree today. When a row's record
is a ticket in `todo/`, the ticket is the authority for the CORRECTION and the accepted record for the behaviour is the
one it cites.

## What this page does not replace

- `docs/architecture/current.md`, `domains.md`, `protocol.md` — how the system is built.
- `docs/decisions/active.md` — the numbered rulings; this page links them, it never restates them.
- The `done/` ticket of a surface — its rejection list is the surface's specification, and a regression against it is a
  regression, not a new design.

## Related reading

- `docs/development/native-parity.md` — the three lines and the parity probe for native-mirroring behaviour.
- `docs/evidence/delivery-checklist.md` — the boxes that enforce this page before a commit.
- `docs/decisions/active.md` — the numbered decisions the table cites.
- `docs/backlog/README.md` — the tickets each row names.
