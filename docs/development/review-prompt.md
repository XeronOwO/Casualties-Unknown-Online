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

- **A. Every copied claim.** The change restates facts carried over from tickets, handoffs, or older
  documents. For each one, open the code or test it names and confirm the CURRENT state. A stale
  restatement is a finding even when the original document was correct when it was written — this is
  the single most productive check in practice.
- **B. Each claim's own evidence.** Run the gates/tests the change cites and reproduce its numbers. A
  number that cannot be reproduced, or whose decomposition does not add up, is a finding.
- **C. The mechanism, not the wording.** For behaviour claims read the test BODIES (does the assertion
  actually pin the claimed behaviour, or does the name merely sound right?) and the production call
  sites (is the path reachable at all?).
- **D. Reference integrity.** No dangling paths; index and table entries match reality in BOTH
  directions (every entry resolves, every item is listed); status fields match their containers.
- **E. What the change does NOT say.** Missing rows, unstated limits, over-claims, evidence that was
  deleted rather than moved.

Report findings by severity (blocker / major / minor / nit), each with a path or `file:line`, the
quoted text, and how you verified it. Then state explicitly what you could NOT falsify and what you
could not check at all (for example anything that needs the game running). Send an INTERIM report at
your first milestone so the parent can triage while you continue; the parent must not edit the frozen
files until the final report lands.
