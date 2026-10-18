# Cheat permission, `/op` and `/deop`: who may run the cheat verbs

- Status: Review — **filed 2026-10-18** from the user's ruling on the `/give` verb's authority question (stage C of
  `docs/backlog/done/item-and-entity-data-commands.md`), and **widened the same day** by the user's combined
  direction (below), which supersedes the "`/give` host-only first" step. The first ruling in the user's own
  words: "`/op` `/deop` 这种命令能给他人授权或剥夺作弊权限(拓展:将某人加入作弊权限白名单,需要同时考虑 Steam ID 与
  IP 直连场景,对于 IP 直连场景可能需要额外评估玩家名伪造问题),主机的作弊权限通过联机开关控制,授权与剥夺只允许主
  机对其余客机执行". **Both halves landed**: the permission mechanism — `/op`, `/deop`, the announced roster, the
  host's cheat SWITCH and the two-sided check on `/data`'s write verbs (see *The permission mechanism landed*) —
  and `/give`, the same cycle's second half (`done/give-item-to-a-player-command.md`). The acceptance rows at
  the end are what a real run still owes; the UI and the persistence layers are the NEXT ticket (the user's
  direction, recorded below).
- Priority: Medium-High — it shipped WITH `/give` and the two-sided check for the cheat family, so it queued
  ahead of the rest of the item-data ticket's stage C.
- Category: Command surface / session authority / permissions
- Related: `docs/backlog/done/give-item-to-a-player-command.md` (the verb this permission ships with),
  `docs/backlog/done/item-and-entity-data-commands.md` (the cheat verb family this gates — `spawn` next),
  `docs/backlog/done/host-rules-replication.md` (the announcement pattern a
  member's client reads to refuse a closed rule locally), `docs/backlog/todo/remote-backpack-access-policy.md` (a
  host policy that gates a gesture, the same shape one level down), `docs/en/internals/permissions-and-security.md`
  (the admission seam and what a console verb may assume), `docs/decisions/active.md` decision 265 (a member's
  report is bounded by data; a permission list is a host ruling, not a client-enforced boundary), decision 241
  (pre-release wire changes do not bump the protocol number)
- Source: the user's rulings of 2026-10-18 (quoted here and in *The combined ruling*), plus the same day's
  follow-ups recorded under *The switch* and *Recorded for the next ticket*.

## The combined ruling (2026-10-18 — supersedes "`/give` host-only first")

The user's later direction the same day: "要不一起做了吧,既然要做基础的权限管理系统了,那顺带把主机与客机的 `/give`
都支持了吧,包括其它的作弊命令一起都要做前后端校验,前端指游戏实例,后端指主机的仲裁机制". So the first answer's
"`/give` ships HOST-ONLY first" step is **SUPERSEDED**: `/give` ships for the HOST and for GUESTS together with this
permission, and every cheat verb carries a TWO-SIDED check:

- **Front end = the game instance (the acting client).** It refuses a cheat verb locally, by name, when the host's
  announced rulings close it: the cheat SWITCH off, or its own entry missing from the announced roster. A closed
  permission costs no round trip, and the operator reads the reason where they typed the verb.
- **Back end = the host's arbitration.** The host's own switch and roster are authoritative and are judged for
  every request that arrives (the admission seam for wire commands, the verb's own seam for the host's local
  console); the client's copy is only a prediction, never the verdict. A refusal is ANSWERED so both sides end up
  agreeing (the item-data write's way back is the precedent).
- **The family** is the verbs that change the world or another player beyond the player's own legitimate actions:
  `/data`'s write verbs, `/give`, `/summon` and (since the 2026-10-18 correction) the console `/heal` reset. The host-admin verbs (`/hostrules`, `/kick`, `/ban`,
  `/unban`) keep `HostOnly` and need no two-sided check — only the host can run them, so there is no member
  request to arbitrate; the item-data READ (`/data get`) stays `Anyone`. The item-based remote-medical
  treatment is not a console verb: it is the Online UI's action, and it keeps its own path. The
  `/heal` history — why the same name was excluded before this correction — is under *`/heal` joined the family* below.

