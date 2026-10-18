# `/heal` — the console cheat command that resets a player's state

- Status: Done — accepted by batch `20261018-u` on `0.1.0+a3dff483`; all six rows pass (record:
  `docs/evidence/acceptance/heal-cheat-command-state-reset-20261018.md`). The S1-S3 implementation landed
  2026-10-18; the revive semantics were ruled by the user the same day: **in place**, the trader-revive
  reading, not the next-level respawn's.
- Priority: Medium-High
- Category: Commands / cheat verbs / player state
- Source: the user's correction of 2026-10-18, after batch `20261018-t`. The CUO console is an
  INDEPENDENT extension of the game's built-in console: the native `heal` existing does not mean CUO
  must not have its own. The user's design: `/heal` is a cheat command that resets a selected
  player's state immediately — item-free, a cheat verb. The follow-up questions were answered: FULL
  reset scope (the native limb fields + CUO's severed limbs and hollow + KrokMP's ~40 body fields),
  the CHEAT permission family (`AllowMemberCheatVerbs` + the `/op` roster; the host always), and a
  DEAD target is revived too. The revive-location question was asked and answered the same day:
  **in place** (the trader revive's semantics; no teleport to the layer spawn).
- Related: `review/heal-command-limb-and-hollow-restore.md` (the NATIVE `heal` command and its
  limb/hollow extension — a separate surface), `review/heal-restores-severed-limbs.md` (the limb
  restore the reset reuses), `done/in-game-command-console-interactive.md` (the console the command
  lives in), `review/cheat-permission-and-op-commands.md` (the cheat family's two-sided check),
  `docs/evidence/acceptance/20261018-t-scope.md` (the run that read the old wiring's behaviour)

## The design the user confirmed

`/heal <selector>` in CUO's console, as a cheat verb:

- **What it is.** An immediate, item-free reset of the selected players' state. Not the native local
  body command, not the remote-medical item path: three independent surfaces.
- **Scope: FULL reset.** The native `heal`'s limb fields and the body's `brainHealth`/`clawHealth`;
  CUO's severed-limb restore and the hollow clear (removing `MindwipeScript` and its overlay); plus
  the body fields KrokMP's `ResetHealth` writes. The exact table and the structural effects are
  *What landed* below.
- **Who may use it.** The cheat family: the host always; a member only with the host's
  `AllowMemberCheatVerbs` switch on AND the member named in the announced `/op` roster, checked on
  both sides exactly as `/give` and `/summon` are. A member's local refusal names the reason; no
  request is sent.
- **Targets.** The selector may resolve the local player or another member (the host included); the
  base selectors keep their meaning (`@s` local, `@p`/`@a` remote), so a command may name several
  members. A DEAD target is revived **in place** by the reset: the body gets up where it fell, no
  teleport, no inventory or skill change.
- **Authority and replication.** A reset is a mutation of the TARGET's body, so by the
  judgment-ownership rule (`docs/en/internals/judgment-ownership.md`) the target's own client applies
  it and re-reports the resulting state; the host keeps the authoritative copy and the result reaches
  every peer through the ordinary character snapshot, the same carrier the cross-player heal result
  already uses. The host's own target applies locally with no instruction round trip.
- **The answer.** The command's synchronous checks (cheat permission, selector resolution, target in
  world, dead/alive) are reflected in the answer by name; it does not answer `Sent` for a request it
  already refused. The target's own console prints what the reset did to it.

## Entry mapping (the native call site)

- **The reset effect** mirrors the game's own console command, registered at
  `ConsoleScript.RegisterAllCommands`: `new Command("heal", "Fully heals the player character.", …)`
  writes each limb's nine fields and the body's fields on `PlayerCamera.main.body`. The reset's value
  table is that body, expanded by the reference reading (below). CUO's `/heal` is a DIFFERENT surface
  by the user's ruling, so this is parity for the effect, not a second registration of the native
  name.
- **The severed-limb restore** mirrors what a dismember does in reverse (`Limb.dismembered` +
  `SetActive(false)`), which the native `heal` does not do: `LimbRestore` is CUO's own call site, and
  the parent ticket's Stage B owns its evidence.
- **The hollow clear** mirrors no native call: nothing in the assembly removes `MindwipeScript`; the
  parent ticket's Stage A owns that evidence.
- **The revive baseline** is the game's own revive path read in `TraderRecruitPolicy.PrepareRevive`
  (the trader revive's "heal in place" shape), plus `Body.MendBone`/`UnDislocate` for the latches.
- **Minecraft's template**: Java Edition has no vanilla `heal` command (fetched
  `minecraft.wiki/w/Commands`, 2026-10-18, cross-checked by web search); the verb's existence and its
  `<selector>` argument are the user's same-day design, recorded here rather than inferred from
  another edition or a plugin.

## What landed (S1-S3, 2026-10-18)

**S1 — the reset effect and the field list.**

- `Runtime/GameAdapter/HealResetState.cs` is the field table: the values the native command writes,
  KrokMP's additions (`furBloodAmount`, `bothEyesGone`, `reversedControls`), and three revive-baseline
  clears the native command lacks (`Shock`, `PainShock`, `FibrillationForced` — zero is what lets
  `Body.Stand` lift a revived body that is still lying down). `ApplyTo` writes the table onto a
  CAPTURED health message and limb messages, so every field the references do not reset keeps its
  live value (movement parameters, pose presentation, mood history).
- The table's limb half carries the native nine plus KrokMP's `furBloodAmount`, the healing timers
  (`SkinHealAmount`/`BandageSlowAmount`/`DisinfectionTime`) and the latch flags
  (`Broken`/`Dislocated`/`Splinted`/`BlockedBleeding`), and clears each limb's `Components` list —
  the codec's removal direction for splints, tourniquets and chilled limbs.
- `GameAdapter/Character/LocalBodyReset.cs` (behind `Runtime/GameAdapter/ILocalBodyReset.cs`, a port
  on the adapter) is the live-body half: capture the body, apply the table, and in this order —
  `HollowClear.ApplyImmediate` + the three medication components destroyed now (a frame-deferred
  destroy would let the same-frame re-report carry them back), the native `MendBone`/`UnDislocate`,
  `LimbRestore.Apply` (before the mapping clears the flags it reads), `ApplyHealState`, the two body
  fields Mapster cannot see (`opiateHappiness`/`antidepressantHappiness`), `reversedControls`, and
  `CoUtils.instance.CancelAll()` — then `ReportInventoryChanged`, which is the replication.
- Tests: `tests/CasualtiesUnknownOnline.Tests/Character/HealResetStateTests.cs` asserts the table
  against the reference list field by field (name → value, 79 entries), the capture-then-apply
  contract, and the removal direction through `MedicationComponentPolicy`.
- **Reuse over reinvention**: `HollowClear` gained `ApplyImmediate` (same removal, immediate destroy)
  and `LimbRestore` gained `CountMissing`; `CharacterDataSync` gained `CaptureHealth`/`CaptureLimbs`
  so the reset, the 1 Hz snapshot and the limb event share one capture.

**S2 — the command and its permission.**

- `Runtime/Session/Commands/HealResetCommands.cs` takes the verb out of `CommandConsoleService`:
  grammar → `CheatVerbGate` (before the selector, the family's order) → `CommandPlayerTargets` →
  `IHealResetControl.Reset`.
- `Runtime/Session/PlayerInteraction/PlayerHealResetService.cs` is the mechanics: the host judges
  `CheatVerbsOpenToMembers` then `AllowsCheatVerb` for every request, resolves each target against its
  own in-world set, applies its own body locally, sends `HealResetInstruction` (NetMsg 152) to each
  member target, and answers the requester with `HealResetResult` (153); a member's verb sends
  `HealResetRequest` (151). The host's own command returns its answer directly. A target the host
  cannot place in its world is refused by name; a request past the 32-target cap is refused whole.
- The target's client handles the instruction only from this session's host and prints the outcome on
  its own console (`HealResetNotice`); the result handler and the instruction handler both drop a
  frame whose sender is not the host, by name.
- Tests: `CommandConsoleHealResetTests` (host half: self reset, member instruction, dead-target
  answer, switch-closed host still works, out-of-world refusal, dedupe/aggregate, target-side refusal
  notice, the old wire is gone, selector/usage cases) and `CommandConsoleMemberHealResetTests`
  (member half: grant lands, self target, local switch refusal, local roster refusal, host's own
  roster refusal with nothing announced, the host's own switch refusal with the rules announcement
  swallowed, forged instruction and forged answer dropped, the target-count cap).
- The shared limb capture was made ONE complete helper in the same cycle (the adversarial review's
  blocker): the reset applies a captured limb row back onto the live body, so a row missing
  `IsHead`/`IsVital`/`DistanceToHeart`/`Components` would write CLR defaults and publish the
  corruption. `CharacterDataCapture.CaptureLimbs` is now the single capture the 1 Hz report, the
  limb event and the reset use; `LimbCaptureCompletenessGateTests` pins its six assignments and
  forbids a second mapper loop.

**S3 — the replication.** The reset ends in the target's ordinary character re-report (the existing
`NetMsg.CharacterData`/`HostCharacterData` carrier: the host saves it, relays a member's to the other
guests, broadcasts its own; the 1 Hz snapshot is the fallback). No new state message: the snapshot's
health, limb and component fields already carry every reset fact, and the removal direction was fixed
as this ticket's prerequisite. What the save/reconnect row proves is that a restored character keeps
the reset state (the same restore path decision 170 uses everywhere).

**Self-check table.**

| # | Claim | Evidence |
|---|---|---|
| 1 | The table covers the reference field list exactly | `HealResetStateTests.TheFieldTable_CoversExactlyTheReferenceFields` (reflection, 79 names) and `.TheFieldTable_CarriesEveryReferenceValue` |
| 2 | Fields the references do not reset survive | `.ApplyTo_LeavesTheFieldsTheReferencePathsDoNotResetAlone` |
| 3 | The reset sends absence, not a zero, for the components and the hollow | `.ApplyTo_SendsTheComponentRemovalsAsAbsence` (the component policy's `Absent` for all three medication components and the mindwipe) |
| 4 | A member cannot run the verb while either ruling is closed, locally | `CommandConsoleMemberHealResetTests.Member_Heals_WhenTheAnnouncedSwitchIsClosed_IsRefusedLocally`, `.…WhenTheSwitchIsOpenButTheMemberIsNotGranted_IsRefusedByTheRoster` |
| 5 | The host judges the same rulings for a request that arrives | `.Member_Heals_WhenTheRulesAnnouncementNeverArrived_IsRefusedByTheHostsOwnSwitch` (the announcement never arrived, so the local check is skipped and the request reaches the host) and `.Member_Heals_WhenNoRosterWasAnnounced_IsSettledByTheHostsOwnRefusal` |
| 6 | A self target resets the local body instead of the old silent drop | `CommandConsoleHealResetTests.Host_HealsItself_…`, `.Host_HealSelf_IsNotTheOldItemRequestWire` |
| 7 | A member target's reset runs on that member's own client | `.Host_HealsAMember_InstructsThatClientsOwnBody` (the target's console notice + the body seam call) |
| 8 | A dead target's answer names the revive | `.Host_HealsADeadMember_NamesTheReviveInTheAnswer` |
| 9 | A target the host cannot place is refused by name | `.Host_HealsATargetOutsideTheWorld_IsRefusedByName` |
| 10 | The host needs neither ruling | `.Host_HealsWhileTheMemberSwitchIsClosed_StillWorks` |
| 11 | A forged instruction and a forged answer do not touch a client | `.AResetInstructionFromANonHostSender_IsDropped` (the instruction is the only host-addressed frame that mutates a body: zero resets, no notice) and `.AnAnswerFromANonHostSender_IsDropped` |
| 12 | The reset re-reports the live body (the runtime half) | the adapter code plus acceptance row 1; the test host has no game `Body`, so this row is the acceptance's to judge |
| 13 | The limb capture is complete, so the reset cannot write CLR defaults onto the live limb | `LimbCaptureCompletenessGateTests` (one helper, six assignments, no second loop) and `CharacterDataMapperRegressionTests.Configure_LimbTopologyFields_AreWrittenOntoTheBody` (why an incomplete row would corrupt a real limb) |
| 14 | Every message-backed table value is wired to the same-named property | `HealResetStateTests.ApplyTo_WritesEveryMessageBackedTableField` |

**Field-table deviations, each deliberate and recorded.**

- `Shock`/`PainShock`/`FibrillationForced` are cleared although neither the native heal nor KrokMP
  writes them: they are the revive baseline's (`PrepareRevive`), and without them a revived body
  cannot stand (`Body.Stand` needs `shock < 10`).
- `Broken`/`Dislocated`/`Splinted`/`BlockedBleeding` are cleared on the message, after the native
  `MendBone`/`UnDislocate` ran: the native heal only zeroes the timers and lets the next `Limb.Update`
  clear the latches, which would make the same-frame re-report carry a limb that is already healed as
  still broken.
- The healing timers (`SkinHealAmount`/`BandageSlowAmount`/`DisinfectionTime`) are zeroed: they are
  pending-heal progress, and a reset that left them would keep healing after it.
- Splints, tourniquets and chilled limbs are removed by the limb-component codec's removal direction;
  the removed item is NOT handed back (the reset is item-free; KrokMP's `TakeOff` would instantiate
  it).
- `CoUtils.instance.CancelAll()` is KrokMP's clear of the game's timed-operation pump; without it a
  queued timed effect fires onto the healthy body a second later.

**Limits, recorded rather than hidden.**

- The host's answer names the INSTRUCTION for a member target ("sent to that player's own client")
  and the revive it will produce from its own snapshot; the target's applied outcome prints on the
  target's console and reaches every peer through its snapshot. If a target's client cannot apply it
  (no live body), the host has no ack to inspect — the target logs it and its next snapshot shows the
  unchanged state. An ack would add a message and a pending table for a case the in-world gate
  already makes unreachable for a normal session.
- The reset does not touch inventory, skills, position or the world: it is a body reset, and the
  revive is in place. A dead body in an unreachable spot revives where it is (the user's ruling).
- The native `heal` command keeps its own surface and its limb/hollow extension; the remote-medical
  item path is untouched. Row 5 of the acceptance matrix is the check.
- The `HollowClear` change adds the immediate variant only; the native heal's deferred removal is
  unchanged.
- A reset requested while a character restore is still queued (just entered, still generating) is
  REFUSED by name: the restore's two-frame wipe owns the body and would overwrite the reset, and the
  re-report is suppressed while a restore is pending. The player retries once the restore has landed.
  This is `LocalBodyReset`'s synchronous check; the control-plane tests use a fake with no restore
  queue, so the run's entry rows are where it is observed.
- The host→target instruction has no acknowledgement, so a target that cannot apply it (see the first
  limit) is visible on the target's console and in its next snapshot, not in the operator's answer.

## Adversarial review (2026-10-18, independent, frozen tree)

One blocker, three majors, nine minors, five nits; all fixed in the same commit.

**B1 (blocker) — the limb capture was incomplete, and the reset applies it back.** The new
`CharacterDataSync.CaptureLimbs` mapped a limb and set only `limbMsg.Index = i;`, while the 1 Hz
snapshot's own loop in `CharacterDataCapture` also set `IsHead`, `IsVital`, `Components`,
`ConnectedLimbIndices` and `DistanceToHeart`. `ApplyHealState` maps every row onto the live limb
(`_mapper.Map(limbData, body.limbs[limbData.Index])`), and Mapster's flexible matching writes
`isHead`/`isVital`/`distanceToHeart` — so the reset wrote false/false/0 onto the target's own head and
vital limbs (the reviewer verified it with an in-process Mapster run against a real uninitialised
`Limb`) and the re-report published the corruption to host and peers. The game reads those fields in
`Limb.BreakBone` (34 vs 10 internal bleeding, the head disfigure roll), the healing/ infection rules,
the tourniquet's lower-limb order, and the host's amputation guard. **Fixed** by extracting the one
complete `CharacterDataCapture.CaptureLimbs` and using it from all three producers, plus
`LimbCaptureCompletenessGateTests` (six assignments, no second loop) and the mapper regression test
that pins why an incomplete row is corruption rather than a thinner snapshot.

**M1 (major) — the back-end switch ruling had no case.** The member tests covered the local switch,
the local roster and the swallowed ROSTER, but not a closed host switch with NO rules announcement, so
deleting `_memberRules.CheatVerbsOpenToMembers` from `HandleRequest` stayed green. **Fixed**:
`.Member_Heals_WhenTheRulesAnnouncementNeverArrived_IsRefusedByTheHostsOwnSwitch` swallows
`NetMsg.HostRules`, lets the request reach the host, and reads the host's switch sentence with zero
resets.

**M2 (major) — the forged INSTRUCTION was untested.** The only host-addressed frame that mutates a
body had its sender check, but row 11 was backed by the answer test only. **Fixed**:
`.AResetInstructionFromANonHostSender_IsDropped` has a third handshaken peer send
`NetMsg.HealResetInstruction` to the member, and asserts zero resets, no console line and the handler's
own drop log.

**M3 (major) — the adapter port was `AddSingleton`, not `Replace`.** The Runtime seeds
`UnavailableLocalBodyReset`; the adapter added a second descriptor, leaving registration order as the
only thing making the real one win — a reorder would silently make the deployed verb answer "this
build has no game scene", the failure the item-state seam already records. **Fixed**: the adapter
`Replace`s it, `AdapterCapabilityPortShapeTests` counts both spellings and gained
`LocalBodyResetSeam_ReplacesTheRuntimeDefault`.

**Minors, all fixed:** the false "one capture" claim (now true — the helper is shared); the delivery
checklist's test count and its line-count ruler; the cheat-switch row in both configuration blocks
(now names `/heal`, with the pair re-recorded); "sixteen ports" → seventeen; the table doc's "two
fields" → three and the mindwipe header (the native heal does not touch it); the queued-restore race
(now refused by name, recorded in Limits); the missing cap test (added); the acceptance row's missing
reads (`limb-read` now prints `isHead`/`isVital`/`distanceToHeart` and a new `health-read` reads the
local body and the host's saved health, so row 1 is judgeable and would catch B1); the dead
`health.Alive/Conscious` write (dropped); the misleading `[CloneRender] inventory changed` log (now
"local body changed"); the permission ticket's wire row (now names row I13); the sampled `ApplyTo`
assertions (now every message-backed constant, by reflection); and the reset doc's "reproduced" wording
(it is a host-ordered local mutation; the scope keeps the hooks quiet).

## What the acceptance batch owes (the six rows)

1. Host `/heal <member>`: the member's health, every limb field, the severed limbs, the hollow moodle
   and overlay, and the body's need/vital fields come back to the healthy values, on the member's own
   client and in the host's copy; the member's console reads what happened. Reads the batch uses:
   `health-read source=local` (the member) and `source=saved target=<member>` (the host's copy), and
   `limb-read` for every limb on both sides — `source=local`/`source=saved` now print
   `isHead`/`isVital`/`distanceToHeart` too, which is the fact the blocker corrupted.
2. Host `/heal @s` (or a selector resolving the host): the host's own body resets — the old self-target
   silent no-op is gone.
3. A dead member `/heal <member>`: the member is alive again **in place**, with the body and the state
   the revive path defines; both clients read the same outcome.
4. Cheat permission, two-sided: with the host's switch on and the member in the roster, the member's
   `/heal <target>` works; with the switch off or the member absent from the roster, the member's local
   answer is a refusal naming the reason and the host's log window carries no request.
5. The remote-medical item heal and the native `heal` command behave as before; `/heal` does not
   consume an item and does not need the caster to be conscious.
6. The reset reaches the peers and survives a save/reconnect (or the row names the part that does
   not).

## Non-goals

- Not the item-based remote-medical heal; it keeps its UI path and its item/conscious rules.
- Not a change to the native `heal` command or its limb/hollow extension (the parent ticket's surface).
- Not CPR, and not a general "restore everything" for non-player entities.
- Not a KrokMP compatibility surface: the field list is read from the game's and KrokMP's code, but the
  command's design is the user's, not an inherited KrokMP command.

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
