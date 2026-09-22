# Bilingual documentation: one tree, three layers, paired human-facing docs

- Status: Review
- Priority: Medium
- Category: Documentation / process
- Source: Owner session 2026-09-21 (language split by folder was considered and replaced by the DSH pairing contract; multilingual scope is the human-facing layer only)
- Related: `docs/README.md`, `docs/evidence/normative-gates.md`, `docs/development/agent-reference.md`

## Problem (evidence)

The repository has no documentation layer for people who read it rather than work in it. A
reader who wants to know what CUO does, how to join a session, or what is deliberately not
supported has to enter the engineering tree: `docs/README.md` maps reading intent but every
target is a contributor document, and 450-odd of the 460 Markdown files under `docs/` are
point-in-time records (selfchecks, tickets, audits) that only make sense beside the code.

Two questions follow, and the repository answers neither today:

- **Who is a document for?** An agent needs paths, type names, commands and the rule-to-gate
  map, and it acts on them; a person needs plain wording, product behaviour, and boundaries.
  A single document cannot be good at both, and copying one document into two reader trees
  writes every fact twice.
- **Which language?** The repository convention is English (`AGENTS.md`, *Document Scope &
  Classification*), which is right for a global repository but leaves the owner and any
  Chinese-reading contributor without a readable product explanation. Maintaining a full
  second tree is the cost this ticket exists to avoid.

Template: the `docs/i18n/README.md` pairing contract in the DeepSeek Harness checkout (an
external repository on this machine, not part of this tree), which runs this in production over
its own `docs/` tree. Take its mechanism, not its scope: DSH pairs the whole corpus, while
backlog, process, evidence and reference documents stay English-only here (decision below).

## Design: three layers in ONE tree

The audience split is carried by the layer, not by a directory tree. A second tree per audience
would write the same fact twice and leave one copy to rot.

## The layer is visible in the tree: the index convention

The layers are not just described in this ticket, they are readable from the directory structure.

| Layer | Index file | Language | Meaning |
|---|---|---|---|
| Agent instruction | `AGENTS.md` | English only, unpaired | the agent index and instruction file for that scope; the auto-loaded chain |
| Reference / evidence | `README.md` (plus the indexes already there) | English only, unpaired | the human entry into a layer whose documents stay English only |
| Human guide | `README.md` + `README.<lang>.md` | **paired English + Chinese** | the human index, one guide page per language |

Every documentation directory declares what it is through its index: `docs/AGENTS.md` is the
agent entry for everything under `docs/`, `docs/README.md` is the human entry, and each guide
level gets both (`docs/guide/README.md`, `docs/guide/README.zh.md`, `docs/developer/README.md`,
`docs/developer/README.zh.md`) — the Chinese index is a guide page and therefore paired, not a
translated copy of the English index. Each index also carries its language switcher, so the pair
is navigable from either side. A README states which of the three levels it belongs to and, for
the guide, what its counterpart language file is.

**A new directory needs a declared index and a declared layer.** Without that rule the layers
decay the first time someone adds a folder, which is the cheapest kind of structure to lose.

One rule decides the layer for new material: **needed in order to DO the work → an agent
document; needed in order to UNDERSTAND the product → a human document.** An agent document
never restates the human guide.

"Understand" is a spectrum, not one reading level, so the human guide carries three levels and
the word "plain" binds only the first:

| Level | Reader | Language |
|---|---|---|
| 1. Player guide | plays the mod, does not read code | plain words; no invented or technical vocabulary |
| 2. Developer guide | writes or patches code, may never have opened `src/` | readable but technical: real identifiers and mechanisms, explained rather than assumed |
| 3. Reference pointer | wants the exhaustive detail | English only, in the reference tier — the guide's job here is a reading path into it |

Level 2 is new. The original framing treated "human" as a synonym for "non-technical", which
leaves a developer who can read code but not the owner's head with nowhere to start: the
mechanisms are documented for agents (dense, identifier-first) and the product is documented for
players (no mechanisms at all). A developer-facing explanation of the architecture, the protocol
and the sync model is what turns a readable repository into a workable one.

Within a pair both sides carry **equal authority**: either may be authored first and the
counterpart is translated from it; the binding requirement is that they say the same thing.
The language switcher line is navigation, not a statement that one side outranks the other. A
guide page that needs more depth than level 2 links to the reference layer — a different layer,
never an "authoritative same-topic counterpart".

