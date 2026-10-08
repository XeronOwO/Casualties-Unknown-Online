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

Report findings by severity (blocker / major / minor / nit), each with a path or `file:line`, the
quoted text, and how you verified it. Then state explicitly what you could NOT falsify and what you
could not check at all (for example anything that needs the game running). Send an INTERIM report at
your first milestone so the parent can triage while you continue; the parent must not edit the frozen
files until the final report lands.
