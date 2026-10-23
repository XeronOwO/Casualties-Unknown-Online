# `/gamerule` joins the op family

- Status: Done — implemented and accepted in batch `20261024-b`
  (`docs/evidence/acceptance/gamerule-joins-the-op-family-20261024-b.md`). The design was agreed with the
  user on 2026-10-24.
- Priority: Medium-High
- Category: Host rules / permissions / cross-player request / console
- Source: the user's 2026-10-24 ruling, after the host-only `/gamerule` landed and was accepted
  (`done/gamerule-host-rule-command.md`): "就接入 op 体系" — a member the host has granted may change host
  rules, and a member that is not granted may not.
- Related: `done/gamerule-host-rule-command.md` (the host-only implementation this extends),
  `review/cheat-permission-and-op-commands.md` (the family this verb joins), decision 263 (a host rule is
  the host's, and the announcement is the change edge) and decision 269 (the verb's shape; its permission
  clause is amended here), `review/console-completion-hides-what-you-cannot-run.md` (the completion half of
  the same ruling), `docs/evidence/sync-coverage-matrix.md` row I14 and
  `docs/en/reference/protocol-messages.md` (the new request/answer pair).

## Problem

`/gamerule` is `HostOnly` and writes THIS process's own config entry. That is right for the host and wrong
for a member: a member's local write changes its own config file, which the host's announced set overrides,
so the change would look applied and do nothing. The verb's natural home is the cheat family, whose
authority is already two rulings — the host's `AllowMemberCheatVerbs` switch and the host's `/op` roster
(`CheatVerbGate`) — and the user's ruling places it there.

## The design

**Authority.** `CommandPermission.Anyone` at the console layer, like `/give`, `/summon`, `/heal` and
`/data`'s write verbs; the real gate is `CheatVerbGate` inside the handler. Consequences, all of them the
cheat family's existing behaviour:

- the host passes without either ruling (its standing is the online session), as it does for every cheat
  verb;
- a member passes only when the switch is on AND the roster names it; otherwise the existing refusal
  sentence names the ground (no session / switch closed / not on the roster);
- no active session refuses everyone, host included — the verb no longer works in the menu, which it does
  not today anyway (the host role exists only while hosting).

**The write path.** The host applies the change to its own config entry exactly as today (same editor, same
census names/values, same canonical parse), then decision 263's change edge announces the new set. The
member path is a request/answer pair:

- the member parses the rule and the value locally first (so an invalid value is refused without a round
  trip and the answer is immediate);
- a valid change is sent to the host with the rule id and the canonical value;
- the host re-validates the same gate against its own switch and roster and the same value grammar, applies
  the change, and answers the requester with the value IN FORCE (read back after the write, so a clamp or a
  refused write cannot answer a value the config does not hold);
- the member never writes its local config; its console prints the host's answer where it typed the line,
  the same shape `/give` and `/heal` use for a member's verb;
- the host answers a refusal by name: the switch, the roster, an unknown rule, an invalid value, a write it
  could not apply.

**Report validation.** The request is a request, not a report: the host checks the sender's identity (the
transport sender) against the roster it owns and its own switch — data it already holds — and re-derives
nothing about what the member could do. The rule id and value are validated against the host's own census
and grammar. No behavioural bound is added (decision 265's rule does not apply to a request; recorded in
`docs/architecture/member-report-validation.md`).

**One message pair, pre-release.** A `HostRuleChangeRequest` (host-bound: rule id, canonical value) and a
`HostRuleChangeResult` (answer text + applied flag, the shape the give/heal results use). Pre-release the
wire changes in the same commit with no protocol bump (decision 241); the sync-coverage matrix gains the
row, the protocol reference its line, and the member-report-validation census answers the request's
question.

**The command's home.** It moves out of `HostAdminCommands` (a group whose doc says every verb is
host-only) into its own rule group that holds the gate.

