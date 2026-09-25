# CUO documentation — English

Documentation

---

What a player or a mod author needs, in reading order. The English block is `docs/en/`; the Chinese
block `docs/zh/` is a full mirror of this tree — [中文文档](../zh/README.md) carries the same pages at
the same paths.

## Where to start

Pick the line that matches what you want to do; each line runs from shallow to deep.

1. **Get it running** — [Start here](start/README.md): what CUO is, how to play it, how to set up a
   development checkout, and a first mod you can watch working.
2. **[Do one thing](how-to/README.md)** — one task per page, with the steps, a runnable example and the traps.
3. **[Understand why](internals/README.md)** — who decides what, the deterministic kernel, the protocol,
   saves, the adapter boundary, and the cost of each choice.
4. **Look something up** — [Reference](reference/README.md): the mod API, the modification policy,
   protocol messages, configuration, feature matrices, the logs and the [glossary](reference/glossary.md).
5. **Work on CUO itself** — [Contributing](contributing/README.md): build and test, the gates, the
   repository map, the documentation rules and the review-and-delivery workflow.

## How these pages are written

Every page has the same shape: a breadcrumb, one sentence saying what the page is for, what you need
first, the steps, a runnable example, why it works that way, the traps, how to check that it worked,
and a few links to read next. A project word is linked on its first use in a page.

## What stays outside these pages

The five lines above are the whole of the human documentation — a player or a mod author needs nothing
else. Three kinds of material answer "how do I work on CUO itself" instead, so they stay English and
outside this tree:

- **Architecture specifications** — [the current design](../architecture/current.md),
  [domains and projections](../architecture/domains.md), [the protocol](../architecture/protocol.md),
  [the save archive format](../architecture/save-archive-format.md), and the completed
  [evolution history](../architecture/README.md).
- **Process records** — [active decisions](../decisions/active.md),
  [evidence](../evidence/verification.md), the [rule-to-gate map](../evidence/normative-gates.md) and
  the [backlog](../backlog/README.md).
- **Agent-facing pages** — [repository layout and pitfalls](../development/agent-reference.md).

A conclusion that belongs in the documentation has been absorbed into the pages above; what is left in
those trees is the record of how it was reached.

---

Documentation