## The switch (2026-10-18, later the same day): a guest needs the switch AND a grant

The user's clarification: "假如先给了别人作弊权限或预先加过作弊白名单,依然受到主机的作弊开关影响,也就是对客机来说,需要
主机打开作弊开关并且有作弊授权,才能使用命令." So the host has ONE cheat switch for the whole family
(`HostRulesOptions.AllowMemberCheatVerbs`, announced with the host rules), and a member needs that switch OPEN and
its own entry on the roster. The pre-existing `AllowMemberItemDataWrite` flag was the switch for `/data`'s write
verbs only; it was RENAMED and WIDENED into this family switch rather than stacked under it, because the user's
rule is exactly two conditions and a third, always-on gate would have been one too many. Consequences, all
recorded: a granted member is refused while the switch is closed; the switch alone admits nobody; the refusal
names which of the two is missing; the host's own verbs need neither (its standing is the online session).

## The permission mechanism landed (2026-10-18) — `/op`, `/deop`, the switch, the roster and the two-sided check

The permission half of this ticket is implemented and green. Everything here is unit- and integration-covered;
the acceptance rows at the end are what a real run still owes.

**The identity question, decided (open question 1).** The key is the Steam ID, and an IP-direct session grants
NOBODY: the roster refuses `/op` by name in that mode, because that transport knows a peer by the logical id its
own connection registered rather than by an account, so a grant keyed on it would follow a connection rather than
a person. The ticket's other two options were rejected on purpose — keying on the address the transport
authenticated accepts that a NAT or a reconnect silently moves the grant, and a host-granted session token is a
new identity mechanism for one verb family. Consequence, recorded rather than hidden: in an IP-direct session a
member cannot run the cheat family at all, and the host's own standing is unaffected. **The user has since asked
for a real IP-direct client identity (a client certificate / keypair); that is its own ticket and would supersede
this refusal deliberately — see *Recorded for the next ticket*.**

