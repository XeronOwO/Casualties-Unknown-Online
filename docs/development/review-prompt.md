# Independent adversarial review — prompt template

Start the review in a fresh context (`subagent`, not a fork of the working conversation) BEFORE the
commit, against a FROZEN working tree. The reviewer must not edit files. Fill the bracketed slots and
delete whatever does not apply.

## Risk tier — pick ONE and delete the other

- **FULL** — protocol, save format, architecture, cross-module, or user-visible behaviour: reverse
  directions, third-party views, boundaries, and regressions of adjacent features.
- **NARROWED** — behaviour-preserving extraction, single-point fix, or a documentation/evidence cycle:
  the changed files and their immediate neighbours, plus every claim the change makes.

## Prompt

You are an independent adversarial reviewer (fresh context, no stake in this change). Repository:
`<absolute path>` (Windows; branch `<branch>`; HEAD `<sha>`). The working tree holds an UNCOMMITTED
change set — that is what you review. **Do not modify any file** (the tree is frozen): read-only
checks and test runs only. Do not run `dotnet format` — it rewrites files. Useful shapes:
`dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --nologo -v q`,
`dotnet test tests/CasualtiesUnknownOnline.Tests --nologo -v q --filter "FullyQualifiedName~<class>"`.

What the change claims (these are the claims to attack, never the evidence for them):

1. `<claim>`
2. `<claim>`

Adversarially verify:

