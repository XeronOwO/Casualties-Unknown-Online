# Guest-mined item static-physics desync

- Status: Todo
- Type: Bug
- Category: Items / world sync
- Source: user report 2026-08-31

For an item dug out by a guest:
- The white outline disappears earlier on the guest than on the host, and the
  item appears without gravity/falling behavior on the guest.
- After the host simulation reaches its static/optimized state, the guest's
  copy of the item keeps twitching and does not enter the static/optimized
  state.

Related to the earlier item duplicate/unsynced-drop issue:
`docs/backlog/todo/guest-tree-extra-unsynced-drops.md`.