**Mechanism inventory.**

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The host's SWITCH | `HostRulesOptions.AllowMemberCheatVerbs` (renamed and widened from `AllowMemberItemDataWrite`), off by default, announced with the host rules; `SessionMemberCommandRules.CheatVerbsOpenToMembers` answers it at the seam |
| 2 | The host's roster | `CheatPermissions` — sorted, session-owned (`ISessionReset`, dropped on `SessionEnded`); `Grant`/`Revoke` refuse by name on five standings: not host / no active session, IP-direct, the host itself (its permission is the online switch), player 0, a non-handshaken member |
| 3 | The member's copy | `AnnouncedCheatPermissions` — three distinct states: no announcement (skip the local check), an announced EMPTY list (closes the family), an announced list that does or does not name this client; sorted + `SequenceEqual` so a repeat is not a change; dropped on `SessionEnded` |
| 4 | The announcement | `CheatPermissionsMsg` = `NetMsg.CheatPermissions` 146, ABSOLUTE (the whole list every time); a list naming player 0 is refused WHOLE at the receiver, and a frame whose sender is not this session's host is dropped by name (the family fix: `HostRulesHandler` gained the same check) |
| 5 | Its edges | `CheatPermissionAnnouncer` — the member's handshake, the world-entry group, the repair group and every grant/revoke (`AnnounceAll`); a repeated `/op` for a member that already holds the grant RE-ANNOUNCES, so a stale closed list converges without waiting for the repair cycle |
| 6 | The FRONT END | `CheatVerbGate` — no session refuses both roles (the host's permission is the online switch); the host passes; a member is checked against the announced SWITCH first and then the announced ROSTER, each with its own sentence; with no announcement the corresponding check is SKIPPED and the host's verdict is the only one |
| 7 | The BACK END | `IMemberCommandRules.CheatVerbsOpenToMembers` then `.AllowsCheatVerb(sender)`, in that order, at `KernelCommandGateway`; both answered (`HostRuleDenied` for the switch, `CheatPermissionDenied` for the roster); the member settles the recorded value back and prints the refusal (`ItemDataWriteRefusal.Describe`) |
| 8 | The verbs | `CheatAdminCommands` — `/op` and `/deop`, both `HostOnly`, an unknown member refused by name; the roster's own refusals are the command's answers; a successful edit announces at that moment. Member resolution is shared with `/kick`/`/ban` through `CommandMemberTargets` |
| 9 | The gate on the existing verb | `/data set\|merge\|remove` calls the gate before anything is applied, and the seam judges the same two rulings on the wire; `ItemDataCommands` no longer carries a rule check of its own |
| 10 | No protocol bump (a fact, not a design input) | Decision 241: pre-release the number is a frozen baseline. This change ADDS a wire message (`NetMsg` 146) and one `RejectionReason` value; the message is declared in the sync-coverage matrix (row H2, index line, six anchors), and `RejectionReason.CheatPermissionDenied` is declared here in row 7 and in `ItemDataWriteRefusal.Describe` |
| 11 | The console stayed under the size gate | `CommandConsoleService` was 600 lines (the gate's ceiling) and is 551: the completion vocabulary moved out to `CommandSuggestions`, which the gate requires once a change adds even one constructor line |

**`/heal` joined the family (2026-10-18 correction), and the exclusion above is superseded.** The combined
ruling's sentence named `/heal`; the tree's `/heal` at that moment was wired to the remote-medical item path,
which is the player's own legitimate action and was therefore excluded. The user then corrected the design
(after batch `20261018-t`): the console `/heal` is its own CHEAT verb — an immediate, item-free full reset of
a selected player's state — while the item-based treatment stays the Online UI's action. That command is
`done/heal-cheat-command-state-reset.md` (implemented; the ticket is in `review/`), and it carries the same
two-sided check as `/give` and `/summon`: the front end refuses a closed switch or a missing roster entry by
name, and the host judges the same two rulings for every request that arrives. The two sibling tickets that
asserted the opposite were re-pointed in the same change
(`item-and-entity-data-commands.md`, `health-simulation-and-host-authority.md`).

**Self-check table.**

| # | Claim | Evidence |
|---|---|---|
| 1 | The roster starts empty and only a handshaken member can join it | `CheatPermissionsTests.AFreshSession_GrantsNobody`, `.APeerThatIsNotAHandshakenMember_IsRefused` |
| 2 | The host cannot op itself, and no session means no edit | `CheatPermissionsTests.TheHostItself_IsRefusedByName_BecauseItsStandingIsTheOnlineSwitch`, `.NoActiveSession_EditsNothing` |
| 3 | An IP-direct HOST refuses the op by name and stores nothing (the member's half of that mode is the acceptance row below) | `CheatPermissionsTests.AnIpDirectSession_GrantsNobody_AndSaysWhy`, `CheatPermissionReplicationTests.AnIpDirectHost_RefusesTheOpAndKeepsTheRosterEmpty` |
| 4 | The roster is sorted, idempotent and declared on the wire | `CheatPermissionsTests.TheRoster_IsSorted_…`, `AnnouncedCheatPermissionsTests.ARepeatAnnouncement_IsAbsorbedSilently_…`, `.TheMessage_RoundTripsTheList` (the store-level round trip; the wire serialization is the sync-coverage gate's own), `.TheMessage_RefusesAPlayerZeroEntryWhole` |
| 5 | Both session-owned stores drop on the session end edge | `CheatPermissionsTests.TheSessionEnding_DropsTheRoster`, `AnnouncedCheatPermissionsTests.ASessionEnd_DropsTheList_…`, `CheatPermissionReplicationTests.TheSessionsEnd_DropsTheRosterOnBothSides` |
| 6 | The announcement reaches a member at admission itself (the world-entry edge cannot stand in for it) | `CheatPermissionReplicationTests.TheHandshake_PutsAnEmptyRosterOnTheMember` (built with `enterWorld: false`) |
| 7 | A grant/revoke reaches a member at that moment | `.TheHostsOp_ReachesTheMemberAtThatMoment`, `.TheHostsDeop_ReachesTheMemberAtThatMoment` |
| 8 | The front end refuses before anything is applied, and names the ROSTER when the switch is open | `.AMembersWrite_IsRefusedLocallyUntilTheGrantLands`, `CommandConsoleMemberItemDataTests.Set_OnAMembersConsole_WhenTheSwitchIsOpenButTheMemberIsNotGranted_IsRefusedByTheRoster` (asserts the roster sentence and the ABSENCE of the switch's) |
| 9 | The two rulings together are what admits a member, and the SWITCH is the ground when both are shut | `KernelCommandGatewayTests.ConsoleVerbWithTheSwitchOpenAndTheMemberGranted_IsAdmitted`, `.ConsoleVerbWithTheSwitchClosedAndNoGrant_IsRefusedByTheSwitch`, `.ConsoleVerbWithTheHostRuleClosed_IsRefusedAndAnswered` (granted + switch closed), `.ConsoleVerbFromAMemberTheRosterDoesNotName_IsRefusedAndAnswered`; `CommandAdmissionIntegrationTests.AConsoleWriteFromAPromotedMember_IsStillRefusedWhileTheSwitchIsClosed`, `.AConsoleWriteFromAMemberWithNeitherGrantNorSwitch_IsRefusedByTheSwitch` |
| 10 | A member whose rulings were never announced is settled by the host's own refusal | `.AMemberWhoseRosterWasSwallowed_IsSettledByTheHostsOwnRefusal`, `CommandConsoleMemberItemDataTests.Set_OnAMembersConsole_WhenNothingWasAnnounced_IsSettledByTheHostsOwnRefusal` (the switch's ground) |
| 11 | The host needs neither; a member cannot op | `.TheHostsOwnWrite_NeedsNoGrant`, `.AMemberTypingOp_IsRefusedAsHostOnly`, `CheatPermissionsTests.TheHostItself_IsRefusedByName_…` |
| 12 | The whole family audited, not just `/data` | the console's cheat verbs are `/data`'s write verbs (gated at the admission seam), `/give` (gated in its own request handler, landed as `done/give-item-to-a-player-command.md`), `/summon` (its own request handler, landed 2026-10-18 under the same two-sided check), and `/heal` (its own request handler, landed 2026-10-18 under the same two-sided check — `done/heal-cheat-command-state-reset.md`) and `/data get` (still `Anyone` by the read half's own rule); the host-admin verbs stay `HostOnly`; the item-based treatment is the Online UI's path, not a console verb |
| 13 | The wire is declared where the sync audit demands it | sync-coverage matrix row H2 + its vocabulary index line + six evidence anchors; the `/give` pair is row I11 with its own five; `/summon` is row I12; the `/heal` verb's request/instruction/answer trio is row I13 with its three anchors |

**Limits, recorded rather than hidden.**

- Not an anti-cheat boundary (the ticket's own non-goal): a modified client can type its own console. Every
  request that arrives is judged against the host's switch and roster, and that is the whole claim.
- NOT persistent: both the roster and the switch's session standing live for the session. A host restart or a
  rejoin starts from nobody (the switch itself is a config entry, so its VALUE survives a restart, but the grants
  do not). The user's direction of 2026-10-18 makes persistence the next ticket.
- A STALE announced value can refuse something the host would admit, and only in one direction: a stale ALLOW
  value is healed by the host's own refusal plus the member's settlement, while a stale DENY value (the grant or
  the switch that would have allowed it never arrived) refuses locally without a round trip. The ways back are
  the next world entry, the repair cycle, or a repeated announcement — and the two halves differ there: a
  repeated `/op` re-announces the ROSTER half, while a stale CLOSED SWITCH is announced with the host rules and
  can only be healed by an entry, the repair cycle, or a `/hostrules` write. Recorded on sync-coverage row H2's
  loss cell as well.
- The config key was RENAMED by this change: an existing INI that still holds `AllowMemberItemDataWrite` is
  ignored and the new key defaults to closed. That is the safe direction, and decision 241 allows it pre-release,
  but a host who had opened the old key must open the new one.
- A member whose announcement is swallowed (the lazy peer session) applies and reports, and the host's own
  refusal settles it — the same fallback the host rules have, not a second one.
- IP-direct sessions: the whole family is closed to members (the identity decision above).
- A display name resolves to a member only as a convenience for the operator; every stored grant is the
  transport's id.

**Adversarial review of the frozen change (2026-10-18).** Independent, fresh context, read-only over the frozen
tree: **0 blocker, 6 major, 12 minor, 10 nit**, all but the two recorded below fixed in the same commit. The
majors, each with the fix: (M1) `KernelCommandGateway`'s order contract did not list the new ruling — the list's
item 3 now states the switch-then-roster order and why, and the human page's claim matches it; (M2) the order was
not pinned by any test — the discriminating cases above were added at the seam, through the real composition
root and at the local gate (both-closed must answer with the switch's ground); (M3) the host-facing
`configuration.md` still described the old flag — both language blocks now document `AllowMemberCheatVerbs` and
the AND; (M4) `/heal` was self-contradictory across three tickets — the family sentence now names the exclusion
and both sibling tickets were re-pointed; (M5) a stale deny list had no remedy and `/op`'s "already holds" branch
did not re-announce — the branch re-announces now, and the limit is recorded above and in the matrix; (M6) the
delivery checklist quoted a phrase that exists nowhere in the tree (it came from the session handoff, not a
document) — the line now cites the ticket's own wording. Minors/nits fixed: the member-report census row names
the roster bound; `architecture/current.md` and the item-data/future tickets name the switch; the protocol
message reference gained 146; `HostRulesService`'s comment names the renamed class; `src/AGENTS.md`'s red line
now follows decision 241; the claimed numbers were corrected to the measured ones (551 lines, class counts);
the announcer's edge count, the sync-cadence lead-in and the "four session-end drops" wording were corrected;
the admission-edge test was isolated with `enterWorld: false`; the IP-direct test was renamed for what it proves;
the wire-round-trip claim now says what that test actually asserts; the two same-named test grant helpers were
renamed and the quiet one now surfaces the host's refusal; the `/help` line no longer promises a verb this commit
does not ship; `CheatPermissions`' doc says the family gains a no-item heal, not the existing `/heal`; the gate's
roster sentence notes that an IP-direct session refuses `/op`; the "four standings" count is five; the rules
class moved to `Session/Permissions/`; the `Current` accessor is documented as the observability surface; and the
capitalised "cheat permission" treatment was dropped on the human page. **Not fixed, recorded:** (a) the
per-fix numbers in the review's own report are not reproduced here one by one; (b) a co-member that can send a
`HostToGuest` frame directly to another guest is now dropped by the host-id check, but whether Steam's transport
permits such a frame at all cannot be checked without a live session.

**Recorded for the next ticket (the user's 2026-10-18 direction, NOT built here).** The players page gets
grant/revoke buttons, and the permission becomes two persisted layers: the session grant follows the world
archive (written at a cut, restored on a continue), and a "permanent" whitelist lives in a host-local file (the
ban list's shape) that seeds a NEW run and auto-grants a whitelisted player the moment it enters — including a
mid-run join. The whitelist is machine-local, keyed on Steam ids, and does not apply in IP-direct sessions; a
Steam save opened over IP-direct must drop its roster with a named reason (decision 162's key spaces). The UI is
deliberately deferred to the same ticket: the member card should show the three states (granted / whitelisted /
neither) at once, and shipping half of it now would mean reworking it there.

**A focused review of the DELTA followed the same day**, after the switch rename and the review fixes:
**0 blocker, 2 major, 8 minor, 3 nit**, all fixed in the same commit. The two majors: the human
permissions page (both languages) still described the pre-switch model and a removed config key — it now
states switch-then-roster and the AND rule; and the live acceptance recipe `host-rules-read` still read the
removed key, which the gates cannot catch because the client compiles recipes at run time — it now reads
`AllowMemberCheatVerbs` and emits the renamed fields. The minors/nits fixed with them: the delivery
checklist's counts and the "needs the grant" wording, `ItemDataCommands`' and `RejectionReason`'s class docs,
the "per-verb" wording in two owners, a stale gateway test comment, the active decision record and two live
tickets naming the removed key (historical `done/` and `acceptance/` records deliberately keep it), the stale
CLOSED-SWITCH half of the stale-announcement limit (recorded in the Limits above and in the gate's doc), a
test for both new sender-drop branches (a third peer's forged roster and rules frames are dropped, the guest's
stores unmoved), the announcement test's parameter name, the `IItemControl` comment, and one Limits line for
the renamed config key. **Not fixed, recorded:** the review's own per-finding numbers are not reproduced here
one by one, and whether Steam's transport lets a co-member address another guest directly cannot be checked
without a live session (the drop branch is now covered regardless).

**What the acceptance batch owes.**

**Batch `20261017-q` judged these rows on the deployed `bb2350e7`**: rows 1-5 **pass** (each with the
console's own sentence quoted in the record), row 6 (IP-direct) is **unproven** — no transport lever in that
batch; the ticket stays in `review/` for it. Record:
`docs/evidence/acceptance/cheat-permission-and-op-commands-20261017-q.md`.

1. Host + member in one world, switch closed, grant present: the member types `/data set id <its item>
   condition …` and reads the SWITCH's sentence; the host's `/data get id <it>` still shows the old value.
2. The host opens the switch and grants nobody: the same command now reads the ROSTER's sentence.
3. The host runs `/op <member>`; the member types the same write and it lands, and BOTH peers read the new value
   through `/data get id <it>`.
4. The host runs `/deop <member>`; the same write is refused on the member's own console, and both peers still
   read the previous value.
5. The member types `/op <itself>`: "host-only"; the host types `/op <its own id>` and `/op 999999`: both refused
   by name with their own sentences.
6. IP-direct (needs the transport switch): the host's `/op` is refused with the IP-direct sentence, and the
   member's cheat verb is refused too. If the transport switch cannot be driven in the batch, this row is
   recorded `unproven` rather than weakened.

## What is asked

1. `/op <player>` and `/deop <player>`: the host grants or revokes another player's cheat permission. Only the
   host may run either, and only ON OTHER guests — a member cannot op itself or another member, and the host's own
   standing is not something a verb changes.
2. A cheat-permission roster rather than a one-shot flag: the list holds who may run the cheat verbs, and
   `/op`/`/deop` edit it — beside the host's own switch for the family.
3. Identity: Steam ID is the normal key. The IP-direct join has no Steam identity, so that case needs its own
   evaluation — a display name there is claimed, not proven, and a whitelist keyed on it would be spoofable.
4. The host's own cheat permission is governed by the online switch (the session/online toggle), not by `/op`.
5. The verbs the permission gates are the cheat family: the item-data verbs (`/data`), `/give`, `/summon`
   and the console `/heal` reset. The item-based remote-medical treatment is the Online UI's action, not a
   console verb, and is not gated by this family.

## Open questions — and where each stands now

- **The IP-direct identity question — ANSWERED for this ticket, REOPENED by the user as its own ticket.** The
  ticket refuses cheat permission outright for IP-direct peers (the decision above). The user then asked for a
  real per-client identity in that mode (a generated keypair, a fingerprint as the peer's identity, so a name or a
  logical peer id can no longer be claimed): that is its own ticket, would let IP-direct grants become keyed on a
  fingerprint, and would also remove the "names must be unique" pressure the save layer's name-key had. Not built
  here.
- **Session scope — ANSWERED for this ticket, DEFERRED for persistence.** Session-scoped today (both stores drop
  at the session end; the ticket's earlier "config profile" idea is superseded by the user's next-ticket
  direction: the grant follows the world archive, the whitelist is host-local).
- **The refusal and its answer — ANSWERED.** The member's client holds the announced switch and roster and refuses
  by name; the host judges and answers; the member settles the value it had applied.
- **What the permission is NOT — unchanged.** It is a host ruling on ordinary clients, not an anti-cheat boundary:
  every cheat verb still rides the host's admission seam and the host still judges what it accepts
  (`docs/en/internals/permissions-and-security.md`; decision 265).

## Non-goals

- Not an anti-cheat mechanism (above).
- Not a general account system: no remote administration.
- Not `/give` itself: that verb ships TOGETHER with this ticket (both roles), and this ticket owns the permission
  and the two-sided check it runs through; the verb's creation/delivery mechanics stay in the item-data ticket.
- Not the permission UI or the persistence layers: the next ticket (the user's direction above).
