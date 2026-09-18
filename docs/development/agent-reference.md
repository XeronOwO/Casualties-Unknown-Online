# Agent reference (binding detail moved out of AGENTS.md)

`AGENTS.md` is auto-loaded by the harness under a byte budget, so these sections live here and are
linked from it. They are BINDING detail, not background: read the matching section before working in
that area. Moved verbatim on 2026-09-17.

## Repository Layout

```text
src/CasualtiesUnknownOnline.Abstractions/  # public API; the ONLY package mods may reference
src/CasualtiesUnknownOnline.Runtime/       # DI/Logging/BepInEx/Steam/session; never game assemblies
src/CasualtiesUnknownOnline.GameAdapter/   # the ONLY project referencing game assemblies; HarmonyX
src/CasualtiesUnknownOnline.Plugin/        # BepInEx 5 entry; thin lifecycle driver
CasualtiesUnknownOnline.slnx               # solution
references/                                # game assemblies, gitignored, copied on demand
reversing/                                 # reverse-engineering workspace, gitignored
docs/                                      # architecture, decisions, backlog, feature matrices, selfchecks
AGENTS.local.md                            # gitignored local notes; never commit
```

See `docs/README.md` for the documentation index.


## Architecture & Sync Rules

**Sync model (non-negotiable):**

- `[CRITICAL]` **Local compute, remote verify/sync**: each player simulates its own actions
  with single-player feel; the host never simulates a guest's per-frame behavior. Host
  authority is limited to global world-state ownership (seed, saves, rulings).
- `[CRITICAL]` **Judge where the effect lands (user ruling 2026-09-18)**: a judgment about
  what happens to a player belongs to THAT player's client, on its own view and timeline —
  the victim judges its own hit, the actor and the target judge their own interaction gates
  (line of sight, operation preconditions), and a locally initiated operation (world-time
  acceleration) takes effect locally at once while the host arbitrates and broadcasts the
  shared state. The host owns the world it simulates and the arbitration of conflicting
  claims (first writer wins) — never the verdict on somebody else's body, reach, or timing.
  Co-op work must stay possible: exclusive one-operator locks are the exception, not the rule.
- `[CRITICAL]` **Latency is never a parameter of a judgment**: no judgment, arbitration or
  tolerance may rest on a fixed window that ignores the peer's measured RTT — a locally
  completed action rolled back by a guessed hold (the pickup window) is the smell this rule
  exists to catch. Either the deciding fact is known (a creation judged before any operation
  on it, a tombstone for a refused creation) or the judgment belongs to the client that can
  see it.
- `[CRITICAL]` **Accept-first sync arbitration — only for state the host can represent**:
  adopt and relay a guest's report first; correct only on an obvious conflict; a correction
  never blocks the player. Strict validation/anti-cheat are low priority until the feature set
  is stable. **Precondition: the host can actually adopt the reported state.** The rule exists
  to stop hard validation and player-blocking corrections, never to accept state the host
  cannot own. A report the host cannot represent — its own content set lacks the
  prefab/template, the id cannot be mapped, the domain object cannot be owned — is REJECTED,
  not accepted: it is neither recorded nor relayed. An accepted-but-unowned record has no owner
  whose death can ever retract it, so it leaks into every later snapshot and resurrects state a
  peer has already destroyed. A rejection must be VISIBLE: answer the reporter so its re-report
  fallback stops, and log/surface the concrete mismatch. Silent drops and unowned accepts are
  both forbidden.
- `[CRITICAL]` **Sync semantics, not Transforms**: synchronize game-semantic state, never raw
  Transform/GameObject state. (Transform sync fails on physics, parenting, animation, nav,
  rigidbodies, and scene loads.)
- `[CRITICAL]` **Dedicated events, not snapshots**: discrete triggers travel as dedicated event
  messages; periodic streams are only fallback/replay.
- `[CRITICAL]` **Deep sync chains**: one operation = one owner; Harmony patches are thin
  adapters; no cross-call business state in patches; reports happen only after a verified
  commit; every operation is recoverable as a complete trace.
- `[CRITICAL]` **Injected state must be authority-safe**: mutable state belongs to its owner;
  DI services are behavior/mechanism, not global mutable state.
- `[CRITICAL]` **No host migration in MVP**: host exit → session ends → guests return to lobby.
- `[CRITICAL]` **Identity, not handles**: use `NetworkEntityId` (epoch + host allocation
  counter + generation), never Unity instance IDs.
- `[CRITICAL]` Network/Steam callbacks never touch Unity objects; main-thread marshaling is
  mandatory.
- `[RULE]` Steam Lobby is discovery/roster only; game data goes through `INetworkTransport` /
  `ISession` / `IPeer` / `INetworkChannel`. Never expose Steam APIs to mods.
- `[RULE]` Host is the only save authority; guests keep local settings only.

