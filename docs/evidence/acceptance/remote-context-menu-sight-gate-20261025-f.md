# Remote context menu: Medical is gated by sight, both directions

- Ticket: `docs/backlog/done/remote-context-menu-medical-visible-when-target-not-visible.md`
- Batch: `20261025-f`, deployed build `0.1.0+7605a140` (both clients report it in the window's build line)
- Instruments: `tools/acceptance/recipes/open-context-menu.cs` (opens the production menu through
  `OnlineUiPlayerContextMenu.Open`) and `tools/acceptance/recipes/capture-frame.cs` (the client's own frame)

## How each state was made

- **Visible**: both clients at the spawn (`0, 506`), each seeing the other's character.
- **Not visible**: the guest was placed at `400, 506` with `body-place`; both windows then showed the other player as a world
  marker at **120 m**, outside sight.

## The readings

| direction | target visible | the menu drew | file |
|---|---|---|---|
| host → guest | yes | the clipped name plus the four member actions (medical / open backpack / piggyback / carry / push) | `menu-visible.png` |
| host → guest | no | the clipped name and no member actions | `menu-hidden.png` |
| guest → host | no | the peer's name and no member actions | `menu-guest-hidden.png` |
| guest → host | yes | the peer's name and the four member actions | `menu-guest-visible.png` |

(The frames are gitignored artifacts under `CUO_FRAME_DIR`; the guest's were copied out of its Sandboxie box.)

## What this proves

- **Medical is not offered when the target is not visible**, in either direction, and it is not left alone as an inconsistent
  single action: the menu is title-only.
- **Medical is offered together with every other remote action when the target is visible**, in either direction — no option is
  missing, which is the other half of the report.
- Both readings come from the same production projection (`OnlineUiMemberProjection`), which is what the fix changed, and both
  clients run the same deployed build.

## Residual

The title clips a long display name at the menu's fixed 240 px width. That is the separate layout decision recorded
on `docs/backlog/done/dead-player-right-click-name-suffix.md` (grow the panel or elide the name), which is the user's call and is
not part of this ticket.