## Implemented (2026-10-24)

- `HostRuleCommands` owns the verb: `CommandPermission.Anyone`, the unchanged grammar (usage, the census
  name, the value's accepted set, the query form), and the order **parse → gate → apply/send**, so an
  invalid value or an unknown rule is refused without a round trip and a closed permission costs no request.
  The query path stays local: the host reads its own entries, a member reads the announced set its own
  `IHostRules` already exposes (no new message for a read). `HostAdminCommands` is now membership
  administration only (`/kick`, `/ban`, `/unban`), and its class doc says so.
- `HostRuleChangeService` (behind `IHostRuleChangeControl`) is the write path: the host re-parses the
  canonical value with `HostRuleValueText`, writes through the same `IHostRulesEditor` the Admin page and
  the host's own verb use, and answers `HostRuleValueText.Describe(HostRuleSet.From(IHostRules))` — the
  value read back AFTER the write. A read-back that disagrees with the write is logged as a warning and the
  answer still names the value in force. A member's call sends the request and returns null; the host's
  answer arrives through `Answered`, which the console prints at the typed line (`Success`/`Error` by the
  applied flag).
- The pair: `NetMsg.HostRuleChangeRequest` = 161 (guest → host: `Rule` = the census id, `Value` = the
  canonical text) and `NetMsg.HostRuleChangeResult` = 162 (host → requester: `Applied`, `Answer`), each with
  its packet handler and its narrow handler context. The result handler drops a frame whose sender is not
  this session's host, the rule the give/summon answers follow.
- The host's request path judges, in this order: its own role and session (a rule write needs no world, and
  the host's own verb applies in the lobby exactly as it does in world), the host's cheat switch
  (`IMemberCommandRules.CheatVerbsOpenToMembers`), the roster (`AllowsCheatVerb(sender)`), the rule id
  against its own census (`Enum.IsDefined` + `HostRuleCensus.Of`, an unknown id answered by name), then the
  value grammar and the write. A refusal is answered with the same sentence both consoles print.
- The wire doc comment records why the member's config is never written and why the answer is a read-back;
  the protocol reference (both languages), sync-coverage row I14 with its evidence, the report-validation
  census row, the configuration reference (both languages) and decision 269's amendment are updated in the
  same change. The completion half stays `review/console-completion-hides-what-you-cannot-run.md`, exactly as
  this ticket's Limits say.
- Tests: `CommandConsoleMemberGameRuleTests` (19 cases) covers the member set (host writes, answer prints), a
  set served while the host is OUT of world, the member's own config never written, the change edge reaching
  the member on the next pump, a member's query answered from the announced set, the local refusals (switch
  closed, roster, revoked, invalid value, unknown rule) each asserting no request was sent, the host's own
  switch re-check when nothing was announced, the swallowed-announcement cases (host refuses by roster; host
  serves a granted member), the host editor's refusal, the read-back answer (a clamping editor answers the
  clamped value, never the requested one), a non-canonical value and an unknown rule id sent straight to the
  host (both refused by name), and a forged answer whose sender is not the session host (dropped, not
  printed). The host's own matrix in `CommandConsoleCommandTests` now writes through a fake that fires the
  options monitor, so the read-back is real in those cases too.

## Acceptance

Three clients (host, `/op`-granted member, plain member — the batch machinery can start three):

| # | Row |
|---|---|
| 1 | The host's own `/gamerule` set/query still works exactly as in the accepted batch |
| 2 | The plain member's `/gamerule …` is refused by the gate, naming the switch/roster ground, and nothing changes on any client |
| 3 | The granted member's `/gamerule remote_backpack_access unconscious_or_dead` answers on its own console with the host's answer; the host's config entry holds the value; the plain member's announced set reads it on the next pump |
| 4 | The granted member's invalid value is refused locally, no request is sent, and the host's value is unchanged |
| 5 | With the switch off (or the roster revoked) the same granted member is refused and the rule stays |
| 6 | After a granted member's change, the member's own config file is unchanged and the host's holds the new value |
| 7 | The rest of the cheat family's refusals are unchanged (one `/give` or `/heal` probe as the control) |

