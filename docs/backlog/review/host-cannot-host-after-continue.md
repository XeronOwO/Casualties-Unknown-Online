# A host cannot start a session after entering a world

- Status: Review — blocked on a product decision. A host who continues a world first and hosts second is refused
  on 1.0 IP direct (`IP-direct start refused: a world is running or generating`), and the world has already loaded in
  the host's own key space, so a later session cannot adopt the restored characters (`Continue restored world … with
  damage: the snapshot was written in the IpDirect key space and this session runs Steam, so none of its 2 stored
  character(s) was claimed (decision 162)`). The decision to make: when the player picks Continue as the host, create
  the lobby/IP session first automatically, or allow a session to start inside a running world. The reading is
  `docs/evidence/acceptance/adapt-to-1.0-ip-direct-20261109.md` and the layer-end section of
  `docs/backlog/in-progress/adapt-to-1.0.md`.
- Priority: High
- Category: Session / save
- Source: found while reading the layer-end continue on the sandboxed 1.0 session (2026-11-10).

## What happens

The natural player order is: open the game, press Continue, then invite a friend. On the release build that order
produces a session that cannot be started (IP direct refuses while a world runs) or a world whose characters were
claimed in the wrong key space (Steam instead of IpDirect), so every stored character is dropped as unclaimed.

The order that works today is: create the session at the menu, then continue inside it — the archive's characters are
claimed and a member joins the restored layer (read 2026-11-10). Nothing in the UI tells the player this order.

## Options

1. **Auto-session on Continue**: when the host continues from the Worlds page (or the main menu's Continue is routed
   through CUO), start the configured transport first and continue inside it. One player action, the archive's key
   space always matches.
2. **Allow hosting a running world**: lift the IP-direct guard and make the session adopt the loaded world; the late
   joiner path already exists for a session that started before the run, but adopting a world that started without a
   session touches every stream's start-up, so it is the larger change.

Recommendation: option 1 — it matches the player's words ("continue my world"), keeps the session's key space and the
archive's in agreement, and needs no stream re-init.

## The reading that filed this

- Layer-end cut: `Cut LayerAdvance committed for world w-20261110-5938 … revision 845, layer 1 …`.
- Working order: IP session at the menu, then the continue → `Restored characters: 1 bound to present peers, 1 left
  unclaimed, 0 refused (key space IpDirect)`; the member rejoined the restored layer.
- Failing order: continue first, then `ip.create_host` → `IP-direct start refused: a world is running or generating`;
  the restore had already run in the Steam key space, so `none … was claimed`.

## Design

The order rule comes from docs/design/cross-cutting/saves-and-recovery.md (an unclaimed character joins as a new one;
claims bind only present peers in the session's key space) and the 1.0 save flow in docs/design/game/console-and-save.md.
A session created before the continue keeps the archive's key space and the players' claims; a session created after cannot.
