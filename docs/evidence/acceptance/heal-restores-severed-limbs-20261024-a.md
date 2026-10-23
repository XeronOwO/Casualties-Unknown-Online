# Acceptance — a severed limb comes back, survives a save, and the hollow vignette is cleared

- Ticket: `docs/backlog/done/heal-restores-severed-limbs.md`
- Artifact: `0.1.0+1ae6b691`
- Batch: `.acceptance/20261024-c/` (local, gitignored; runs `he` and `vig`)
- Date: 2026-10-24
- Result: **the two owed rows pass** (the restore's own rows were read in 2026-10-18 and are quoted in the
  ticket).

## Save/reconnect survival (run `he`)

One host client. Limb 4 was severed through the latch (the fixture), then the cheat verb ran: `/heal @s`
answered `full heal applied (1 severed limb(s) restored)` and the probe read all fifteen limbs intact.

`/save` wrote a mid-run cut (revision 597, 9 files, the character among them); `leave-world` + `continue-run`
restored the world (`CUO restored world ... nothing was lost.`) and the limbs still read intact. The client was
then RESTARTED and `continue-run` loaded the same save in a fresh process — the limbs still read intact.

## The hollow vignette (run `vig`)

`HollowClear` removes the `MindwipeScript` component AND the overlay it installs (a canvas child whose name
contains `Mindwipe`, the prefab `Special/MindwipeVignette`). The probe staged the fixture — added the component
and a `Mindwipe_fixture` child under `PlayerCamera.main.mainCanvas` — and read:

- before: 19 canvas children, 0 named Mindwipe, no component;
- fixture: 20 children, 1 named Mindwipe, component present;
- after `/heal @s` (`full heal applied (hollow cleared)`): 19 children, 0 named Mindwipe, component gone.

## Limits and substitutions

- The fixture is a direct component + child, not the game's own wipe trigger; the clear's contract is exactly
  the component's presence and the overlay's name, and the heal reported `hollow cleared`.
- The save half ran on the host's own character; a second client's reconnect was not re-driven this batch (the
  earlier 2026-10-18 cross-client rows cover the wire half).
- One run's session was wedged (`/heal` said "not in a world" while the overlay read `inWorld` true); a fresh
  client process ran both rows cleanly. That is an environment observation, not a heal finding, and the ticket's
  own rows were read on the clean run.
