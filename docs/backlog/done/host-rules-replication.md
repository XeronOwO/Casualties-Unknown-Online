# Host rules reach the members, and both sides check them

- Status: Done — the mechanism, its census gate and the two-node cases landed 2026-10-14, recorded at the
  user's direction when the member console write closed (S1b of `todo/validation-and-source-checks.md`).
  **Accepted 2026-10-16 in batch `20261016-a`**
  (`docs/evidence/acceptance/host-rules-replication-20261016.md`): every row of the table below passes —
  the announcement at admission, in a world and on the live change edge; the member's local refusal with
  nothing applied; the same verb forced past the local check and refused by the host; the session-end drop
  and the member's own rules when it hosts its own lobby; and the recruit affordance following the
  announced rule.
- Priority: Medium-High
- Category: Session policy / host rules / client-side checks
- Source: the user's 2026-10-14 instruction, in their words: the rules have to reach the members; KrokMP lets
  the host adjust rules WHILE a session runs, so a mid-session change has to travel as an event too; and a
  client has to check as it operates — what is forbidden is intercepted locally — while the host keeps its own
  check behind it, "the way a web app validates on both the front end and the back end".
- Related: `docs/en/reference/configuration.md` (`[HostRules]`, the twelve rules), `docs/decisions/active.md`
  210 (the admission seam, which stays the last word on anything a member submits),
  `todo/remote-backpack-access-policy.md` (the first consumer: `AllowRemoteInventoryTake` and the member-side
  take affordance), `done/item-data-write-commands.md` (the second: the console write's rule and the flicker
  this ticket removes), `docs/evidence/sync-coverage-matrix.md` row H1, `docs/architecture/protocol.md`
  (world-entry facts and the in-session repair group)

## What is asked

1. **The rules a member's client ACTS ON reach that client.** Which those are is a census rather than the whole
   list: a rule only the host executes needs no replication, and a rule whose value a member's screen, console
   or local gesture reads does. The question is answered per rule and kept answered — the shape
   `WireCommandAuthorityPolicy` uses for wire kinds (one entry each, a gate that fails on an unclassified
   member) is the precedent, and a rule added later must not be able to skip the question.
