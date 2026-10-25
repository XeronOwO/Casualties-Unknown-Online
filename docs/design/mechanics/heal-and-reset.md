# Heal and reset: putting a player's body back

The console's cheat verb that resets a player's state — health, limbs, moodles — and what it does in co-op.

## What the player does

Types `/heal` with a selector (`@s`, `@p`, `@a`, a name) and expects the named bodies to come back whole: lost health
restored, severed limbs regrown, the hollow state cleared, the death state ended in place rather than by respawning the
party.

## Rules and states

- **Revive happens IN PLACE.** The user's ruling when the verb landed: revive "in place", the trader-revive reading, not
  the next-level respawn's.
- **The host computes the continuous state; the affected client judges each event and applies the host's rows to its own
  body.** That direction is the user's ruling for the health family, and `/heal` follows it: a reset the host decides
  reaches the client that owns the body, which applies it and reports what it applied.
- **A reset must be able to reach the host-owned vitals.** The defect the record closes is that a `/heal` reset "cannot
  restore the host-owned vitals"; the fix is that the same reset path covers them, not a second path.
- **Severed limbs are restored by CUO, because the game has no path for it.** The ticket states the reason: "the game's
  own `heal` command does not restore severed limbs". The restored limb survives a save cut, a continue-run restore and a
  process restart, because the limb is a persisted body fact.
- **The hollow state is cleared through the native heal path.** Clearing it removes the `MindwipeScript` component and its
  canvas overlay, so nothing of the hollow presentation is left behind.
- A reset reports what it applied, and the applied result is what the body holds afterwards: an applied result naming a
  value the reset table does not write is refused whole.

## Refusals

| Condition | The player reads |
|---|---|
| the selector matches nobody | the line names the selector |
| the request names several bodies where one is required | the line names the count |
| the applied result contradicts the reset table | the result is refused and the snapshot stays |

## Who judges

The host computes the reset and the continuous state; the body's owner applies it to its own body and reports the applied
result, which the host stores through the authority path. A member may run the verb only with the cheat grant (see
`../cross-cutting/permissions-and-commands.md`).

## What travels

The selector's expansion, the reset the host decides, and the applied result the owner reports back.

## What the player sees and hears

The body comes back: health and limbs restored, the hollow overlay gone, the revived player standing where they fell.

## Records

Decision 184 (the affected side judges). Accepted: `../../backlog/done/heal-cheat-command-state-reset.md` (all six rows),
`done/heal-command-limb-and-hollow-restore.md`, `done/heal-restores-severed-limbs.md` ("the game's own `heal` command does
not restore severed limbs"; the restored limb survived a `/save` cut, a continue-run restore and a process restart),
`done/heal-reset-does-not-restore-host-owned-vitals.md`, and `done/health-simulation-and-host-authority.md` for the
direction quoted above.

## Gaps

- The reset table's exact fields and their clamps are held by the implementation and its acceptance records rather than
  stated here yet.
- What a reset does to a body that is being carried is not recorded here yet.