## Accepted (batch `20261024-b`, 2026-10-24)

All seven rows passed on the deployed artifact `0.1.0+f90529458c42804b31cb7e80cc0b37c0907b6b0e` with three
clients (host, granted member, plain member). The record is
`docs/evidence/acceptance/gamerule-joins-the-op-family-20261024-b.md`. Row 2's local ground was the ROSTER
because this install's switch was already on; row 5a drove the switch's own ground. The member's own config
file was SHA-256- and mtime-identical across its write (row 6), the host's file and query held the new value,
and the host's log names the member's request as its actor (`[Rules] <member> set …`).

## Limits

- No completion here: the command word and its arguments are STILL offered to every caller — the console has
  no authority filter yet — so a member without the grant sees `/gamerule` in the suggestion list and in
  `/help`, exactly as the sibling ticket records. `review/console-completion-hides-what-you-cannot-run.md` hides
  the word and adds the rule/value suggestions; this ticket only changes the authority. **(Landed 2026-10-24:
  the follow-up is `review/console-completion-hides-what-you-cannot-run.md`; both halves are now in place.)**
- The member's line and the host's answer are two lines of evidence; a lost answer is a lost line, not a
  lost write (the announcement carries the outcome to everyone).
- The host's config write fires the existing change edge; the member's request does not add a second
  announcement path.
- The host's own console is not told when a granted member changes a rule: the host's log has the
  `[Rules] <actor> set …` line and the members get the announcement, but the host's console prints nothing.
  The same is true of a member's `/give`/`/summon`/`/heal` (the host logs those, its console does not).
- A clamped write is answered with the value in force (and logged as a warning), not with a second error:
  the config accepted the write, the operator reads what the entry really holds, and the clamp itself stays
  the pre-existing `todo/config-clamp-silently-widens-rule-levels.md`.
- The member's query reads `IHostRules` rather than asking the host: the announced set when one arrived, and
  this process's own config otherwise (the documented pre-announcement fallback the local checks share). It is
  as fresh as the last announcement, and a member that missed it converges on the next entry or repair
  (sync-coverage row H1). A query is NOT a round trip by design.
- The request path accepts any rule the census classifies; every rule is member-known today
  (`HostRuleCensus.Audience.Members`). A rule added later with `Audience.HostOnly` must be filtered out of
  this path before it lands, or a member could write a rule it is never told about.
