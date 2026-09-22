# Contributing

What a change to CUO has to satisfy before it is called done: the commands, the gates, the map of the
tree, the rules the pages follow, and the review the change goes through.

1. [Build, test and deploy](build-and-test.md) — the commands, the smallest test subset that can fail,
   and the log that explains a failure.
2. [Gates and binding rules](gates-and-rules.md) — the gate project, the conventions it enforces, and
   how a new gate is written.
3. [Repository map and pitfalls](repository-map-and-pitfalls.md) — which project a file belongs to,
   what may reference what, and the traps this tree is known for.
4. [Writing documentation](documentation-standard.md) — the two mirrored blocks, the page shape, and
   how a pair is kept in sync.
5. [Review and delivery](review-and-delivery.md) — the hard order from "understood" to "committed",
   the delivery checklist, and the commit convention.

Two registries stay outside these pages: the [rule-to-gate map](../../evidence/normative-gates.md)
answers "which gate enforces this", and the [test parallelization record](../../evidence/test-parallelization.md)
carries the measured numbers this section deliberately does not restate.

These pages are written for a contributor; the binding rules an agent must follow are
[`AGENTS.md`](../../../AGENTS.md) at the repository root.
