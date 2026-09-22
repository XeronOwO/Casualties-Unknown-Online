# Review and delivery

[Documentation](../README.md) > [Contributing](README.md) > Review and delivery

---

**After this page** you can take a change from "understood" to "committed" in the order this
repository expects, run the reviews it has to pass, and write the commit. Read
[Build, test and deploy](build-and-test.md) for the commands and [Gates and binding rules](gates-and-rules.md)
for what the gates refuse.

## The hard order

Understand → mechanism inventory → plan and self-check table → user approval (large changes only) →
red test (defects only) → implement → build and gates → deploy → runtime verification → independent
adversarial self-check → structure review → commit.

This applies to all normal work: features, bug fixes, user-facing changes and internal improvements.
It is not reserved for rejected items or user-reported problems.

1. **Frame the task from the user's perspective.** For a reported issue, write the exact reproduction
   steps, the expected behaviour and an acceptance matrix covering roles, directions, views and
   related families. For feature work, state the functional intent, the user-visible behaviour and the
   scope before coding.
2. **Resolve functional design before implementation.** If the design is ambiguous or has user-visible
   trade-offs, research reference implementations and confirm the direction with the user. Once design
   and requirements are clear, proceed on implementation detail without over-asking.
3. **Check for a reusable native game UI or mechanism first.** If the game already has a surface for
   the feature, reuse it. If it does not, document the evidence and get user direction before building
   custom UI.
4. **Defects only: make the expected failure visible before fixing.** Add a regression test or runtime
   probe that fails on the current code, covering the reported scenario and its neighbours, and record
   the red. New feature work writes behaviour tests directly and needs no pre-implementation red —
   with no pre-existing defect there is nothing meaningful to watch fail. Red → green is a hard gate
   for defects: a compile error caused by a missing type is not a red, and "the test passes now" is no
   substitute for having watched it fail. If the implementation was made without first watching the
   regression test fail, stop and go back to the pre-fix code to record the red before presenting. The
   red step only needs the focused failing test to run; the full suite runs once the fix is in place.
5. **Implement, then verify against the full matrix.** Build, deploy the latest artifacts, verify the
   deployed artifact identity (hash or timestamp), then run the runtime, log and dual-client checks
   where they apply, and confirm every acceptance-matrix row before the change moves on. A build that
   passes without the latest DLLs running is not completion.
6. **Run an independent adversarial self-check before the commit.** Use a fresh context — an
   independent subagent, not the same reasoning path that produced the change. Start it against the
   FROZEN working tree: implement, run the affected tests and `dotnet format`, then stop editing until
   the report lands, and spend that window on non-conflicting work (deployment build, documentation
   drafts, evidence collection). Fix its findings in the SAME commit; a review run after the commit
   turns every finding into a second commit with its own format, full-suite and deploy round, which is
   the largest avoidable cost of a cycle (measured: one extra full round). Editing files while the
   review runs invalidates its conclusions. Ask for an interim report at the reviewer's first
   milestone, but do not edit until the final report lands. The prompt template, its FULL and NARROWED
   risk tiers, and the rule that every claim copied from an older document must be re-checked against
   the current tree live in [review-prompt.md](../../development/review-prompt.md).
7. **When a delivery is rejected or verification fails, run a root-cause loop.** Answer "why was it
   missed", record the process lesson and fix the leak before moving on; moving the ticket back is not
   enough.
8. **Keep incomplete or unverified work open.** Do not claim completion, do not reclassify known gaps
   as future work, and do not move to review until the exact scenario and the full acceptance matrix
   are verified.
9. **Budget the working context and hand off at phase boundaries.** Batching suits small, strongly
   related work only. After a task, or a phase of a multi-stage task, judge how much context is spent
   (long documents read, large sources, independent reviews, full-suite runs) and stop at the boundary
   with a handoff that states what landed, what is verified and what comes next. Exhausted context
   produces shallow work, which is a quality failure.

## Quality and delivery rules

- **No self-assumption.** Every claim needs source evidence — the path plus the quoted text, never a
  line number — or runtime evidence. The runtime is the judge, not the plan. A paper review is not a
  review: before hand-off, ask how the chain gets proven at runtime.
- **Fix the family, not just the reported case.** Inspect sibling mechanisms and other modules for the
  same defect pattern and align the whole family in the same cycle, or record a backlog item if the
  rest is genuinely out of scope.