- The parse runs BEFORE the gate (the design's order, so an invalid value costs no round trip), which means
  an unauthorized caller still learns the rule names from an unknown-name refusal and the accepted values from
  a bad-value refusal. That is no new disclosure: every rule is already rendered on a member's read-only Admin
  summary, and the operator's own typo is the sentence's subject.

## Independent review (2026-10-24) — every finding and what changed

Verdict: 0 blocker, 1 major, 8 minor, 5 nit. The reviewer reproduced the full suite (6315/6315), the gate
suite (776/777, the one red being the checklist's final box) and every line count, and could not falsify the
two-sided flow itself (gate → request → host re-judge → editor write → read-back → announcement; the member
never writes its own config). Every finding is dispositioned below; the fixed ones landed in this commit.

### MAJOR

- **M1 — a member's write was silently dropped when the host was not in a world.** The request path read
  `if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld) { return; }`:
  no answer, no write. The host's own `Change → Apply` has no world guard, so the same write was legal for
  the host in exactly that state, and `/summon`/`/heal` answer that state by design (their own comments say
  a refusal that never travels looks like a lost request). It contradicted this ticket's own claim that a
  refusal is answered, the census's rule 3, and row I14's loss cell. **Fixed:** the world guard is gone (role
  and session only), the reason is recorded at the guard, and
  `Member_SetsARule_WhileTheHostIsOutOfWorld_IsStillServed` proves a granted member's write lands and answers
  with the host in the lobby.

### MINOR

- **m1 — the checklist said "15 cases"; the class had 14.** **Fixed:** the checklist now says 19 (this review
  added five cases) and the filtered run is 19/19.
- **m2 — the revoked-refusal test did not assert the local refusal.** It asserted only `cheat roster` (a
  phrase both the local and the host sentence carry) and an empty editor, so it would pass even if a request
  had been sent. **Fixed:** it now asserts `Refused locally` and observes the host transport.
- **m3 — the member's query had no test and this ticket over-stated its source.** The query reads
  `IHostRules`: the announced set when one arrived, this process's own config before that (the documented
  pre-announcement fallback). **Fixed:** `Member_Query_AnswersTheAnnouncedValue` pins the announced path and
  the Limits now name the fallback instead of claiming the announced set only.
- **m4 — three host-side branches had no test.** **Fixed:** the host's switch re-check
  (`Member_WithNoAnnouncedSwitch_IsSettledByTheHostsOwnSwitch`), the canonical-form refusal
  (`Host_RefusesAValueThatIsNotTheCanonicalForm`) and the result handler's sender guard
  (`AnAnswerFromANonHostSender_IsDropped`) each have a case now.
- **m5 — the cheat family's enumerations were stale in live text.** The `AllowMemberCheatVerbs` rows in both
  configuration pages, the plugin's INI description, the `/op` command description, and the family ticket's
  self-check row 12 and "what is asked" item all omitted `/gamerule`. **Fixed:** all four now name it; the
  family ticket's opening paragraph had already been updated.
- **m6 — "the command word stays invisible" was false.** **Fixed:** the Limits now say the completion half
  is NOT implemented, that the word and its arguments are still offered to everyone, and that the sibling
  ticket is what hides them.
- **m7 — a member's permission refusal now renders green.** The old host-only pre-check added an `Error`
  line; the in-handler gate's sentence is a handler return and takes the console-wide `Success` colour.
  **Fixed by record:** `todo/console-command-refusals-render-green.md` now names the gate refusal (member's
  permission denial) and its acceptance list includes it; the colour itself is that ticket's work, not this
  one's.
- **m8 — two stale source comments.** The console's group comment still said `/gamerule` lived in
  `HostAdminCommands`, and the host test's class doc still listed the deleted host-only gate. **Fixed:** both
  now describe the current groups.

### NITS

- **n1 — the host's console is not told when a member changes a rule.** **Recorded** in Limits (the host's
  log has the line and the members get the announcement; the same shape the other cheat verbs have).
- **n2 — the request path accepts any census row, including a future `HostOnly` row.** **Recorded** in
  Limits as a constraint on the next rule: a host-only rule must be filtered out here before it lands.
- **n3 — the clamp branch is unreachable through today's plugin editor, and the plugin comment was stale.**
  The branch stays as the defensive seam the read-back exists for; **fixed** the comment, which now says the
  binding is what makes a clamp impossible and the answer is what would expose one.
- **n4 — the accepted ticket's deviation 2 still reads as current.** **Fixed by pointer:** the deviation now
  says it was superseded by the 2026-10-24 ruling and points at the supersede section below it.
- **n5 — parse-before-gate discloses rule names to an unauthorized caller.** **Recorded** in Limits: the
  order is the design's, the names are already on a member's Admin summary, and the refusal's subject is the
  operator's own typo.

What the review could not check at all: the three-client acceptance (this ticket correctly stays in
`review/` until it runs), the real in-game colours and the console's lobby reachability (M1's reachability
was read from code), the real BepInEx `SettingChanged` timing for the immediate read-back (the tests fake the
monitor; the prior batch proved only the deferred announcement edge), and the deployed plugin's behaviour.
