# CUO Architecture: Current Design and Evolution History

This directory contains the **active architecture** of CUO and the completed
evolution history that produced it. The typed deterministic game-state kernel is
the only supported design; the phase documents below are history, not plans.

> Status: **Architecture evolution complete** (Phases A–E). The typed kernel is
> authoritative across all migrated domains, the protocol/save stack is kernel-driven,
> dual-architecture surfaces are gone, and the Phase E guard suite is in place.
> See [status.md](evolution/status.md), `docs/evidence/selfchecks/architecture/phase-e-legacy-inventory-selfcheck.md`,
> and `docs/decisions/active.md` #158.

## What belongs here, and what belongs in `docs/design/`

The two areas answer different questions, and a page filed on the wrong side is moved rather than duplicated:

- **Here — the abstract model.** What the system IS: the layers and their allowed directions, the kernel and its
  transaction/authority model, domain ownership (authoritative state versus rebuildable projection), the protocol's
  envelopes and flows, the invariants every domain must keep, and the non-goals. A reader finishes a page here knowing how
  the system is shaped and why, without needing the call path of any single behaviour.
- **`docs/design/` — the concrete behaviour.** What a player does and sees: the input sequence, the rules and thresholds,
  the refusals and their sentences, the native call a gesture mirrors, who judges on whose timeline, what crosses between
  clients, and the accepted record. A design page names the surfaces, the item families and the verdicts; it does not
  restate the kernel or the protocol.
- **The test for a page.** If it would still be true after every player-facing behaviour were rewritten, it is
  architecture. If it would change when a behaviour changes, it is design. An architecture page that starts describing a
  specific action's call path has drifted; its conclusion belongs in the design corpus, and the page keeps only the rule.
- **Surfaces.** `surfaces.md` is the bridge: it says which design authority governs each player-facing surface and links
  the corpus page. It belongs here because the map is an abstraction; the detail lives in `docs/design/`.

## Misfiled pages (audit in progress)

Reading the directory against the division above, three pages are on the wrong side of it. None is moved in this pass; each
has its action named so the move is a work item rather than a rewrite.

| Page | What it actually is | Where its content belongs | Action |
|---|---|---|---|
| [remote-inventory-native-parity.md](remote-inventory-native-parity.md) (690 lines) | a **design record**: the four stages of one behaviour, their decisions (218–221, amended by 258) and its acceptance matrix | `design/mechanics/give-and-take.md`, `design/items/container.md`, `design/model/item-custody.md` | merge the conclusions into those pages, then retire this page and re-point its links — `todo/merge-remote-inventory-design-into-corpus.md` |
| [mod-status-domain.md](mod-status-domain.md) (273 lines) | a **feature design** ("Status: Design with phases 1–… landed"), not a system-shape page | `design/cross-cutting/` (a status-domain page) once the feature's behaviour is read | file when the status domain's behaviour is read; until then it stays as the feature's own record |
| [save-archive-format.md](save-archive-format.md) (714 lines) | a durable **format contract** (what an archive contains), which is legitimate here; the behaviour it enables is not | the format stays; the behaviour is already on `design/cross-cutting/saves-and-recovery.md` | keep, with the corpus page as the behaviour's home |

## Active architecture

Read in this order:

1. [current-architecture.md](current.md) — the current typed
   deterministic kernel: core flow, project structure, transactions, authority,
   guards, non-goals, success criteria. Domain/protocol detail is split into the
   next two files.
2. [domains.md](domains.md) — domain ownership: which state is kernel-owned, which
   is a rebuildable projection, and the current domain table.
3. [protocol.md](protocol.md) — the four-envelope protocol, join flow, state stream,
   save/checkpoint, command rejection, and non-kernel direct frames.

## Why this exists

The previous implementation had many correct low-level mechanisms but still split
one item fact across `WorldItemTable`, `ItemArbitration._transferred`,
`CloneFactTable`, Unity `Item`, rollback caches, and periodic snapshots. Other
domains had similar split ownership. The typed deterministic kernel gives each
persistent gameplay fact one authoritative write path while keeping each domain's
own typed model and invariants.

