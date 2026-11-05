# Split the body-verb service

- Status: Done — the split landed on 2026-11-05 in commit `6308da4a`, and the four rows were judged on the deployed artifact in batch `20261105-a`.
- Priority: Medium (it blocks nothing today, but the family grows one verb at a time and each one lands in the same class)
- Category: Structure / console verb family
- Source: the `/kill` work (2026-11-02). Threading the second operation through `Execute` pushed the class over the
  aggregate ceiling; the ticket that added it recorded the split as its own next step rather than doing it mid-verb.

## Design

Read from `docs/design/mechanics/console.md` (the console verb family and its cheat gate): a verb family has ONE control
plane — the gate, the target roster, the host's judgement and the answer — and the operation is a parameter of it, not a
second protocol. That is why the kill operation rode the heal messages instead of adding four new ones. The split must
keep that property: what moves out is the per-target APPLICATION and the ANSWER BUILDING, not the plane itself.

## Entries

The family has exactly two entries, and neither changes in this cycle:

- **`/heal <selector>`** (`HealResetCommands`) — the console line carries the verb, which names `BodyOperation.Heal`, and
  the selector expansion, which names the targets. Both facts the behaviour needs are in the gesture, so nothing is
  refused for missing information.
- **`/kill <selector>`** (`KillCommands`) — the same shape with `BodyOperation.Kill`; it reaches the same
  `IHealResetControl.Apply` and the same messages, which is the family's one control plane.

A member uses the same two console verbs: the same gesture, with only the routing differing (request instead of
instruction). No entrance is added, renamed or moved, and no gesture gains or loses the information it carries.

## Design comprehension and parity

**Surface row.** `docs/architecture/surfaces.md`, the Console (command + chat overlay) row: judged against the
**Minecraft template** (root rule 17 — `@a` = all players including the executor, `@s` executor, `@p` nearest, empty
Enter closes), surface authority "CUO's own IMGUI overlay; the game's own console is only the host surface", records
`done/in-game-command-console-interactive.md` (batch `20261018-t`) and decisions 228, 264, 269. The row names
`docs/design/surfaces/command-overlay.md` and `docs/design/mechanics/console.md` as the pages that state the behaviour.

**What the design page fixes (quoted from `docs/design/mechanics/console.md`).**

- "A command's answer is a line in the console, in the reading player's language. A command that refuses names why. Two
  families of answer exist, and both are localized by the client that reads them: the answer the host decided (it travels
  as a key plus its values) and the answer the console itself decided (the line never left the client)."
- "A command's own rules decide whether it runs; where the command acts on another player, the host validates it and
  answers."

**The three parity lines.**

1. *The player's action and expectation.* `/heal @a` or `/kill @a` typed into CUO's console: the operator reads one answer
   line per part in their own language and one line per target naming what happened (reset, killed, sent, or refused with
   its ground); a member target's body changes on that member's own client, whose console then prints the host's notice.