Which pages are paired is a decision per file, not per layer: the two guide levels are paired,
the reference layer stays English-only, and the Chinese side always carries a reading path into
it so a Chinese reader is never left facing a wall of English.

## Deliverables

1. The index convention above, applied to the tree: `docs/README.md` restructured so its three
   layers are stated up front, with each layer's index named; `docs/AGENTS.md` as the agent
   entry; the four guide index files created with the pages.
2. `docs/i18n/README.md` — the policy: layers, the pairing contract, the maintenance workflow,
   and the stated limit that a green gate proves the pair was confirmed consistent at those
   contents, not that the translation is accurate or well-worded (that stays a review duty,
   matching `docs/evidence/normative-gates.md` on language quality being a soft rule).
3. `docs/i18n/terminology.md` — the only source of renderings: `English | 中文 | First use |
   Never render as | Notes`. The `Never render as` column is where the wording rules become
   concrete, and it is also the single place to look up "what does this word mean".
4. `docs/AGENTS.md` — the soft-rule seat, kept deliberately small (see the constraint below).
5. The human guide layer, each topic as THREE sibling files in the SAME directory:
   `foo.md` (English) + `foo.zh.md` (Chinese) + `foo.i18n.yaml` (consistency record).
   Starting list, to be confirmed while writing:

   **Player guide (level 1)** — plain words, no implementation detail:

   | Topic | Subject |
   |---|---|
   | `docs/guide/README.md` | what CUO is, what a session looks like, reading order |
   | `docs/guide/getting-started.md` | installing the mod, hosting, joining, common failures |
   | `docs/guide/playing.md` | what a guest experiences: interactions, UI, carried players |
   | `docs/guide/limitations.md` | what does not sync, what is out of scope, known rough edges |
   | `docs/guide/design-notes.md` | why it works this way: authority model, local judging, conflicts |
   | `docs/guide/glossary.md` | everyday-language explanations of the words the mod uses |

   **Developer guide (level 2)** — technical, in the terms the code uses, written for someone who
   has not read `src/`:

   | Topic | Subject |
   |---|---|
   | `docs/developer/README.md` | how to read this repository; where to start; what to ignore |
   | `docs/developer/overview.md` | the mod's shape at a glance: mod framework, host and guests, data flow |
   | `docs/developer/protocol.md` | the four message envelopes, joining, the state stream, the handshake and version check |
   | `docs/developer/sync-model.md` | what is host-authoritative, what each client judges for itself, how conflicts are arbitrated |
   | `docs/developer/layers.md` | the Runtime / Game Adapter / Abstractions split: what you may patch, what is not promised |
   | `docs/developer/tooling.md` | which logs exist, the replay and simulation harness, how to run and filter the suite |
   | `docs/developer/saves.md` | the world archive layout and restore path |
   | `docs/developer/known-issues.md` | technical limitations, declared gaps, where they are tracked |
   | `docs/developer/reference-map.md` | the English reference layer in reading order, so the depth is reachable |

   **Reference layer (level 3)** — unchanged and English-only: `docs/architecture/**`,
   `docs/api/**`, `docs/features/**`, `docs/decisions/**`, `docs/evidence/**`.

6. A gate in `tests/CasualtiesUnknownOnline.NormativeGates.Tests` (no new toolchain):
   - every in-scope topic has all three files;
   - each side's current git blob hash equals the recorded one (`git hash-object`), so editing
     one side without re-recording the pair fails;
   - both switchers present (`English | [中文](foo.zh.md)` and the reciprocal);
   - structural mirror in order: heading depth and order, list kinds and item counts, table row
     and column counts, verbatim code blocks, and link targets;
   - the inverse check: a `.zh.md` or `.i18n.yaml` outside the human guide layer is an error;
   - the gate carries its own negative-contract self-tests, matching the other gates in that
     project.
7. `AGENTS.md` — a few lines stating the layer rule and pointing at `docs/i18n/README.md`.
   `docs/README.md` — the human guide added to the reading path.

## The `docs/AGENTS.md` constraint (verified, binding)

The harness loads the applicable instruction chain for the first request and discovers a newly
relevant nested file when a filesystem operation touches that scope; `docs/AGENTS.md` therefore
enters context when working under `docs/` and stays there. Two limits are recorded in the
loader's own README (`packages/context/agent-instructions/README.md` in the DSH checkout):

