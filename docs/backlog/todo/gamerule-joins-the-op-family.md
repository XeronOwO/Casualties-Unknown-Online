# `/gamerule` joins the op family

- Status: Todo — design agreed with the user on 2026-10-24; not implemented.
- Priority: Medium-High
- Category: Host rules / permissions / cross-player request / console
- Source: the user's 2026-10-24 ruling, after the host-only `/gamerule` landed and was accepted
  (`done/gamerule-host-rule-command.md`): "就接入 op 体系" — a member the host has granted may change host
  rules, and a member that is not granted may not.
- Related: `done/gamerule-host-rule-command.md` (the host-only implementation this extends),
  `review/cheat-permission-and-op-commands.md` (the family this verb joins), decision 263 (a host rule is
  the host's, and the announcement is the change edge) and decision 269 (the verb's shape; its permission
  clause is amended here), `todo/console-completion-hides-what-you-cannot-run.md` (the completion half of
  the same ruling), `docs/evidence/sync-coverage-matrix.md` and `docs/en/reference/protocol-messages.md`
  (the new request/answer pair).

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
and grammar. No behavioural bound is added (decision 265's rule does not apply to a request; record why).

**One message pair, pre-release.** A `HostRuleChangeRequest` (host-bound: rule id, canonical value) and a
`HostRuleChangeResult` (answer text + applied flag, the shape the give/heal results use). Pre-release the
wire changes in the same commit with no protocol bump (decision 241); the sync-coverage matrix gains the
row, the protocol reference its line, and the member-report-validation census answers the request's
question.

**The command's home.** It moves out of `HostAdminCommands` (a group whose doc says every verb is
host-only) into its own rule group that holds the gate, or the group's doc is rewritten; the choice is the
implementer's, but the group's stated responsibility must match its verbs.

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

## Limits

- No completion here: the command word and its arguments stay invisible to a caller without the authority,
  and appear for one with it — that is `todo/console-completion-hides-what-you-cannot-run.md`.
- The member's line and the host's answer are two lines of evidence; a lost answer is a lost line, not a
  lost write (the announcement carries the outcome to everyone).
- The host's config write fires the existing change edge; the member's request does not add a second
  announcement path.