- **User-found issues are hard release blockers.** Fix until the exact reproduction is resolved and
  verified, not merely until tests and gates pass.
- **Deployment and artifact verification are part of completion.** After a runtime behaviour change,
  build and deploy the latest artifacts and verify the deployed identity before reporting completion.
- **Development-period verification is simulation- and static-evidence based.** No manual dual-client
  acceptance during feature development: deployment to the physical machine and dual-client acceptance
  are the user's release-cycle actions, performed later.
- **Acceptance-readiness audit before review.** For every user-facing feature answer explicitly, with
  evidence: does the game already have a native UI for this surface; was the whole family audited
  across roles, directions, participants and third-party views; was the exact reproduction covered by
  a test or runtime trace against the latest deployed DLLs; were unsupported operations left as future
  only with explicit user acceptance. A self-imposed "future" is not a completed parity claim. Passing
  tests and gates is necessary, not sufficient.
- **The adversarial self-check must be independent.** A fresh context, never the reasoning path that
  produced the fix.
- **Rejection root-cause loop.** When a delivered item is rejected, record why it was missed; moving
  the ticket back is not enough — the leak must be understood.

## Definition of done for a user-facing change

Tests and gates passing is not the finish line. Before the change moves on:

- the exact user reproduction no longer reproduces;
- all roles, directions, participant views and third-party views are verified;
- the game's existing native UI is reused wherever one exists;
- the latest build is deployed and its artifact identity is verified;
- an independent adversarial self-check has passed;
- no known failing scenario is left as "future" without explicit user acceptance;
- the root cause is addressed, not patch-stacked.

## The delivery checklist

Every development cycle runs through [delivery-checklist.md](../../evidence/delivery-checklist.md);
`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` refuses the cycle's final commit
while any required box is unchecked.

- Boxes are checked ONE LINE AT A TIME with an editor as each step completes; bulk checking with a
  script or a single catch-up pass is forbidden, because it fabricates the process record.
- A checked box carries a short evidence suffix on the same line — `- [x] <item> — evidence: <command
  or file>` — one clause, with the detail in the ticket or evidence file.
- A documentation-only cycle (no runtime or test behaviour changed) still fills every box and still
  leaves the deployment line and the FORBIDDEN line unchecked, but may write its boxes in one pass,
  and may skip `dotnet format`.
- The deployment/acceptance line and the FORBIDDEN line stay unchecked; checking FORBIDDEN fails the
  gate on purpose.
- When a release cycle lands, reset the checklist by unchecking every box so the next cycle starts
  clean.

## Commit messages

- Conventional Commits for every commit: `type(scope): summary`.
- Allowed types: `feat`, `fix`, `docs`, `test`, `chore`, `refactor`, `perf`, `revert`, `build`, `ci`,
  `style`.
- The scope is a short lowercase domain or path name (`protocol`, `carry`, `backlog`, `projection`,
  `character-sound`, `adaptive-sync`), omitted when there is no clear domain.
- The summary starts with an imperative verb, stays lowercase after the prefix (proper nouns and
  acronyms keep their case), has no trailing period and is one line; an optional body starts after a
  blank line and is wrapped for readability.
- A pure documentation or backlog change uses `docs(scope): …`.
- Commits are GPG-signed; commit directly without disabling signing. The build, test and format
  commands run before the commit (documentation-only changes excepted), and the commit is made
  autonomously once they pass.

## How you know it worked

- `git log -1` shows the expected conventional-commit line, GPG-signed, with a clean tree around it.
- The delivery checklist has every required box checked with an evidence suffix, and the gate that
  reads it is green.
- Every acceptance-matrix row has a row of evidence, and anything only a real session can show is
  written down as awaiting the user's acceptance run.

## Related reading

- [Build, test and deploy](build-and-test.md) — the commands in the hard order
- [Gates and binding rules](gates-and-rules.md) — what a change has to satisfy
- [Writing documentation](documentation-standard.md) — the rules a documentation change follows
- [Delivery checklist](../../evidence/delivery-checklist.md) — the executable gate for a cycle
- [Independent review template](../../development/review-prompt.md) — the prompt for step 6

---

[Documentation](../README.md) > [Contributing](README.md) > Review and delivery
