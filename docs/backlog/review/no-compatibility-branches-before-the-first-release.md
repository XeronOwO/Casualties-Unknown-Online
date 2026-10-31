# No compatibility branches: the first release has not happened, so nothing is owed to old shapes

- Status: Review — opened 2026-10-30 from the standing ruling (user): "the project has not released anything and the wire version does not increment, so any
  breaking change is acceptable — do not leave historical debt."
- Priority: High — every compatibility branch is a second implementation kept alive by a client that does not exist, and each one is a place the next change
  has to satisfy twice.
- Category: Architecture / release discipline / debt removal

## Design

Read `docs/design/model/item-custody.md` (the one-authority rule a leftover dual path violates) and `docs/decisions/active.md` decision 241 (the pre-release
wire baseline this ruling extends) before opening this ticket; the layer boundary a removal must not hollow out is `docs/architecture/current.md`.

`docs/decisions/active.md` carries the pre-release rule (decision 241's baseline and the standing ruling beside it): before the first official release
`ProtocolVersion.Current` is the frozen baseline and does NOT move, the handshake stays the only compatibility boundary, and a change is recorded by its
commit, its ticket and the sync-coverage matrix. Nothing in this tree has shipped, so no client, save, wire message or mod in the wild needs an old shape.

**What the ruling forbids, stated as a rule**: a branch, a type, a `[Obsolete]` member, a dual code path or a "for old data" fallback whose ONLY reason to
exist is an older version of this project. It does not forbid data normalisation that is part of the CURRENT format (a save written by today's build must
still load), and it does not forbid the game's own legacy surfaces — the game's API is not ours to simplify.

## What exists today and is debt by that rule (read, not remembered)

| Where | What it is | Why it is debt |
|---|---|---|
| `Abstractions` `ModConsoleCommand` + its registration path | the flat, pre-definition console command contract | superseded by the definition contract (`the-command-console-is-a-contract.md` decision 4); no mod has shipped outside this tree |
| `GameAdapter/GameAdapterBridge.cs` (`TryGetMarkerId` fallback) | "Legacy fallback: extremely old clone renders may still carry a domain item id from before the id-free display boundary" | a display path kept for an object shape this tree can no longer produce; it hides a real "no marker" case behind an `ItemInstanceId` read |
| the `[Obsolete]`/`deprecated`/`legacy` mentions across `src` | 99 matches, most of them prose about the game's own legacy modules or about a design that replaced an older one | not all are branches; the ticket's first job is to separate prose from code, so the sweep does not delete a sentence it mistook for a shim |

## The work

1. **Census first, delete second.** Enumerate every candidate with the reason it exists today: a call site that would break without it, or "nothing — it was
   for the old shape". The second kind is deleted in this ticket; the first kind is deleted only after its caller moves (and the caller's move belongs to that
   caller's ticket, named here).
2. **Each removal names who it was for** in the commit message ("kept for a client that never shipped", "kept for the pre-namespace id format"), so the
   history records the reason the branch existed and not only its disappearance.
3. **A gate, not a habit.** Once the tree is clean, a normative gate refuses a NEW `[Obsolete]` member or a `// legacy`/`// compat` branch that names no
   current format — the same shape as the existing architecture gates. A new compatibility branch then needs a decision recorded in `docs/decisions/active.md`
   and a published release to point at.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | The census lists every candidate with its reason; the list is in this ticket and each entry says "deleted here" or "deleted with <ticket>" |
| 2 | No `[Obsolete]` member and no `// legacy`/`// compat` branch remains in `src` whose reason is an older version of this project — the gate is the proof |
| 3 | A save written by the previous build of THIS tree still loads (the current format's own normalisation is not what this ticket removes) — one session, the archive restored |
| 4 | A mod using the old console contract fails to compile with a name that says what to use instead (the definition contract), not with a missing-type error buried in a stack |
| 5 | The gate refuses a seeded `[Obsolete]` member and a seeded `// compat` branch, and passes the tree (its matcher is pinned with both samples) |

## Limits

- "The game's own legacy surfaces" are out of scope: `UnityEngine.InputLegacyModule`, the game's `legacy protobuf messages` mentions and the KrokMP reference
  tree are not this project's debt.
- A branch kept for a REAL current constraint (a save field that older sessions of the same run wrote, the game's own two API shapes) stays, and the ticket
  says which constraint — the rule forbids debt, not documentation of a live constraint.
- The release date (the game's 2026-11-09 official build) does not change this: nothing of ours ships before that, and the rule is re-read on that day.

## The census, and what it deleted (2026-10-31)

A full scan of `src/**/*.cs` for `[Obsolete`, `// legacy`, `// compat`, `legacy fallback`, `backwards compat` and `pre-namespace` returns FIVE lines. They are the
whole census, and each one is classified here (row 1):

| Where | What it is | Verdict |
|---|---|---|
| `Abstractions/ModConsoleCommand.cs` and its registration path | the flat console contract that predates the definition contract | **deleted with** `the-command-console-is-a-contract` (the type, `IModConsoleCommandContext` and the adapter are gone) |
| `GameAdapter/GameAdapterBridge.GetRemoteProxyItemId` | a fallback to `ItemInstanceId` for very old clone renders that predate the id-free display boundary | **deleted here**: a branch for a client that never shipped, and it had NO callers at all |
| `Runtime/Session/Mods/ModContentCatalog` bare-id lookup | a comment calling the bare id "legacy" and "pre-namespace" | **kept, comment corrected**: a bare id is a CURRENT form — the game names its own items bare and `/give` accepts both forms — so the branch serves the current format, not debt |
| `Application/Kernel/KernelEnemyCombatWireMapper` | "legacy protobuf messages a version adapter still speaks" | **kept**: it names the GAME's own legacy surface, which this ticket does not own |
| `Application/Kernel/KernelLimbWireMapper` | "legacy work and forward the limb conversion here" | **kept**: the same, the game's own modules |
| `Runtime/Session/Items/ItemProjection` | "legacy `WorldItemTable` projection" | **kept**: it describes a current design decision (the kernel row is authoritative and the table is its projection), not an old version of ours |

The only line that was debt is deleted, and it is the shape the rule exists for: a fallback whose whole reason was an older build of this project.

## The gate (rows 2 and 5)

`CompatibilityDebtGateTests` refuses two shapes across `src/**/*.cs`: an `[Obsolete]` attribute (a marker that means "kept for a released caller") and a comment
announcing debt to a client, save or protocol that never shipped (`legacy fallback`, `backwards-compat`, `compat branch`, `for old clients`, `for old data`,
`pre-namespace semantics`, `older protocol version`). It deliberately PASSES the three comments above that name the game's own legacy modules. Its matcher is
pinned with both samples inside the test itself, so it cannot quietly stop seeing the shape it was written for.

## Rows 3 and 4

**Row 3** (a save written by the previous build still loads): no save code was touched by this ticket — the deletions are a dead method and a comment — so the
format a previous build wrote is today's format, and `SaveArchiveRoundTripTests` plus the salvage and contract-fix suites are the evidence it still loads. There
is no older format to produce, because nothing has been released.

**Row 4** (a mod using the old console contract fails to compile with a name that says what to use): the type is gone, so the compiler answers "not found".
Teaching the compiler a sentence would mean keeping an `[Obsolete]` type alive, which is exactly what this ticket forbids. The reader-facing half is served
instead: the mod API reference and `how-to/write-console-commands.md` both name the definition contract as the replacement, and the how-to is the page a mod
author opens first.

The ticket moves to `review/`: rows 1, 2 and 5 are judged by the census and the gate, and rows 3 and 4 carry the reasons above.