- **A0. The PREMISE, before anything else — and this check is mandatory for any change whose behaviour
  crosses players, mirrors a native action, or changes something a player can do.** Do not accept the
  design as given: write down, yourself, the mapping the change depends on and check it against the
  game's own call sites. For each entry the change touches, answer three questions in this order and
  report each answer as a finding if it is wrong or missing:
  1. **Which native action is this CUO gesture the counterpart of?** Name the decompiled call site
     (a gesture is a call site, not a family name) and quote it. A CUO entry with no native
     counterpart is itself a finding: it is CUO's own invention and its scope must be argued, not
     inherited from an older CUO table.
  2. **Does the action need information the gesture cannot carry?** A limb, an identity, an intent, a
     target: if the request has no field for it and the code substitutes a fallback ("the most injured
     limb", "the first match"), that fallback IS the finding — a design contradiction shipped as a
     default. It must be refused or asked about, never documented as a limit.
  3. **Could this item/action belong to two entries at once?** If two flows can both claim it and the
     code decides by ORDER (a family chain, an if-else ladder), the finding is the shared decision, not
     the order: the two entries are meant to be isolated, and the item's own data has to say which
     action each entry runs.
  An earlier cycle shipped a cross-player limb tool reachable from the inventory-use gesture (which
  names no limb) with the limb resolved to the target's most-injured one, and this section exists
  because the review of that cycle was scoped to attack the implementation and could not see the wrong
  premise. Ask the same three questions of the change in front of you.
- **A0a. The PLAYER-FACING PREMISE — what does "connecting it" mean?** Before the mapping, write down
  yourself what problem the change solves for a player, and what the design is: who uses it, what it
  does, what it costs, what it refuses, what the player sees. A compatible service, a matching signature
  or a mechanism that would otherwise be unused is NOT a reason to add a surface; "the code connects",
  "it compiles" and "the mechanism needs a consumer" are findings, not justifications. Then ask whether
  the design meets the normal expectation of the name or place it appears in — a `/heal` that cannot
  heal its own caster, costs an item or answers success for a refusal is wrong at the design level,
  however clean the wiring. A user-visible design with trade-offs must have been asked (AGENTS.md rule
  9); the implementation's own help text or doc comment is not that answer.
- **A1. Report validation — for every fact the change has a member REPORT rather than request.** Name,
  yourself, what the receiving side checks that claim against: the record it holds, the table it can read,
  the announcement it made. A fact taken on the reporter's word is a finding unless the census declares it
  `unbounded:` with the anchor it would need and a ticket
  (`docs/architecture/member-report-validation.md`); a bound that re-derives something the receiver cannot
  reproduce (an unseeded roll, another client's frame) is a finding too, and so is a report that names an
  identity the message cannot carry. Ask the other two halves as well: is a refusal ANSWERED so both sides
  end up agreeing, and can an unmodified client meet this refusal in ordinary play?
- **A1b. Data or behaviour?** Ask of every check this change adds: does it compare the claim with a fact
  the receiver ALREADY HOLDS (an identity, a value it computed, a table it owns), or does it model what the
  player was physically ABLE to do? The second kind — reach, aim, timing, cooldown, input rate, "could this
  gesture have happened" — is anti-cheat, which decision 265 rules out: it duplicates the game's rules in a
  second, weaker implementation and refuses legitimate play when the two disagree. It is a BLOCKER even when
  it looks like a tidy bound, and it is a blocker when the "fix" for an unbounded row is a distance check
  rather than the wire identity the row actually needs. Also ask whether the new bound can be exercised at
  all: a check whose only proof is an acceptance verb sharing its own conversion proves nothing.
- **A. Every copied claim.** The change restates facts carried over from tickets, handoffs, or older
  documents. For each one, open the code or test it names and confirm the CURRENT state. A stale
  restatement is a finding even when the original document was correct when it was written — this is
  the single most productive check in practice. Numbers get the same treatment with one extra
  question: **can this figure be reproduced from the frozen tree?** A count, line count or suite
  total that cannot (for example one measured from an uncommitted intermediate state) is reported as
  unverifiable unless it names its source.
- **B. Each claim's own evidence.** Run the gates/tests the change cites and reproduce its numbers. A
  number that cannot be reproduced, or whose decomposition does not add up, is a finding.
- **C. The mechanism, not the wording.** For behaviour claims read the test BODIES (does the assertion
  actually pin the claimed behaviour, or does the name merely sound right?) and the production call
  sites (is the path reachable at all?).
- **D. Reference integrity.** No dangling paths; index and table entries match reality in BOTH
  directions (every entry resolves, every item is listed); status fields match their containers.
- **E. What the change does NOT say.** Missing rows, unstated limits, over-claims, evidence that was
  deleted rather than moved.
- **F. Contract shape — only when the change adds or changes an `Abstractions` member.** Ask the three
  questions the modification policy puts in front of every baseline line: is a payload a typed definition
  or the framework's own data model; is a handle a CUO-defined type or an engine type, never `object`; is a
  binary value an explicit leaf rather than the envelope? A member that fails one is a finding. So is a
  claim that a SCAN enforces this: the judgement lives in the review, and a spelling check would flag a
  legitimate binary leaf while an envelope spelled as JSON passed it.
- **G. Does every document the change lands — or keeps — have a reader?** For each page, section, record or
  file it adds, name who opens it and when: an agent at a specific decision point (then it must be a file that
  is loaded or routed at that moment), or a reader arriving from an index. A file that only records what the
  cycle did, an append-only log, a ledger that grows by appending, or a restatement of a rule already carried
  elsewhere is a finding: say which page should carry the conclusion, and whether the file should be deleted or
  rewritten. Existing offenders belong to the corpus audit
  (`docs/backlog/review/document-corpus-audit.md`); the same question is asked of the documents a change touches
  but does not add.

Report findings by severity (blocker / major / minor / nit), each with a path or `file:line`, the
quoted text, and how you verified it. Then state explicitly what you could NOT falsify and what you
could not check at all (for example anything that needs the game running).

Send an INTERIM report only when a finding would change the parent's plan — a blocker that stops or
re-scopes the round, or a contradiction it must put to the user — because the parent must not edit the
frozen files until the final report lands: a routine milestone ping cannot be acted on, so it is noise.
Otherwise deliver one final report.
