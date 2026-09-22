# Reference map

English | [中文](reference-map.zh.md)

The reference layer is written for people working in the code, so it is English only and dense on
purpose. This page is the reading path into it: what each document answers, and the order that makes
them cheapest to read.

Read the first two rows before touching anything. The rest can wait until the work needs them.

## The path

- `docs/architecture/README.md` — the active architecture and the completed evolution history.
- `docs/architecture/current.md` — the current design: kernel, transactions, projections, non-goals.
- `docs/architecture/domains.md` — what belongs to which domain, and what is only a projection.
- `docs/architecture/protocol.md` — the four envelopes, joining, the state stream, save and recovery.
- `docs/architecture/guards.md` — the architecture gates that are active, and what each one protects.
- `docs/api/mod-api.md` — the mod API contract: lifecycle, permissions, host commands, content.
- `docs/api/advanced-modification-policy.md` — contract versus implementation, stability levels, patching policy.
- `docs/decisions/active.md` — the decisions that still apply, together with their reasons.
- `docs/features/items.md`, `docs/features/entities.md`, `docs/features/enemies.md` — the mechanism matrices.
- `docs/evidence/verification.md` — the evidence chain behind the claims this repository makes.
- `docs/backlog/README.md` — open work, grouped by status.

## Reading order for a first change

Start with the architecture page for the area you are changing, then the decision records that
mention it, then the feature-matrix row that declares its sync status. A change that disagrees with a
recorded decision is a decision change rather than an implementation detail.

## How a claim is proven

Every rule in this repository points at the gate or the evidence that enforces it:
`docs/evidence/normative-gates.md` is the rule-to-gate map, and `docs/evidence/delivery-checklist.md`
is what a delivery has to satisfy. Prefer the gate over the prose: a document can go stale, while a
gate cannot pass while the rule it enforces is broken.