2. *What the game itself runs for the same action.* The native console registers `heal` ("Fully heals the player
   character.", setting every limb and body field to its full-health value) at
   `reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs:530-545`, and `kill` ("Instantly kills the player character
   by setting their brain health to 0.") as `PlayerCamera.main.body.brainHealth = 0f;` followed by
   `this.LogToConsole("Killed the player.")` at `ConsoleScript.cs:620-624`. CUO's `/heal` applies the same full-health
   table (`HealResetState`) through the adapter's `LocalBodyReset`; `/kill` applies the same one-field write through
   `LocalBodyKill`.
3. *How the remote case reaches the same chain.* The host judges and sends `HealResetInstructionMsg` carrying the
   operation; the target's own client calls the same `ILocalBodyReset.Reset` / `ILocalBodyKill.Kill` adapter seam (the
   native call site above) and reports `HealResetAppliedMsg`, which the host stores on the character data's authority
   path.

**What this change must NOT alter.** Every answer sentence and catalogue key; every log prefix and level; the four
`HealReset*` messages and their fields; the permission order (session open -> operation named -> member switch -> roster ->
target cap -> host in world); target dedupe and caller order; one outstanding instruction per body with its 30 s liveness
bound and echoed id; the `Applied` / `Answered` events; and the absence of a new wire message. It is a structural split:
`docs/architecture-debt.json` loses its entry because the class is under the ceiling, not because the ceiling moved.

## The split

The class currently holds five jobs: the wire events, the permission ruling, the target roster, the per-target
application (local body vs instruction) and the answer text (fallback sentence plus keyed parts).

| piece | goes to | why |
|---|---|---|
| answer building (`DescribeLocal`, `DescribeRemote`, `AddClauseParts`, keyed parts per operation) | a small `BodyVerbAnswers` type | it is the only part that differs per operation and it is pure formatting |
| per-target application (local heal / local kill / instruction to a member) | a `BodyVerbApplier` type | it is the only part that touches the body seams |
| wire events, permission, roster, pending-instruction table | stay | that is the control plane, and it is what the family must keep in one place |

The shapes this ticket implements:

- `BodyVerbAnswers` (internal, own file): the answer builder. Owns `ILocalizationService` and its own logger; exposes the
  key-bearing `Part(labelKey, ...)` and `Line(labelKey, ...)` helpers, `Refused(key, ...)`, the fallback sentences
  (`Applied`, `SentToMember`, `SentKillToMember`, `Killed`, `Notice`, `RefusedFor`) and the keyed clause parts. It owns
  no session, no roster and no body seam, so the same target fact always renders the same sentence and key.
- `BodyVerbApplier` (internal, own file): the per-target application. Owns `ILocalBodyReset`, `ILocalBodyKill`,
  `PacketSender` and the answers type; exposes `ApplyLocally(target, operation)`, `InstructMember(target, actor,
  operation)` and `ApplyInstruction(operatorSteamId, msg)`. It returns the facts (the seam outcome, the answer parts, and
  the issued instruction id for a member) and never reads or writes the pending table.
- `PlayerHealResetService` stays the control plane: wire handlers, the permission ruling, the roster (dedupe + in-world),
  the pending table, and the `Execute` loop that decides per target which application runs and records an issued
  instruction only after the transport accepted it.

The target is the same behaviour with the class under the ceiling: the full suite and the gates green, and the `/kill`
and `/heal` acceptance rows still judged the same way.

## Family

The ceiling family is recorded, not guessed: `docs/architecture-debt.json` holds exactly one entry, this class. The
console verb family itself has two members (`/heal`, `/kill`) and one control plane, which is why the split keeps them in
one service. The pins that move with this change are the two lists that name the tree's shape — the debt ledger (entry
deleted) and `docs/design/audit/sweep.md` (the `Session/PlayerInteraction` block's file count rises by the two new files)
— plus `docs/evidence/sync-coverage-evidence.json`, whose one quote for this class is the pending-table id check that
stays in the service, so it is re-read rather than edited.

## Self-check table

| mechanism | change | evidence |
|---|---|---|
| answer text and catalogue keys | moved verbatim into `BodyVerbAnswers` | `CommandConsoleHealResetTests` / `CommandConsoleMemberHealResetTests` assert the same sentences; the localization census still resolves every key |
| the native `/heal` table and `/kill` write | unchanged, still the adapter seams on the owner's client | the suite's `FakeLocalBodyReset` cases and the new `FakeLocalBodyKill` cases; acceptance rows 1-2 on the deployed artifact |
| the permission order and the roster | unchanged, still decided in the service before any application | the member test class' refusal cases (switch, roster, cap, no-roster) |
| the pending-instruction table | stays in the service; `Execute` records the issue after a successful send | `AnInstructionTheTransportRefuses_IsAnsweredAndLeavesNoPendingReset` and the applied/expired/superseded cases |
| the class ceiling | 619 aggregate lines measured red; after the split 407 + 181 + 150, none over 600 | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` |
| the `/kill` operation's own path | had no suite coverage at all; now two cases pin the local write and the member instruction | `Host_KillsItself_WritesZeroBrainHealthAndNamesTheValueItHeld`, `Host_KillsAMember_InstructsThatClientsOwnBody` |

## Verification design

- **Failure first, measured:** with the `PlayerHealResetService` entry deleted from `docs/architecture-debt.json`, the
  source-shape gate exited 1 with `PlayerHealResetService : 619 aggregate lines (max 600; either split or record in
  docs/architecture-debt.json)`.
- **Then:** the split landed and the same gate passed (1 of 1) with no entry for the class; the files measure
  `PlayerHealResetService.cs` 407 lines, `BodyVerbAnswers.cs` 181, `BodyVerbApplier.cs` 150.
- **Behaviour:** the heal/member test classes run over the real composition root, the real wire handlers and the shared
  body fixtures, so the split is judged through the console and the wire, not through the new types directly. A `/kill`
  case was added for the path that had no suite coverage at all (self kill and a member instruction), so both operations
  of the family are pinned before the acceptance rows read them; the two classes together ran 32 tests, 0 failed.
- **Runtime:** rows 1-2 read on the deployed artifact in a one-client session (`/heal @a`, `/kill @a`, then the saved
  character read back); row 3 needs a member and is judged only if a session forms; row 4 is the ledger file read back.
- Full suite and normative gates green, `dotnet format` clean, deploy identity `+<sha>` matching the commit.

## Acceptance rows

Judged on the deployed artifact in batch `20261105-a`; the record is
`docs/evidence/acceptance/split-the-body-verb-service-20261105-a.md`.

| # | Action | Expectation | Verdict | Reading |
|---|---|---|---|---|
| 1 | `/heal @a` after the split | the same answers and the same bodies as before | pass | `heal.answer.reset_applied` then `heal.answer.clause_revived`; body `alive=true conscious=true brainHealth=100 consciousness=100 bloodVolume=100` after a kill had read `brainHealth=0` |
| 2 | `/kill @a` after the split | the same answers and the same bodies as before | pass | `kill.answer.applied`; body `alive=false brainHealth=0` with `bloodVolume=100 heartRate=70.178` unchanged (the native one-field write) |
| 3 | a member's request, granted and not granted | the same local refusal and the same host answer as before | pass | not granted: `/heal refused locally: the host's announced roster does not name <member>` and nothing sent; granted: after `/op`, the host's log records the member's reset at `22:52:57:542` and the guest's console reads `Reset <host-id>: full heal applied.` |
| 4 | the recorded ceiling | `docs/architecture-debt.json` no longer lists the class | pass | the file reads `{}`; the source-shape gate ran green (812 gates) |

## Run sheet

- [x] 1. Read the console design page and the two native call sites; write the comprehension and parity sections above —
  evidence: `docs/design/mechanics/console.md`, `ConsoleScript.cs:530` (heal) and `:620` (kill).
- [x] 2. Map the family's two entries and count the ceiling family — evidence: the Entries and Family sections; the ledger
  held one entry.
- [x] 3. Delete the ledger entry and run the source-shape gate to see it fail — evidence: exit 1, "619 aggregate lines
  (max 600)".
- [x] 4. Split the class into `BodyVerbAnswers` and `BodyVerbApplier`, keeping the control plane in the service — evidence:
  407 + 181 + 150 lines, the same gate green with no entry.
- [x] 5. Add the `/kill` behaviour cases with a fake kill seam — evidence: the two new cases plus the existing classes, 32
  tests passed, 0 failed.
- [x] 6. Build, format, then the full suite and the normative gates — evidence: build 0 warnings / 0 errors, `dotnet format`
  exit 0, the gates project 812 passed, the behavioural suite 6598 passed.
- [x] 7. Commit, deploy, verify the artifact identity, then read the acceptance rows — evidence: commit `6308da4a`, deployed
  artifact `0.1.0+6308da4a` verified exit 0, a host + guest session judged rows 1-3 and the ledger file judged row 4.
- [x] 8. Write the acceptance record, move the ticket to its final folder, and update the index row — evidence:
  `docs/evidence/acceptance/split-the-body-verb-service-20261105-a.md`, this file under `done/`, and the index row.
