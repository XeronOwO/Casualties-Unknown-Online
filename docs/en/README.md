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
3. **Understand why** — `internals/`: who decides what, the deterministic kernel, the protocol, saves,
   the adapter boundary, and the cost of each choice.
4. **Look something up** — [Reference](reference/README.md): the mod API, protocol messages,
   configuration, feature matrices and the [glossary](reference/glossary.md).
5. **Work on CUO itself** — [Contributing](contributing/README.md): build and test, the gates, the
   repository map, the documentation rules and the review-and-delivery workflow.

## How these pages are written

Every page has the same shape: a breadcrumb, one sentence saying what the page is for, what you need
first, the steps, a runnable example, why it works that way, the traps, how to check that it worked,
and a few links to read next. A project word is linked on its first use in a page.

## Still English only

Architecture, decisions, evidence and the backlog are contributor material and are being rewritten
into these two blocks. Until that finishes, the working entry points are:

- Architecture: [current](../architecture/current.md), [protocol](../architecture/protocol.md),
  [domains and projections](../architecture/domains.md), [save archive format](../architecture/save-archive-format.md)
- Interfaces and policy: [mod API contract](../api/mod-api.md),
  [extension and stability policy](../api/advanced-modification-policy.md)
- Mechanisms and matrices: [items](../features/items.md), [entities](../features/entities.md),
  [enemy sync](../features/enemies.md), [game internals](../features/game-internals.md)
- Records: [active decisions](../decisions/active.md), [evidence](../evidence/verification.md),
  [rule-to-gate map](../evidence/normative-gates.md), [backlog](../backlog/README.md)
- Development: [repository layout and pitfalls](../development/agent-reference.md),
  [operations and deployment](../operations/README.md)

---

Documentation