**Structure and boundaries:**

- `[RULE]` **Strict single responsibility**: separate control plane from data plane; a class
  that both holds state and does wire I/O is a smell; split before it grows.
- `[RULE]` **One top-level type per file**; file name matches type name. Nested helper types
  stay inside their container.
- `[RULE]` **Handler pattern**: `[Handler(Key)]` + generic base class + reflection registration
  into DI, with a read-only route table built at startup. No giant `switch`. For large families
  of similar registration code (commands, handlers, providers, packets), prefer discoverable
  attribute + reflection registration over hard-coded linear registration. Keep the explicit
  list when it is genuinely clearer/more auditable; if such refactor space is found during
  development, refactor it or add a backlog item in the same cycle.
- `[RULE]` **Break construction cycles by extracting abstractions**: shrink the dependency
  surface into a standalone object so the dependency graph has no cycle. `Lazy<T>` is a second
  choice; late wiring such as `AttachXxx(other)` is forbidden. Reason about resolution chains
  with "who constructs whom".
- `[RULE]` **State belongs to its owner**: mutable state is held inside the owning object and
  exposed through narrow interfaces; DI services are not a global mutable-state repository.
- `[RULE]` Prefer switch expressions (IDE0066) over switch statements and long if/else chains.
- `[RULE]` Prefer HarmonyX; Mono.Cecil only for assembly structure changes. Feature-scan game
  APIs instead of hardcoding offsets/private fields (hardcoded offsets/private fields break on
  every game update).
- `[RULE]` Safe degradation at startup: `Compatible` / `CompatibleWithWarnings` / `Unsupported`
  / `CriticalFailure`; never let a failed patch silently run.

**Architecture gates:**

- `[CRITICAL]` **Architecture over feature/patch stacking**: when a sound architecture change
  can fully satisfy the requirement and remove the root cause, use it. Do not satisfy a need by
  piling on feature switches, narrow patches, per-case workarounds, or local hacks. A patch is
  acceptable only when it is the clearer/less-costly choice and the architecture alternative is
  explicitly documented as not justified in the same cycle. When a better, cleaner architecture
  exists, proactively overhaul instead of continuing to patch an inferior structure — local
  patches on a fundamentally wrong design either fail to fix the problem or create new ones.
- `[CRITICAL]` **Gate escapes must be real responsibility splits**: never delete
  comments/blank lines, shrink formatting, or move code between files just to pass a
  line-count or architecture gate. Extract a single-responsibility type, preserve behavior, and
  keep tests/gates green.
- `[CRITICAL]` **Hard thresholds are enforced at build time**, not by self-discipline: a
  top-level type exceeding 600 aggregate lines, more than 5 boolean state fields in one type,
  or multiple top-level types in one file must be resolved before the change, not after.
  Enforced by `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits`
  (C# port of the former `tools/check-architecture.ps1`, which no longer exists); over-limit
  exceptions are recorded in `docs/architecture-debt.json`. Run the three structure questions
  before every change: which domain does this belong to? who owns the state? will the target
  type exceed a limit?
- `[RULE]` **Architecture-first, ask before risky work**: for risky or architecture-affecting
  changes, propose a complete plan (current responsibilities, target domain model, dependency
  direction) and get consent before acting; split by domain in one pass. Test-only hardening
  and behavior-preserving extraction may proceed without prior approval. **When the existing
  mechanism has a structural problem or a clearly better architecture direction appears, the
  current backlog ticket is not the task boundary**: stop the small patching, present the full
  architecture proposal with impact and trade-offs, get confirmation, then implement it in
  stages through the full workflow. "Fix this ticket first", "no time", and "too risky" are not
  reasons to keep patching.


## Known Pitfalls

`[REF]` Detailed pitfalls list (historical blueprint, still applicable):
`docs/history/architecture-blueprint.md` §10. Keep these in mind:

- A number in a committed document (a count, a line count, a suite total) must be reproducible
  from the tree it describes, or carry its source; a figure measured from an uncommitted
  intermediate state is not a fact about the repository.
- Steam P2P is not plain LAN UDP; don't mix the two modes.
- Syncing Transforms fails on physics, parenting, animation, nav, rigidbodies, scene loads.
- Over-reliance on hardcoded offsets/private fields breaks on every game update.
- Harmony patch state leakage: a Prefix that clears an instance field must have its Postfix
  restore it.
- A Steam receive batch is all-or-nothing: catch per message and release in `finally`.
- Lobby identity must follow the actual lobby, not process history.
- Late Steam init must refresh downstream snapshots (SteamId captured as 0).
- Undefined failure modes are not acceptable: define disconnect/dropout/version-mismatch
  behavior.
- `System.Memory` hijacks `.Reverse()` on arrays; use reverse-index loops or
  `Enumerable.Reverse`.