- discovery follows **file operations, not shell navigation** — a shell `cd` triggers nothing;
- nested files share the ONE 65,536-byte workspace budget with the root `AGENTS.md`, which is
  already at its ceiling here (the reason `docs/development/agent-reference.md` exists at all).
  A large `docs/AGENTS.md` would push the root file out of the baseline, which is a worse
  failure than the one it fixes. Keep it to rules of that subtree only, and link the detail.

## Maintenance workflow (the cost this ticket is buying down)

When either side of a pair is edited: patch the counterpart minimally against that side's diff,
then re-record both blob hashes in the `.i18n.yaml`. **Re-translating a whole document is
forbidden** — the recorded hashes recover the last confirmed text of either side, which is what
makes the minimal patch possible. Routine work is done directly by the working agent after
loading `docs/i18n/terminology.md`; there is no separate translation tool, repository or build
step.

## Verification

- `dotnet test CasualtiesUnknownOnline.slnx` green with the pairing gate and its negative
  contract cases present.
- Edit only the English side of one pair, leave the record alone, and the gate must name that
  pair — the check that the mechanism bites.
- Every tutorial step in the human guide is executed once against the current build; the guide
  states no behaviour the code does not have.
- No implementation identifiers in the level-1 player guide: no `src/` path, type name or test
  anchor. The level-2 developer guide is technical by design and uses the real names.
- The `docs/handoff/` removal is confirmed: the directory is gone and no `README` or index points
  at it (owner ruling 2026-09-21: removed by hand, with no gate added — the rule that handoff
  content never lands as a file stays a review duty).

## Non-goals

- No translation of backlog, evidence, selfchecks, audits or decision records.
- No translation of the level-3 reference layer (owner ruling 2026-09-21). The Chinese entry
  point is the `docs/developer/reference-map.md` page: what the layer contains, the reading order,
  and what each document answers. A Chinese reader reaches the depth through that path instead;
  translating the layer would put every architecture and protocol edit under a second maintenance
  obligation for documents that are read while working in the code.
- No directory tree per audience, and no separate translation repository.
- No agent document in a pair: agent documents are not "the other language version" of a human
  guide, they are a different layer with different content.
- No `.zh.md` outside the human guide, meaning the two paired guide levels, their index pages
  and the pages those indexes introduce.

## Session handoff disposition (final: delete, do not re-file)

Owner ruling 2026-09-21: both files are temporary working notes rather than documents. Each was
checked for content that exists nowhere else, and there is none:

- `docs/handoff/session-control-convergence-review-findings.md` — its findings are already in
  `docs/backlog/review/session-control-convergence.md`: MAJOR-1 and MINOR-3 by name, MAJOR-2 and
  MINOR-1 in substance (the budget correction and the honest row-4 reading).
- `docs/handoff/medical-minigame-concurrency-audit.md` — its conclusion and per-kind evidence
  table are already the `Native per-kind semantics` table of
  `docs/backlog/review/concurrent-medical-operations.md`; the remaining detail is a `reversing/`
  line:line lookup anyone can repeat, which is the point of that tree never being edited.

Keeping them would be a second, unmaintained home for facts that already have one — the failure
the index convention exists to prevent. **Action: delete both files, then remove the now-empty
`docs/handoff/` directory.** No replacement file; the owning tickets stay authoritative.