## Directory map

| File | Purpose |
|---|---|
| [current-architecture.md](current.md) | **Active** current architecture: kernel core, transaction/authority model, guards, non-goals. |
| [domains.md](domains.md) | **Active** domain ownership and projection map. |
| [surfaces.md](surfaces.md) | **Active** player-facing surfaces: what each is judged against, its native counterpart and its accepted record. Read this before changing any surface. |
| [projection-framework.md](projection-framework.md) | **Active** global unified projection framework contract and migration status. |
| [remote-inventory-native-parity.md](remote-inventory-native-parity.md) | **Design (stage 0)** remote inventory operations run the native call on the owner's client; stages 1-4 not implemented. |
| [mod-status-domain.md](mod-status-domain.md) | Design proposal for the runtime mod-status domain boundary (not implemented). |
| [protocol.md](protocol.md) | **Active** four-envelope protocol and data-flow reference. |
| [../decisions/archive.md](../decisions/archive.md) | The archived decision rows, including most of the Phase A–E record; the rows that still constrain current work are in [`../decisions/active.md`](../decisions/active.md). |
| [status.md](evolution/status.md) | Completed phase tracker and handoff state. |
| [session-workflow.md](evolution/session-workflow.md) | Historical process rules for independent phase sessions. |
| [architecture-guards.md](guards.md) | Active architecture guard list and landed guard automation. |
| [member-report-validation.md](member-report-validation.md) | **Active** rule for what a member reports and what the host bounds it against, plus the per-wire-member census. |
| [glossary.md](glossary.md) | Stable vocabulary used across phase docs and current design. |
| [templates/phase-session.md](evolution/templates/phase-session.md) | Reusable historical phase-session template. |
| [phase-a-shadow-kernel.md](evolution/phase-a-shadow-kernel.md) | Phase A history. |
| [phase-b-items-authority.md](evolution/phase-b-items-authority.md) | Phase B history. |
| [phase-c-protocol-save-switch.md](evolution/phase-c-protocol-save-switch.md) | Phase C history. |
| [phase-d-full-domain-migration.md](evolution/phase-d-full-domain-migration.md) | Phase D history. |
| [phase-e-delete-dual-architecture.md](evolution/phase-e-delete-dual-architecture.md) | Phase E history. |
| [migration-roadmap.md](evolution/migration-roadmap.md) | Completed migration route/history. |

## Phase status (history)

| Phase | Short name | Exit signal | Status |
|---|---|---|---|
| A | Shadow kernel | Replay semantic diff zero for the item slice; shadow explains known defect families; no behavior change. | Completed |
| B | Items authority | Every item fact has one authoritative write path; old tables are projections. | Completed |
| C | Protocol/save switch | New envelopes and checkpoint join pass network simulation; old item wire handlers/DTOs/NetMsg enums removed. | Completed |
| D | Full domain migration | All persistent gameplay facts live in kernel domains; epoch isolation works. | Completed |
| E | Delete dual architecture | No `Legacy`/`Compat`/shadow double-write/two authority tables remain. | Completed |

## How to use this area in a new session

1. Read [current-architecture.md](current.md) for the current design.
2. Read [domains.md](domains.md) and [protocol.md](protocol.md) for the layer you are touching.
3. For historical context, read the relevant phase doc or [status.md](evolution/status.md).
4. For evidence, read `docs/evidence/verification.md` and the matching `docs/evidence/selfchecks/` sheet.
5. Keep the architecture guard suite green.

## Non-negotiable direction

- The kernel is authoritative; Unity objects, UI, remote clones, network caches, and
  saves are projections.
- Core gameplay is expressed in typed domain models, never `Dictionary<string, object>`
  or string event names.
- Commands, events, and effects are strictly separated.
- One logical operation produces one atomic committed batch.
- The kernel is deterministic: no Unity time, transforms, random, network state, files,
  or ambient globals unless passed in explicitly.
- Every projection can be dropped and rebuilt from checkpoint + committed batches.
- No permanent legacy/compatibility path is allowed; the Phase E guard suite fails if
  a dual-architecture pattern is reintroduced.
