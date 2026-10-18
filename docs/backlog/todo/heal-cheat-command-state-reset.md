# `/heal` — the console cheat command that resets a player's state

- Status: Todo
- Priority: Medium-High
- Category: Commands / cheat verbs / player state
- Source: the user's correction of 2026-10-18, after batch `20261018-t`. The CUO console is an INDEPENDENT
  extension of the game's built-in console: the native `heal` existing does not mean CUO must not have its
  own. The user's design: `/heal` is a cheat command that resets a selected player's state immediately —
  item-free, a cheat verb. The follow-up questions were answered: FULL reset scope (the native limb fields +
  CUO's severed limbs and hollow + KrokMP's ~40 body fields), the CHEAT permission family
  (`AllowMemberCheatVerbs` + the `/op` roster; the host always), and a DEAD target is revived too.
- Related: `review/heal-command-limb-and-hollow-restore.md` (the NATIVE `heal` command and its limb/hollow
  extension — a separate surface), `review/heal-restores-severed-limbs.md` (the limb restore the reset
  reuses), `done/in-game-command-console-interactive.md` (the console the command lives in),
  `review/cheat-permission-and-op-commands.md` (the cheat family's two-sided check),
  `docs/evidence/acceptance/20261018-t-scope.md` (the run that read the old wiring's behaviour)

## The design that was wrong, and the two wrong decisions that followed

The console redo added `/heal <selector>` wired to `PlayerHealService.SendHealRequest` — the remote-medical
ITEM path. Its help text ("Use a carried medical item on the selected player(s)") and its doc comment were
the implementation's own claims, not the user's design. That wiring decided, without asking the user:

- that `/heal` costs a carried medical item;
- that its user must be another player (the `healer == target` silent return);
- that its user must be conscious/alive to heal;
- that it is the remote-medical operation's console front.

Then the run's logs (batch `20261018-t`, `docs/evidence/acceptance/20261018-t-scope.md`) showed the
consequences: `/heal @s` was silently dropped while answering `Sent`, and `/heal @a`/`@p` were refused
because the host was already unconscious. The agent then proposed a SECOND decision the user never made —
that the command be removed as a duplicate — and that is also wrong: the CUO console is its own surface and
the command is wanted. Both decisions were design decisions taken without asking; the rule they violate is
now written down (`AGENTS.md`, *Ask on functional design*: a rejection is a question, not a mandate).

## The design the user confirmed

`/heal <selector>` in CUO's console, as a cheat verb:

- **What it is.** An immediate, item-free reset of the selected players' state. Not the native local-body
  command, not the remote-medical item path: three independent surfaces.
- **Scope: FULL reset.** The native `heal`'s limb fields (`muscleHealth`, `skinHealth`, `boneHealTimer`,
  `dislocationTimer`, `infectionAmount`, `bleedAmount`, `pain`, `shrapnel`, `infected`) and the body's
  `brainHealth`/`clawHealth`; CUO's severed-limb restore and the hollow clear (removing `MindwipeScript`
  and its overlay); plus the ~40 body fields KrokMP's `ResetHealth` writes — blood volume, oxygen, pressure,
  heart rate, viscosity, respiratory rate, stroke, pulmonary embolism, fibrillation, hunger, thirst, septic
  shock, temperature, sickness, consciousness, stamina, energy, happiness, radiation, disfigurement, eyes,
  internal bleeding, hemothorax, trauma, dirtiness, wetness, sleep debt, hearing, adrenaline, venom, claw
  regrow, and the rest of the list read in `review/heal-command-limb-and-hollow-restore.md`.
- **Who may use it.** The cheat family: the host always; a member only with the host's
  `AllowMemberCheatVerbs` switch on AND the member named in the announced `/op` roster, checked on both
  sides exactly as `/give` and `/summon` are (`review/cheat-permission-and-op-commands.md`). A member's
  local refusal names the reason; no request is sent.
- **Targets.** The selector may resolve the local player or another member (the host included). A DEAD
  target is revived by the reset — the revive's exact semantics (alive flag, consciousness, body state and
  where it comes back) are the first thing the implementation settles from the game's own revive paths
  (`ReviveFromTrader` / `ReviveOnNextLevel` and the trader revive), not guessed.
- **Authority and replication.** A reset is a mutation of the TARGET's body, so by the disclosure rule the
  target's own client applies it and reports the resulting state; the host keeps the authoritative copy and
  the result reaches every peer, the same shape the cross-player heal result already uses. The host's own
  target applies locally with no wire round trip.
- **The answer.** The command's synchronous checks (cheat permission, selector resolution, target in world,
  dead/alive, the revive outcome) are reflected in the answer by name; it does not answer `Sent` for a
  request it already refused.

## What done looks like (acceptance rows)

1. Host `/heal <member>`: the member's health, every limb field, the severed limbs, the hollow moodle and
   overlay, and the body's need/vital fields come back to the healthy values, on the member's own client
   and in the host's copy; the member's console/answer reflects what happened.
2. Host `/heal @s` (or a selector resolving the host): the host's own body resets — the old self-target
   silent no-op is gone.
3. A dead member `/heal <member>`: the member is alive again, with the body and the state the revive path
   defines; both clients read the same outcome.
4. Cheat permission, two-sided: with the host's switch on and the member in the roster, the member's
   `/heal <target>` works; with the switch off or the member absent from the roster, the member's local
   answer is a refusal naming the reason and the host's log window carries no request.
5. The remote-medical item heal and the native `heal` command behave as before; `/heal` does not consume an
   item and does not need the caster to be conscious.
6. The reset reaches the peers and survives a save/reconnect (or the row names the part that does not).

## Required work (stages)

1. **S1 — the reset effect and the field list.** One model of the full reset over a live `Body` (the native
   field list, CUO's limb/hollow effects, KrokMP's body fields), applied on the target's own client. Red
   tests on the field list and on the removal direction (the component/overlay clear), plus the revive
   decision from the game's own revive paths.
2. **S2 — the command and its permission.** `/heal <selector>` joins the cheat family's two-sided check,
   resolves the target, carries the reset to the target's client, and reports the synchronous outcome by
   name. Red tests: the permission matrix, the self target, the dead target, the refusal answers.
3. **S3 — the replication.** The reset's result reaches the host and every peer (the existing
   character/health carrier or the result envelope the heal already uses), and the target's state survives
   a save and a reconnect.
4. **S4 — the two-client acceptance** of the six rows above, on the deployed artifact.

## Non-goals

- Not the item-based remote-medical heal; it keeps its UI path and its item/conscious rules.
- Not a change to the native `heal` command or its limb/hollow extension (the parent ticket's surface).
- Not CPR, and not a general "restore everything" for non-player entities.
- Not a KrokMP compatibility surface: the field list is read from the game's and KrokMP's code, but the
  command's design is the user's, not an inherited KrokMP command.

## Evidence the work starts from

- The parent ticket's field-by-field reading of KrokMP's `ResetHealth`, the native `heal`'s field list, and
  the hollow clear (the `MindwipeScript` removal + overlay).
- `Character/LimbRestore.cs` and the `ConsoleScriptHealHollowPatch` for the limb/hollow half.
- The cheat family's two-sided check: `cheat-permission-and-op-commands.md` and the `/give`/`/summon`
  handlers.
- The character-health carrier and `PlayerHealService`'s result path for the replication shape.
- The old wiring's behaviour in `docs/evidence/acceptance/20261018-t-scope.md` (the four log lines).