2. **A change while the session runs is an EVENT.** A host that flips a rule mid-session (the Admin page, the
   config file, or the console's host-rules command) must reach every member at that moment, not at the next
   world entry — KrokMP adjusts rules during play and parity is the requirement.
3. **Both sides check, and the host is still the authority.** A member's client refuses locally what the
   current rules forbid (so a forbidden gesture does not happen and does not have to be undone), and the host
   checks the same thing on its own side, because a client's belief can be stale, lost or forged — the local
   check is an interception layer over the operator, never the verdict.

## The census (asked 2026-10-15, from the code)

The question is one per rule: does a member's client read it? Twelve rules are on `IHostRules` — the eight
`[HostRules]` entries and the four `[Respawn]` ones the surface composes.

| Rule | What reads it on a member's client |
|---|---|
| `AllowRemoteInventoryTake` | the member card's take affordance — `OnlineUiMemberListDrawer` passes it into `OnlineUiMemberProjection`, which turns it into each row's `CanTake` and lists the takeable items |
| `AllowMemberItemDataWrite` | the member's own console — `ItemDataCommands` decides whether `/data set` is written and reported at all |
| `PiggybackWeightMultiplier` | the body that is CARRYING someone — `CarriagePatchBridge.GetCarriedEncumbrance` answers for the local body only, so a guest carrier's own movement penalty came from the guest's config |
| `ReviveFromTrader` / `Permadeath` | the member card's RECRUIT button — `OnlineUiMemberProjection.CanRecruit` asked no rule at all until this change and offered the button into a host refusal; it now follows the same two terms the host's own gate reads (`RespawnPolicy.CanUseTraderRecruit`), which the replication made possible for the first time |
| the other seven (`PvpEnabled`, `AutoContinue`, `AllowLateJoin`, `WidenRunSettings`, `NativeBindingParity`, `SaveInventory`, `ReviveOnNextLevel`) | the Admin page's read-only summary, which renders every rule for a member |

**The answer is therefore the whole list**, and the reason it is the whole list is the read-only summary: a
rule a member's screen shows is a rule a member's client reads. That is what settled the design rather than a
narrower behavioural set — and it is also why the summary is now driven BY the census
(`HostRuleCensus.MemberRows`) instead of a second hardcoded list: a rule the census marks host-only is neither
announced nor rendered on a member's screen, so the classification cannot become a lie in the UI.

The twelve names live in one vocabulary (`HostRuleId`), and the census gate
(`tests/CasualtiesUnknownOnline.Tests/Session/HostRuleCensusTests.cs`) reflects `IHostRules` and fails when a
rule is missing from the census, from the announced set (`HostRuleSet`), from the wire message
(`HostRulesMsg`) or from either localization catalogue — so a rule added later cannot skip the question.

The independent review ran the same census from the code rather than from this table and reached the same
answer (0 rules announced but read by nobody on a member, 0 read on a member but not announced, 0 readers that
should have been switched to the host's value and were not) — which is the check this ticket's first
requirement asks for, done twice by different readers.

## How the five questions this stage owned were answered

- **What the announcement carries:** the whole member-known set, ABSOLUTE — one field per rule, sent every
  time, never a delta. Protobuf omits a field at its default, which is exactly right here: "false" and "0" are
  values a host may hold, and the receiver replaces its whole set rather than merging what arrived.
- **How a change travels:** both config monitors the surface is composed from are watched
  (`IOptionsMonitor<HostRulesOptions>` and `<RespawnOptions>`), so the Admin page, the config file and the
  console's host-rules command are one event and one message per member — not a wait for the next world entry.
  Convergence is the absolute set plus the repair group: admission, the world-entry group, the 60 s
  in-session repair group, and the change edge all send the same message.
- **What the local check may do, and may never do:** it may refuse a gesture the ANNOUNCED rules forbid, in the
  rule's own name; it may never be the reason something is allowed. It fires on an announced set only, so a
  member whose belief is stale or absent applies and reports as it always did and the host's own answer
  settles the record. The judgement-ownership rule is untouched: the host arbitrates, the affected client
  judges its own experience, latency is never a parameter. The set is SESSION-scoped: it belongs to the session
  that announced it and is dropped on the session-end edge, so a process that was a member and later hosts —
  or joins a different host — reads its own configuration again instead of enforcing a stranger's rules
  (the independent review's blocker; see the review section below).
- **What happens with no announcement at all:** the member reads its own config and skips the local check —
  the behaviour that existed before the rules were replicated, never a guess at the host's intent. This is a
  branch with its own case (`HostRuleAnnouncementTests.NothingAnnounced_ReadsAsNoSetAndNoLocalRefusal`,
  `HostRuleReplicationTests.AMemberThatNeverReceivedTheSet_KeepsItsOwnRulesAndSkipsTheLocalCheck`).
- **The wire shape:** `NetMsg.HostRules` = 144, host → member only, one message, authoritative direction
  (`HostRulesHandler`, `[PacketHandler(..., NetMessageDirection.HostToGuest)]`), and the receiver names a frame
  that arrives while this process is the host. The member never reports a rule back. `ProtocolVersion.Current`
  is FROZEN at the pre-release baseline (decision 241), so no number moves for this change.

## What landed (files)

| Piece | Where |
|---|---|
| the rule vocabulary, the absolute value set, the census | `Runtime/Session/HostRules/HostRuleId.cs`, `HostRuleKind.cs`, `HostRuleSet.cs`, `HostRuleCensus.cs` |
| the member-side store and the local check | `Runtime/Session/HostRules/AnnouncedHostRules.cs` — session-scoped: it subscribes the session-end edge and drops the set (`ISessionReset`), so the set never outlives the session that announced it |
| the host-side announcer (admission + change edges) and its port | `Runtime/Session/HostRules/HostRulesAnnouncer.cs`, `IHostRulesAnnouncement.cs` — the change edge records a flag and the pump (`ICuoService.Update`) sends, because the thread that wrote the config entry is not this service's to choose |
| the wire message and its receiver | `Runtime/Protocol/Messages/HostRulesMsg.cs` (with its closed-set check), `Runtime/Session/Handlers/HostRulesHandler.cs`, `Runtime/Protocol/NetMsg.cs`, `Runtime/Session/HandlerContexts/IHostRulesHandlerContext.cs` |
| the surface in force for this process | `Runtime/Session/HostRules/HostRulesService.cs` (`Current` = announced ?? own config) |
| the two groups that carry it and heal a swallowed send | `Runtime/Session/World/WorldEntryFanout.cs` |
| the member's console refusal | `Runtime/Session/Commands/ItemDataCommands.cs` |
| the member card's affordances, and the read-only summary, driven by the announced rules | `Runtime/OnlineUi/OnlineUiMemberProjection.cs` (the take AND the recruit gesture), `Plugin/OnlineUiAdminDrawer.cs`, `Plugin/OnlineUiContext.cs` |

## Acceptance

| # | Scenario | Expected | Judged |
|---|---|---|---|
| 1 | A host flips a rule the member's screen reads, while both are in a world | The member's next look at the surface reflects it within one announcement, and no local gesture is refused on the old value | `HostRuleReplicationTests.AHostFlippingARule_ReachesTheMemberAtThatMoment` (no pump between the flip and the read) and `.TheRespawnHalfOfTheSurface_ChangesLiveToo`; the run reads it on the member's Admin page |
| 2 | A member's client holds a rule the host has just closed | The forbidden gesture is refused LOCALLY, in the rule's own words, and nothing is applied and undone | `CommandConsoleMemberItemDataTests.Set_OnAMembersConsole_WhenTheAnnouncedRuleIsClosed_IsRefusedBeforeAnythingIsApplied` (RED with the local check disabled, and the only case that failed) — the local object keeps the old value and the host never refuses anything; the run reads the console line |
| 3 | The same gesture, forced past the local check (a modified client) | The host refuses it exactly as it does today, so the local check is not the boundary | `CommandConsoleMemberItemDataTests.Set_OnAMembersConsole_WhenNothingWasAnnounced_IsSettledByTheHostsOwnRefusal` (the host's own refusal, arriving as it always did) plus `KernelCommandGatewayTests.ConsoleVerbWithTheHostRuleClosed_IsRefusedAndAnswered`; the run forces it past the local check |
| 4 | A member joins a running world after the host changed a rule | The member enters with the current set, not the values from the host's config file at its own launch | `HostRuleReplicationTests.AMemberWhoseOwnConfigDiffers_ReadsTheHostsRules` (the two nodes hold opposite config) and `.AMemberWhoseAnnouncementWasSwallowed_IsPutRightOnWorldEntry` |
| 5 | An ordinary session, unmodified clients | Nothing changes for the host, and nothing is refused that was not refused before | `HostRuleReplicationTests.AMemberChangingItsOwnConfig_AnnouncesNothing` (the host's own rules are untouched by its announcement), the four `CommandConsoleMemberItemDataTests` cases that were green before this change, and the run |
| 6 | A process that was a MEMBER later hosts its own lobby (the review's blocker) | It judges and announces its OWN rules, never the ones it was told; a member that left one host stops refusing on that host's values | `HostRuleAnnouncementTests.TheSessionEnd_DropsTheAnnouncedSet` and `HostRuleReplicationTests.AMemberThatThenHosts_JudgesAndAnnouncesItsOwnRules` — both RED with the reset disabled — plus the L0 `NothingAnnounced_ReadsAsNoSetAndNoLocalRefusal`; the run covers the ordinary single-session shape |
| 7 | A member whose rules forbid a trader recruit | The card offers no recruit button, and the host's own gate is unchanged behind it | `OnlineUiMemberProjectionTests.TraderReviveForbidden_HidesTheRecruitAction` (both terms, in isolation) with `TraderRecruitCoordinator`'s own gate as the host half |

## The independent adversarial review, and what it changed

The review ran in a fresh context against the frozen tree before the commit, and it found one BLOCKER and one
MAJOR that the tests could not have caught by construction:

- **Blocker — the announced set outlived its session.** `AnnouncedHostRules` had no clearing path and
  `HostRulesService.Current` preferred it unconditionally, so a process that had been a member and later
  hosted enforced the PREVIOUS host's rules: the late-join gate, the native-binding parity switch, the
  cross-player take policy, the item-data-write admission, the run-settings widening, the Admin page's own
  editable rows and — worst — the values it announced to its own members. The same root cause showed from the
  member side: a member joining a second host kept refusing on the first host's values until the new
  announcement arrived. Fixed by making the set session-scoped (`ISessionReset` + the session-end edge, which
  every role transition in `SessionService` reaches through `TeardownSession`), pinned by two cases that are RED
  with the reset disabled.
- **Major — a callback thread this service does not own.** The change edge sent from inside
  `IOptionsMonitor.OnChange`, whose thread is BepInEx's business (the in-process writers are on the main
  thread; a file re-read is not this service's to choose), while every other send in this tree happens on the
  pump. The callback now records a flag and `ICuoService.Update` sends — one frame later, on the same thread as
  every other send — and the claim that a config-file edit is "the same edge" is attributed to decision 25
  (the project's config hot-reload rule) rather than to a class body that does not prove it.

Also closed from the same report: the receiver now checks the one closed set on the wire (an unknown
`NativeBindingParity` level refuses the whole announcement instead of indexing a label list with it), the
recruit affordance follows the announced rules, the two protocol pages gained the message, and four dangling
references were re-pointed. The review could not check anything needing the game running, which is what the
acceptance batch is for.

## Limits (recorded, not hidden)

- **Opening a remote backpack is not part of the local check.** `RemoteBackpackCoordinator.Open` never asked
  the rule, and whether the open belongs to the policy at all is the three-way decision
  `todo/remote-backpack-access-policy.md` owns (its question 3 — "how does a guest learn the host's policy" —
  is now answered by this announcement; the ticket is updated to say so). The member-side TAKE affordance does
  follow the announced rule, and so does the RECRUIT button.
- **Two rules have no consumer on either side.** `PvpEnabled` and `AutoContinue` are reserved surfaces; they
  ride the announcement because a member's screen shows them, and nothing reads them.
- **The member's own config entries survive behind the announcement.** A member that received a set ignores
  its own values for every rule; the entries are still read when nothing has been announced, which is the
  documented fallback rather than a second source of truth.
- **The rules are a session fact, not a save fact.** Nothing about the announced set is written to a world
  archive, and the set is dropped when the session ends (never carried into the next one) — the admission
  announcement and the world-entry group mean a member is put right before it acts, and the fallback covers
  the window in between.
- **The announcement is a whole-set message.** A single flip re-sends all twelve fields (20 bytes on the
  wire); no delta form exists, deliberately, because a delta cannot heal a member that missed one.
- **The change edge lands one frame later than the write.** The callback records and the pump sends, because
  the writing thread is not this service's to choose. One frame is the whole interval, and the set is read at
  the send point, so a burst of writes costs one announcement carrying the final values.

## Non-goals

- Not a permissions system for mods or players: the rules stay the host's own switches.
- Not a second verdict: the host's enforcement is unchanged, and no rule becomes a member-side authority.
- Not the anti-cheat stage: `todo/validation-and-source-checks.md`'s S2 and S3 question the CONTENT of a
  report, which no announcement can do.
