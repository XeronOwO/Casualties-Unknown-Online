# The native console is an unpoliced authority hole

- Status: Done — user-reported (2026-11-08): running the game's own `heal` on Steam2 reset every state; the host now
  refuses a limb report that carries a change the game's own rates cannot produce (2026-11-08). Batch `20261108-t2`:
  Steam2 ran the game's own `heal` (its local limb read 100/100 after a seeded -12.2); the host refused the report
  (`[LimbEvent] refused SkinJumped … the host's record stays.`) and kept its record. The console-side half (refusing a
  state-mutating native command on a client in a session) remains the second lever named below.
- Priority: High (a member can rewrite its own authoritative facts)
- Category: Session authority / console
- Source: the user, 2026-11-08 (translated: "the native console commands have no permission check, right? I ran heal on
  Steam2 and it reset all states — neither the client frontend nor the host arbitration validates, and there is no
  automatic data correction?")
- Related: `docs/decisions/active.md` decision 162 (a member's copy of an authoritative fact is synced, never trusted),
  `done/restore-leaves-stale-carried-rows.md` (the same trust boundary), `docs/architecture/member-report-validation.md`
  (the report-bound census the fix must extend)

## Design

The surface is the member's body report and the host's record of it: `docs/design/mechanics/heal-and-reset.md` (what a
heal is and who owns it), `docs/design/mechanics/medical-view.md` (the treatment facts that change a limb) and
`docs/design/surfaces/command-overlay.md` (the console surface). The fix lives on the report bound the census already
names, not in a new anti-cheat model.

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

## The chain (read 2026-11-08)

The hole is explicit in the code. `src/CasualtiesUnknownOnline.Runtime/Session/Handlers/LimbStateEventHandler.cs`
says, in its own doc: "Accept-first: the host adopts the reported state unconditionally (the body belongs to the
reporter's own local simulation)". The handler checks exactly two things: the reporter may not name another owner
(`msg.OwnerSteamId != sender` → dropped) and the numbers must be finite
(`CharacterDataStore.ApplyLimbStateEvent` refuses non-finite events). Then the host stores the reported limbs, fires
the adapter event (the reporter's clone gets the new state) and relays the frame to every guest.

So a guest's native `heal` becomes a full-stamina limb report, the host adopts it as the owner's truth and fans it
out: the member rewrote the world's idea of its own body, and nothing names the cause. The same accept-first shape
carries every field the report holds (health, each limb's condition, blood, infection, statuses).

## The design to confirm

The host validates the transition against its own last record of that owner, using the game's own rules as the
allowed set:

1. **A value may not jump beyond what the game can produce in the elapsed time**: healing, bleeding, infection and
   pain have native rates; a step larger than the rate × elapsed frames (plus a margin) is refused as unexplained.
2. **A limb state may not appear or disappear without the fact that causes it**: a severed limb cannot reappear, a
   fracture cannot vanish, an infection cannot clear, a new wound status cannot appear — unless the host itself
   applied the treatment fact (the bandage splint tourniquet amputation paths already publish their own facts).
3. **Refusal is a correction, not silence**: the host keeps its own value, tells the reporter (a status line naming
   the unexplained change), and relays the corrected state, so every client's copy ends at the host's value.
4. The legitimate case stays: the owner's own simulation produces ordinary deltas (bleeding, healing, the game's own
   treatments), which pass rule 1; the host's own `/heal` and the game's own treatment facts are host-originated and
   carry their cause.

The native console half (refusing state commands on a client in a session, or correcting them) is the second lever;
the validator above is the one that must exist regardless, because the console is not the only writer.

## The reading (2026-11-08)

`LimbStatePlausibility` now runs in `CharacterEventMerges.ApplyLimbStateEvent` before the merge: a dismembered limb
cannot return, skin or muscle cannot rise by more than 40 in one event, and infection cannot fall by more than 40; a
refused report changes nothing and the host logs the reason. Batch `20261108-t2` (deployed `0.1.0+036d5d7e`): Steam2's
limb 0 was seeded to `skinHealth -12.2`; the acceptance recipe invoked the game console's own `heal` delegate
(`tools/acceptance/recipes/native-heal.cs`, `DynamicInvoke` on the command's stored delegate — typing into the native
console window is not a drive path), and Steam2's local limb then read `skinHealth 100, muscleHealth 100`. The host
refused the report twice over:

    [Warning] [CharacterDataStore] [LimbEvent] refused SkinJumped from 76561199526807662: the report carries a change
    the game's own rates cannot produce in one event; the host's record stays.
    [Warning] [LimbStateEventHandler] [LimbEvent] refused an implausible owner event from 76561199526807662 for
    76561199526807662; the host's record stays.

So the guest's local heal no longer becomes the world's truth: the host keeps its record. Limits: the recipe invokes the
console's own delegate rather than typing into its window; the status line on the member's own screen is the next lever
(the refusal is logged, not yet drawn). The other native verbs (`kill`, `spawn`, the item and block commands) reach the
host through their own facts; the same bound must be read for each when their reports are next touched.
