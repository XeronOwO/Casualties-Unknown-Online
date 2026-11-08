# The native console is an unpoliced authority hole

- Status: Review — user-reported (2026-11-08): running the game's own `heal` on Steam2 reset every state; neither the
  guest side nor the host's arbitration refused it, and nothing corrected the member's copy afterwards.
- Priority: High (a member can rewrite its own authoritative facts)
- Category: Session authority / console
- Source: the user, 2026-11-08 (translated: "the native console commands have no permission check, right? I ran heal on
  Steam2 and it reset all states — neither the client frontend nor the host arbitration validates, and there is no
  automatic data correction?")
- Related: `docs/decisions/active.md` decision 162 (a member's copy of an authoritative fact is synced, never trusted),
  `done/restore-leaves-stale-carried-rows.md` (the same trust boundary), `done/character-sync-divergence-report.md`
  (the divergence line this hole did not trip)

## The defect

The game's own console (`reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs`) runs its commands locally, for
the local body: `heal` sets every limb to 100 and the brain/blood to full. CUO gates ITS OWN console verbs
(`CheatAdminCommands`: `/op` grants them to a roster), but the game's console is a different surface and is not gated:
a member runs `heal`, its local limbs become full, and the member's next health report tells the host "my limbs are
these" — the host's character sync treats a member's limb report as an observation and adopts it. The result is exactly
what the user saw: a guest rewrites the world's idea of its own health, and no correction follows.

The same shape applies to every native verb that mutates state CUO mirrors: `kill`, `give`, `spawn`, `setcondition`,
the item and block commands. The set is larger than `heal`.

## Chain to read before designing (the first edit's homework)

- `ConsoleScript`: the command table, how a command reaches the local body, and whether any hook point exists before
  the command runs.
- `CharacterDataSync` (CUO): which member-originated health facts the host applies and where the divergence line is
  computed (`[CharSync] divergence …`) — why a full heal did not trip it.
- `LimbStateEvent`/`CharacterReport` handlers: whether the host validates a member's limb report against the game's
  own rules (a limb cannot go from 12 to 100 between two snapshots without a treatment fact).
- `CheatAdminCommands`: the CUO-side gate this hole bypasses, and what roster data the host already holds.

## Fix direction (to be confirmed)

1. **Host-side validation, not trust**: a member's health/limb report is an observation; the host refuses a jump the
   game's own rules cannot produce in that frame (the same "a member's copy is synced" rule the radiation clock and
   the restore rows already follow) and corrects the member with the authoritative value.
2. **The native console on a client**: either refuse state-mutating native commands while a CUO session is active
   (with a status line saying why), or let them run and let the host correct — decide from the game's chain, not from
   the console's convenience; a console command that only the host's roster may run (the CUO `/op` list) is a third
   option.
3. The CUO console's own gate stays where it is; the two surfaces must agree.

## Acceptance

- On Steam2, run the game's own `heal` in a CUO session: the host refuses or corrects (its log names the fact and the
  correction), and every other client's copy of that member stays as it was.
- A legitimate heal (the host's own `/heal`, or the game's own treatment) still lands.
- The correction is visible on the member's own screen (status line or the corrected limbs), not just in the host's log.
