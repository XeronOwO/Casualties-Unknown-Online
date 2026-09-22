# How to do one thing

[Documentation](../README.md) > How to

---

One task per page, in `docs/en/how-to/`: what you want to do, the steps, a runnable example, the
traps, and how to check that it worked. Every page assumes you have read [Your first mod](../start/your-first-mod.md).

- [Send a message to the other players](send-a-network-message.md) — the mod network: who may send
  what, and what happens to a message that never arrives.
- [Declare permissions and host commands](declare-permissions-and-commands.md) — the eight flags, and
  a command that runs on the host.
- [Add a panel to the interface](add-a-ui-panel.md) — a local window that changes nothing by itself.
- [Read game state](read-game-state.md) — what CUO already knows about another player, and how fresh
  that reading is.
- [Register content](register-content.md) — an item, a recipe, a tile: content the world treats as its
  own.
- [Save mod data across sessions](save-mod-data-across-sessions.md) — a mod's own small table, written
  by the host and read back on the next run.
- [Decide which side judges an action](decide-which-side-judges-an-action.md) — whose client decides,
  and what the host keeps.

The exact contracts behind these pages are in `../reference/`; the reasoning behind them is in
`../internals/`.

---

[Documentation](../README.md) > How to