State 2026-09-21: not executed. The working session had no file-deletion or command-execution
tool available, so the removal cannot be performed from there. Note before doing it by hand:
`.gitignore` excludes `docs/handoff/`, so these two files were never tracked by git and there is
no history to recover them from — the content check above is the only safeguard, and it has been
done (both files' full text was read and compared against the owning tickets).

State 2026-09-22: executed — `docs/handoff/` no longer exists, and no README or index points at it.

## What landed (2026-09-22)

- **The layer rule where it is read.** `docs/README.md` now opens with the three layers and names each
  layer's index; `docs/AGENTS.md` (new, 711 bytes) is the agent entry for the subtree and links the
  detail; the root `AGENTS.md` states the rule in four lines. The nested file stays small because the
  gate pins its size, and the tracked instruction chain's total with it.
- **The pairing policy.** `docs/i18n/README.md` carries the layer rule, the paired scope, the
  three-file pair, the switchers, the structural mirror, the record, the maintenance workflow and the
  stated limit. `docs/i18n/terminology.md` (5,974 bytes, 52 rows) is the only source of renderings, and
  its `Never render as` column turns a wording rule into something concrete rather than implied.
- **Fifteen paired topics, 45 files.** Six player pages under `docs/guide/` and nine developer pages
  under `docs/developer/`, each as `foo.md` + `foo.zh.md` + `foo.i18n.yaml`. The Chinese side is a guide
  page, not a translated copy of an agent document: it carries its own reading path into the
  English-only reference layer, so a Chinese reader is never left facing a wall of English.
- **The gate.** `HumanDocsPairing.cs` (562 lines, pure functions) plus `HumanDocsPairingGateTests.cs`
  (291 lines, 9 cases) in `tests/CasualtiesUnknownOnline.NormativeGates.Tests`, with no new toolchain:
  pair completeness with a discovery floor, recorded blob hashes through `git hash-object`, both
  switchers, the block-sequence mirror (item and cell shapes counted, escaped pipes read as content)
  with link localization, the inverse scope check over the repository's file set, the workspace
  instruction-budget check, and synthetic contract cases that pin the checker against every break class.
- **Scoped pairing, enforced.** Which pages are paired is a decision per file: only the two guide
  levels are paired, and the inverse check refuses a `.zh.md` or `.i18n.yaml` anywhere else under
  `docs/`. `docs/README.md` stays English only as the human entry into all three layers and points a
  Chinese reader at `docs/guide/README.zh.md`.

### Verification

- `dotnet build CasualtiesUnknownOnline.slnx` (inside the suite run): 0 warnings, 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx`: exit 0.
- Normative gates: 128 total, 9 of them this gate's — 127/128 before the delivery-checklist boxes are
  filled and 128/128 after them.
- Full suite with build: 3795/3795.
- Real-tree control 1: one line appended to `docs/guide/playing.md` with the record untouched turned
  `EveryPair_MatchesItsRecordedBlobHashes` red and named the pair (recorded `9a9d2273…`, file hashing
  to `2842c5eb…`); restoring the file made the hash equal the record again.
- Real-tree control 2: a stray `docs/evidence/stray-check.zh.md` turned
  `NoPairedArtifact_LivesOutsideTheGuideScope` red with that path, and was removed afterwards.
- The removal recorded above: `docs/handoff/` does not exist.

### Independent review round (2026-09-22)

A separate review session (fresh context, frozen tree, read-only, report outside the repository)
returned 0 blocker / 4 major / 8 minor / 3 nit. All were fixed in this commit:

- **major — the mirror compared block kinds, not content.** Reordering two list items, or swapping two
  table cells, passed. The check now also counts each item's and each cell's language-independent
  shape (links, inline code, bold spans) and reads reference-style link definitions, and the policy
  page states the residual limit instead of implying a stronger one: translated words are never
  compared, so two same-shaped items or cells can still trade places.
- **major — reporting stopped at the first difference**, so an early insert or drop let the tail line
  up at shifted indices and replaced a real break with a mislocated message. The comparison now trims
  the common head and tail, reports the differing window (up to four markers) and always states the
  length difference.
- **major — an escaped pipe inside a table cell was counted as a column separator**, which would have
  reported a column mismatch where the rendered table has none. Cell splitting now honours an escaped
  pipe and inline code spans.
- **major — the Chinese pages broke `Never render as`**: 客户端 for guest, 视图 for projection, 副本
  for backup, 裁决 for arbitration. Every instance was corrected against `terminology.md`; the table
  gained `save`, `view`, `client` and `online panel` entries so the remaining uses (client-side
  prediction) have a legal rendering, and the panel name is now consistent across pages.
- **minor** — stale numbers in this ticket and the fact sheet, a Chinese-side addition the English side
  did not have, the missing `save` entry, the instruction-budget check excluding the machine-local file
  that competes for the same budget (it now includes it, and `docs/AGENTS.md` was rewritten to 711
  bytes so the workspace chain fits at 65,375 of 65,536), the configuration location in
  `getting-started`, the missing reference-style link coverage, and a comment separating the two
  failure sources of the hash check.
- **nit** — `docs/AGENTS.md`'s scope wording, the panel-name inconsistency, and a typo in the
  `docs/README.md` layer table.

### Limits

- No runtime code and no wire change: nothing in this cycle says anything about host or guest
  behaviour, and no such claim is made.
- The gate compares hashes and Markdown structure, never meaning: a re-recorded pair with a sloppy
  counterpart passes it, and wording stays a review duty.
- The "every tutorial step executed once against the current build" item is **not** verified in this
  cycle — no game client was run — so the player-facing steps stay part of the user's acceptance pass.
