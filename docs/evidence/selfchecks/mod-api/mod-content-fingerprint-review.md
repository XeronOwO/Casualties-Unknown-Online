# The content fingerprint — independent adversarial review

Tier: FULL (protocol, save format, architecture, cross-module and user-visible behaviour).
Scope: the uncommitted change set on `master` at `HEAD c14a7e05d776f33b49deb5776c0e1387305e7ae3` —
48 files, +1041/−131 (the fingerprint ticket, decision 254). Reviewed in a fresh context against the FROZEN
tree: no file was modified, `dotnet format` was not run, and the one scratch probe this review wrote was
deleted again (verified: `git status --porcelain` is the same 48 entries, `git diff --cached --shortstat` is
still `48 files changed, 1041 insertions(+), 131 deletions(-)`, no untracked file).

**Verdict: no blocker. 3 major, 5 minor, 5 nits.** Every figure the change states reproduces except one
(the focused run in the self-check §4). The mechanism itself is sound where it claims to be: the encoding is
injective, the readers are the two the change names, both load paths reach the comparison, and the mode row
above it makes the host-only `IsStateBearing` choice correct. The majors are a silent read-view regression,
an acceptance row the mechanism does not satisfy, and a refusal with no recorded user ruling.

---

## A0. The premise, checked before anything else

The cross-player entry here is **JOIN ADMISSION** — the host's judgement of the member's handshake. There is
no new player gesture in this change (no input, no UI, no message the player sends differently).

**(1) Native counterpart: there is none, and the check is CUO's own invention whose scope must be argued.**
The game has no multiplayer, so it has no join admission for a host to mirror; CUO's whole handshake is
additive. What the content row *does* inherit is a CUO contract, not a native call site: the mod's own
`NetworkMode` (`docs/en/reference/mod-api.md` "Handshake consistency": "any mode | same id but a different
`NetworkMode` while either side is state-bearing | **reject**"), which the row claims to follow ("**Content
parity** is judged BY the NetworkMode contract rather than apart from it"). So the row's *shape* is inherited
and defensible; its *new refusal* is CUO's own invention and, per the workflow rule, needs the user's ruling
rather than an argument in a self-check — see MAJOR 3. I could not search `reversing/` for a native call
site: that tree is gitignored (grepping the directory returns nothing) and I had no file path to name.

**(2) Information the handshake cannot carry, and the fallback at the thing the decision is about.**
The decision is "do the two peers materialize the same content?". The handshake carries a **digest of the
ADDRESS SET** (mod id, content id, kind, schema version) and nothing else, and the code then treats
`equal digest` as `equal content` (`HandshakeHandler.cs`: `if (string.Equals(hostFingerprint,
guestFingerprint, StringComparison.Ordinal)) { return true; }`). That is an **"assume equal"** at exactly the
thing the decision is named after: a definition's other members may COMPUTE their values (decision 251), so
two peers with the same four-tuple and a different computed value are admitted in silence. This is not a
hypothetical shape — it is the ticket's own motivating example, quoted from the entry this cycle deleted
(`docs/backlog/todo/mod-content-fingerprint.md`, deleted): "With code-driven definitions two copies of the
same mod version can materialize different content — **a local config changes a weight** — and nothing
notices." A weight is a member value, not an address field. See MAJOR 2.
A second, narrower fallback: `null` on the wire is read as "this mod registered no content" (`ModInfoMsg.cs`,
`HandshakeHandler.Declared`), which is indistinguishable from "this peer did not report" — see MINOR 2.

**(3) Two entries claiming one case, and order instead of the item's own data: checked, clean.**
For one mod id, four rows judge the same mod — missing/version, mode equality, permissions, content, native
binding. They are not competing: each tests the item's own data, and none is silently shadowing another,
because (a) the guest list is rejected for duplicated ids before the loop
(`HandshakeHandler.cs`: `if (string.IsNullOrWhiteSpace(info.Id) || !guestIds.Add(info.Id))` → reject) and the
host registry dedups ids (`ModRegistry.cs`: "duplicated mod id {Id} — the later declaration is skipped"),
so `guest.FirstOrDefault(g => g.Id == hostMod.Id)` is not an arbitrary "first match"; and (b) the mode row
`if ((hostStateBearing || guestStateBearing) && guestInfo.NetworkMode != hostMod.NetworkMode)` runs before
the content row, which makes `IsStateBearing(hostMod.NetworkMode)` — the host's mode only — equivalent to
"either side": after that row the two modes are equal, or both are non-state-bearing, in which case the host
mode answers the same. The content row is therefore unreachable exactly where an earlier row already
refuses, which changes only which log line is printed, never the verdict.

**What the change makes a HOST do that it did not do before, and can that refuse a legitimate session?**
Yes. A state-bearing (`RequiresAllPlayers` / `Synchronized` / `Authoritative`) mod whose registered content
set differs now **rejects the join**, where before the member was admitted. A legitimate example: a
state-bearing mod whose content set depends on local configuration or on which optional pack the player
enabled. The member gets no explanation — the refusal is the handler's bare `return` (`HandshakeHandler.cs`:
`if (!CheckModConsistency(sender, msg, ctx)) { return; }`), the guest retries every 1 s
(`SessionPeerMaintenance.cs`: `HandshakeRetryInterval = 1f`, no give-up path), and the only trace is the
host's log line. That is the existing shape of every consistency refusal, but it is a new cause of it.

---

## Findings

### MAJOR 1 — the store keeps the registrations of a mod the lifecycle refused to load

`src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModLifecycle.cs:193-204` (frozen tree):

```
RegisterDeclarations(d, context);
instance.Bind(context);
instance.Initialize();
instance.Start();
_catalog.Add(new LoadedMod(d.Manifest, instance, context));
...
catch (Exception e)
{
    _log.LogError(e, "[Mods] {Id} failed to load — skipped, the other mods continue.", d.Manifest.Id);
}
```

the read view, same file line 143: `internal IReadOnlyList<ModContentRegistration> Entries => _contentStore.Entries;`

Content is filed into the store at line 193 (and by any `TryRegister` inside `Bind`), i.e. **before** the
mod can fail; a throw in `Bind`/`Initialize`/`Start` skips `_catalog.Add` but nothing withdraws the entries.
Before this change the same property was computed from the catalog and therefore excluded them — the removed
line, from the staged diff:

```
-	internal IReadOnlyList<ModContentRegistration> Entries =>
-		[.. _catalog.Mods.SelectMany(m => m.Context.ContentRegistrations)];
```

Consequences, all reaching production paths, none of them new-and-intended:
- `IModContentControl.Entries` (the mod-visible read view the change says is "unchanged") now reports a
  failed mod's content for the whole process — mods are discovered once (`_discovered`), so it never clears.
  Readers: `ModContentCatalog.Entries` (the console resource vocabulary / completion surface),
  `ModContentOwnerQueryAdapter`, `ModContentResourceLocationSource`.
- `ModContentFingerprint` includes it, so the handshake digest and the manifest record content the process
  never materialized — asymmetrically, if only one peer's `Bind` throws, a state-bearing join is refused with
  "materializes different content".
- `ModContentBinder.BindAll` (`ModContentBinder.cs:61-69`) is protected by `mods.CurrentModManifests`, so it
  skips them — but with a line that names the wrong cause: "content {ModId}/{Id} was skipped because its mod
  is not a shared-content network mode." That message is now reachable for a mod that is not loaded at all.

Verified by reading the load path and the old projection; the behaviour is untested — the only throwing
fixture, `tests/CasualtiesUnknownOnline.Tests/Mods/TestThrowingMod.cs`, throws in `Update`, i.e. after
`_catalog.Add`, and no fixture throws in `Bind`. Direction of a fix: withdraw the mod's entries when the load
fails (or key the read view on loaded mods) rather than letting the store outlive the decision, plus one case
with a `Bind` that throws after registering.

### MAJOR 2 — the acceptance row says "materialize different content"; the code compares the ADDRESS, so the ticket's own case stays silent

`docs/backlog/review/mod-content-fingerprint.md:60-61` (the row, moved verbatim from the parent ticket):

> - **Two clients that materialize different content under the same id and the same mod version are reported
>   as a named mismatch rather than silently accepted.**

The comparison is `ModContentFingerprint.Lines` (`ModContentFingerprint.cs:38-39`): the address
(`TryGetCanonicalId` → `namespace:id`, else `Definition.Id`), the kind and the schema version. A differing
computed member value under an equal four-tuple produces an **equal digest**, so it is silently accepted —
the exact drift the ticket was filed for, whose example is "a local config changes a weight" (quoted in A0).
The ticket's record claims the opposite: "nothing is left to develop here, and the acceptance batch is
pending" (`docs/backlog/review/mod-content-fingerprint.md:4-5`), while the row as written cannot pass at that
batch for the scenario the parent ticket used to justify it.

Worse, the reader-facing page uses that very scenario as the case the new parity **catches** —
`docs/en/reference/mod-api.md:906-908`:

> An equal id and version does not imply it: a declaration may compute its members, so two copies of one mod
> version can register different content (a `[ModContent]` class that reads local configuration, a definition
> whose id depends on the machine), and that is the divergence the version rule exists to prevent.

Only the second half of that parenthetical (an id that depends on the machine) is caught; a class that reads
local configuration to compute a VALUE is not. The following sentence does state the exclusion ("What the
fingerprint deliberately excludes is a computed member's own VALUE"), so the page contradicts itself two
lines apart, and the example in the reader's path is the misleading half. Decision 254 records the exclusion
correctly — the defect is the acceptance row plus that example, not the mechanism. I rank this major rather
than blocker because the limitation IS recorded (decision 254, mod-api paragraph, self-check §2) and only the
row and the example overstate it; but it is the one item I would not let the acceptance batch judge as
written.

### MAJOR 3 — a new user-visible refusal with no recorded user ruling, and the one sentence that could have covered it was dropped in the move

`HandshakeHandler.cs:381-392`: a state-bearing mod whose content differs is **refused**; the local-surface
modes only warn. The ticket's acceptance row (quoted above) says the mismatch is "**reported** as a named
mismatch rather than silently accepted" — a refusal is stronger than a report, and it is a user-visible
behaviour change: a session that was admitted is now rejected (A0(3)). The change's own record treats the
verdict as inherited ("No policy knob … A knob would let a host admit content it cannot arbitrate, which is
exactly the state the version rule refuses" — self-check §3), which is an argument, not the user's ruling the
workflow requires for a new user-visible behaviour ("The `User approval` step above covers THIS scope,
including every user-visible behaviour change it introduces: a parent ticket's frozen direction, a handoff's
work order or an existing implementation are never that approval").

The move itself removed the only sentence that could be read as authorizing it. The deleted
`docs/backlog/todo/mod-content-fingerprint.md` carried, under "What this ticket owes":

> It has to be decidable what a peer can honestly assert about another peer's content: ids, kinds and schema
> versions are stable, while a computed member's value is not hashable in general — the ticket starts by
> naming the subset both sides can agree on, and **refuses (with a named mismatch) the rest** rather than
> hashing something unstable.

The replacement ticket keeps the acceptance row, the priorities and the non-goals but drops that whole
paragraph (`docs/backlog/review/mod-content-fingerprint.md` "What this ticket owed" reduces it to "A decision
on WHERE the comparison happens and what it hashes — taken; see *What landed*"). So nothing in the surviving
record says a *refusal* was owed. Disposition is the user's: approve the refusal (and say so in the row), or
make it warn-and-admit like every other local-surface row. Either way the row and the mod-api table must say
which verdict is owed.

### MINOR 1 — the self-check §4 focused figure does not reproduce

`docs/evidence/selfchecks/mod-api/mod-content-fingerprint-selfcheck.md:88` claims:

| Focused (the touched areas) | `--filter "…Tests.Mods\|…Tests.Persistence\|…Tests.Protocol"` | 959/959 |

Reproduced on the frozen tree (same three OR'd terms, `--no-build` after a clean build):
**978/978 passed**. The delta is exactly the 19 new cases, i.e. 959 is the pre-change figure (or a stale one),
so the row is not the output of a run against this tree. Every other figure in §4 reproduces exactly:
`ModContentFingerprintTests` 9/9, `ModHandshakeTests|ModContentFingerprintTests` 49/49, the save-side trio
26/26, the wiring+wire trio 33/33, behaviour 4899/4899, gates 572/572 (checklist gate excluded by the brief),
build 0 warnings / 0 errors (forced `--no-incremental` rebuild).

### MINOR 2 — "null = registered no content, never unknown" cannot tell "did not report" from "has none"

`ModInfoMsg.cs` (wire member) and `HandshakeHandler.Declared(string? fingerprint) => fingerprint ?? "none"`.
The change's claim 4 is explicit that null means "this mod registered no content" on BOTH sides, "never
unknown"; the self-check §5 uses the same reading for an older client ("a mod that reports no fingerprint at
all (an older client) reads as 'none'"). Two opposite readings of one encoding are reachable:
- host has content, member reports nothing → **refused** ("materializes different content") although the
  member may simply not carry the field;
- host has no content, member reports nothing but DOES register content (its mod DLL is the same; only CUO is
  older) → both read `null` → **admitted**, and the divergence the row exists for goes unnoticed.
Reachable only across builds, which pre-release decision 241 does not detect (the protocol number is frozen),
so it is a narrow hole rather than a live defect — but the manifest half of the same change reads its
`string.Empty` as "unknown" (`SaveArchiveReader.NoteContentMismatch`: "empty on EITHER side is 'unknown' and
is not compared"), so two "absent" encodings in one change carry opposite meanings and the mod-api page
states only the wire one.

### MINOR 3 — the late-registration limit is not where the reader relies on it

The self-check §3/§5 state it ("No lifecycle re-check. A mod may register content after the handshake
(`TryRegister` is callable from `Update`) …", "A content set that changes after the handshake is not
re-compared"). Neither `docs/en/reference/mod-api.md` nor `docs/zh/reference/mod-api.md` says it: the page a
mod author reads only says the host compares the fingerprint per mod id at the handshake. A mod that
registers from `Update` therefore reads the page, sees the parity rule, and cannot learn from it that the
registration it does one frame later is compared by nobody. (`docs/AGENTS.md` §1: "Everything a reader needs
belongs in the two blocks, in both languages.")

### MINOR 4 — one production load path never supplies the live fingerprint

`src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldLibraryService.cs:255`:

```
var opened = worlds.LoadBackup(worldId, backup, new WorldLoadOptions { VerifyChecksums = true });
```

This is the player-initiated "restore this backup" pass: it validates, then promotes the archive into
`live/`. With no `ExpectedContentFingerprint`, `NoteContentMismatch` returns immediately, so a snapshot cut
under a different content set is validated and installed without a word, and the §6.2 warning appears only at
the next Continue (which does supply the value, `WorldRestoreApplier.cs:168-173`). The other two production
readers are covered: `WorldRestoreApplier` is the only caller that passes the value, and `Fingerprint` is a
64-hex SHA-256 even for an empty registry (`e3b0c442…`, reproduced by probe), so it can never be empty in
production — the load-side comparison is not silently disabled by accident on the Continue path.

### MINOR 5 — the log half of self-check §1 row 6 is not pinned

§1 row 6 claims "both lines name the mod and both fingerprints". `ModHandshakeTests` asserts the mod id, the
phrase "different content" and the verdict ("rejected" / "admitted") — never that either digest appears, even
though `RecordingLoggerFactory` already captures the formatted line. Dropping both fingerprint arguments from
either `LogWarning` call would leave the suite green.

### Nits

1. **`docs/evidence/selfchecks/mod-api/mod-content-fingerprint-selfcheck.md` §5** lists five limits and omits
   the one MAJOR 2 rests on: a differing computed VALUE under an equal address is not compared. Decision 254
   states the exclusion, so the gap is in the limits list a reader would quote from.
2. **The fingerprint is not cached** — `ModContentStore.Fingerprint` and `.ByMod` recompute
   sort + SHA-256 on every read, and `Lines` re-invokes the live `Definition.Id`/`Kind`/`SchemaVersion`
   getters (`ModContentFingerprint.cs:38-39`). My scratch probe confirmed the same store reports a different
   digest after the definition object's `Id` changes: one store, two values, so a process can hand the
   handshake one digest and the manifest another and then report `ContentMismatch` against its own cut. The
   contract does say a mod registers a definition it does not mutate afterwards (`IModContentDefinition.cs`),
   so this is a framing nit against claim 1's "ONE computation serves two readers" — there is one canonical
   FORM, not one computation. Cost is negligible (≤1024 entries per mod, once per handshake attempt, once per
   cut).
3. **`docs/en/reference/glossary.md` / `docs/zh/reference/glossary.md`** render the term as "the digest of
   the address of every content entry **a mod registered** … what a cut records in its manifest", which
   merges the per-mod value the handshake compares with the whole-set value a manifest records. A reader
   could believe the manifest stores one digest per mod.
4. **Section citations**: `WorldLoadOptions.cs`, `DamageReport.cs` and `SaveArchiveReader.NoteContentMismatch`
   say "§6.1's rule for a build difference"; the rule and the new doc bullet are in **§6.2** ("Version and
   build gating", `docs/architecture/save-archive-format.md:565-573`). This copies the file family's existing
   loose usage (`NoteProtocolMismatch`, `SaveManifest`, `SalvageSession` all cite §6.1), so it is a family
   nit, not one this change invented.
5. **`SaveArchiveDamageTests.ContentMismatch_OpensInRepairModeWithAWarning`** never sets `RepairMode`; the
   reader-level case runs with it off (the production caller sets it on, and the Continue seam is covered
   separately by `WorldRestoreReportTests`), so the test's name claims a mode it does not exercise.

---

## A/B. Every copied claim and every cited number, checked against the current tree

| Claim / restatement | Verdict |
|---|---|
| 1. One canonical text, length-prefixed, ordinal sort, newline-joined UTF-8, lowercase hex SHA-256 | **Holds.** `ModContentFingerprint.cs:39-48` (`$"{Field(entry.ModId)}\t…"`, `lines.Sort(StringComparer.Ordinal)`, `string.Join("\n", lines)`, `Encoding.UTF8.GetBytes`, `SaveArchiveChecksum.OfBytes` → `value.ToString("x2")`). Readers: `ModHandshakeListProvider`, `HandshakeHandler`, `WorldCutWriter`, `WorldRestoreApplier` — the two the change names, plus the handler on the session side. |
| 2. The address set is the honest subset; scope is what was REGISTERED | **Partly holds, and the gap is MAJOR 2.** Registered-vs-bound is argued (self-check §2) and true — the binder's own pass is separate. Address-vs-VALUE is not covered by "honest subset": equal addresses do not imply equal content, and the acceptance row speaks about content. |
| 3. The length prefix is load-bearing | **Holds.** `ModRegistry.cs:50` refuses only a blank id; `ModContentPolicy.IsValidKind` only non-whitespace (≤64). Hand-derived counterfactual for the test's pair: without prefixes, `m\ta\titem\t1\nn` + `b` + `item` + `1` renders exactly as `m`+`a`+`item`+`1` and `n`+`b`+`item`+`1` joined by the same newline — two content sets, one text. My probe reproduced the fixed behaviour (distinct digests) plus two collisions, both on inputs the rails refuse: `namespace="ns", id="b"` vs `id="ns:b"` (needs `:` in a path — `ContentId.IsValidPath` allows only `[a-z0-9_.-]`) and `namespace=""` vs `null` (`IsValidNamespace("")` is false, and `ModRegistry` skips such a mod). `ModContentStore.Add` is internal with the adapter as its only production caller. |
| 4. Wire member, null = "no content" on BOTH sides; per-mod-id comparison, both sides list only; same `IsStateBearing` rule; both log lines name the mod and both fingerprints | **Holds in code**, with MINOR 2 (null vs unreported) and MINOR 5 (log half unpinned). `HandshakeHandler.cs:281` computes `ByMod` once per handshake; `:324-331` calls the check for every host mod the member lists; `:381-391` names the mod and calls `Declared` on both sides; `:437-438` is the same `IsStateBearing` predicate the version row uses. |
| 5. Manifest records the whole-set fingerprint (replacing `string.Empty`); `WorldLoadOptions.ExpectedContentFingerprint` carries the live value; `NoteContentMismatch` reports `ContentMismatch` in repair mode, never refuses; empty on either side is not compared | **Holds** (`WorldCutWriter.cs:303-313` is the ONLY production `SaveManifestMeta` construction site; `SaveArchiveReader.cs:206-229` reports, `Finish` refuses only on File-scope entries with repair mode off; `DamageReport.EntryReason.ContentMismatch` is appended last). Two notes: the report is emitted in ANY mode, not only repair mode — in production the only caller that passes a value always sets `RepairMode = true`, so the claim is true where it matters; and MINOR 4 covers the third load path. `WorldBackupPromotion.cs:203` and `WorldRepository.cs:394` copy the field through promotion/rewrite rather than dropping it. |
| 6. Leaf structure; adapter keeps every rail; `IModContentControl` unchanged; new provider; `ModRegistry` untouched | **Holds with MAJOR 1.** `ModContentStore` has no constructor parameter; `ModComposition.cs:33-37` registers the store, the interface and the provider in that order; `ModContentAdapter.cs:23-76` keeps permission, null, id, kind, schema, duplicate, cap and both log lines; `ModRegistry.cs` is untouched in the diff and still returns fresh `ModInfoMsg` objects (`:142-150`), so the provider's in-place assignment is safe. `IModContentControl` has its one member; what changed is what it CONTAINS (MAJOR 1). The public-and-internal-write-half shape matches the existing `ModStatusStore` / `ModBuildingRuntimeStore` precedent. |
| 7. `WorldSnapshotPayload.ContentFingerprint` deleted as a member nothing read | **Holds.** Exactly three construction sites exist (`WorldCutWriter.cs:91` + two test files); `WorldSnapshotEncoder` reads members explicitly (no reflection), and `SaveArchiveJson.Options` sets no strict unmapped-member handling, so archives written when the member existed still read. |
| 8. No `Abstractions` member changed; baseline does not move; protocol number does not move pre-release (decision 241) | **Holds.** No `src/CasualtiesUnknownOnline.Abstractions/**` file and no `docs/contracts/abstractions-api-baseline.txt` in the change set; `ApiSurfaceGateTests` green in the 572. The wire gained `[ProtoMember(6)]` and `ProtocolVersion` is untouched. |
| 9. Build 0/0; behaviour 4899/4899 (was 4880, +19 in 6 files); gates 572/572 with the checklist gate excluded; 19 `[Fact]`s added | **Holds except the focused figure (MINOR 1).** Forced rebuild: 0 warnings, 0 errors. Full run: `4899/4899` (net48) and `572/572` (net8.0) with the checklist gate excluded. Diff count: exactly 19 added `[Fact]` lines, 0 removed, decomposing 9 (`ModContentFingerprintTests`, new file) + 5 (`ModHandshakeTests`) + 2 (`SaveArchiveDamageTests`) + 1 (`WorldRestoreReportTests`) + 1 (`WorldSaveCaptureTests`) + 1 (`ModContentTests`) = 19, in 6 files, exactly as §1 lists. 4899 − 19 = 4880 matches the previous cycle's recorded figure and 573 − 1 = 572 matches the gate count. The fingerprint's own line pin is correct: `"16:test.fingerprint\t1:a\t4:item\t1"` (16 = `test.fingerprint`.Length, schema version last and unprefixed because an int needs no delimiter). |
| 10. Docs: both language blocks, terminology, alignment, decision 254, the move, the amended records | **Holds.** `git hash-object` reproduces all four new hashes in `docs/standard/alignment.txt` byte for byte (`9335e5e6…`/`7d832c10…` glossaries, `b03b53ad…`/`abbbc450…` mod-api). `standard/terminology.txt` gained `content fingerprint` before first use; both glossary blocks and both mod-api blocks changed; decision 254 is in `docs/decisions/active.md`; the ticket sits in `review/` with `- Status: Review …` (BacklogIntegrityGateTests green); no reference to the deleted `todo/mod-content-fingerprint.md` survives anywhere in the repository (13 hits, all `review/`); the parent ticket, the typed-registration ticket and two older self-checks were amended rather than left stating `string.Empty`; `docs/architecture/save-archive-format.md` gained the §3.2 field meaning and the §6.2 bullet (correct sections). The date "landed 2026-10-10" matches today. |

---

## C. The mechanism, read rather than paraphrased

- **`CheckContentParity` is reachable for every mode.** It sits inside the per-mod loop for every host mod
  the member lists, and the modes split cleanly: `RequiresAllPlayers`/`Synchronized`/`Authoritative` →
  refuse, `ClientOnly`/`Cosmetic`/`HostOnly` → warn and admit. A host mod the member does NOT list never
  reaches it — correct, there is no counterpart to compare, and the mode row already decided who may lack
  what.
- **`IsStateBearing(hostMod.NetworkMode)` is safe given the mode row above it** — demonstrated in A0(3).
- **`NoteContentMismatch` is reachable on both reader paths**: `SaveArchiveReader.cs:101` (live) and `:447`
  (backup), both handed the same `options` object, and the recovery path re-enters with that object
  (`WorldRestoreApplier.cs:204` passes the same `options` into `WorldRestoreRecovery`).
- **`ExpectedContentFingerprint` cannot be empty in production** on the path that compares: it is
  `IModContentFingerprints.Fingerprint`, a 64-character digest even for an empty registry (probe: the
  empty-set value is `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`, matching §2). The
  Restore-library path simply never passes it (MINOR 4).
- **Test bodies pin what they claim.** `StateBearingModWithEqualContent_Accepted` builds the expected digest
  in a SECOND store and compares — a real equality, not the same object; the rejected/admitted cases assert
  the recorded log line, not just the verdict; `ContentMismatch_OpensInRepairModeWithAWarning` asserts the
  report entry AND both values inside its detail; `EqualOrUnknownContentFingerprints_AreNotDifferences` covers
  equal, unknown-manifest, and unknown-caller; `Cut_RecordsTheContentSetThisProcessMaterialized` reads
  `contentFingerprint` back out of the live manifest JSON and shows it move with a later registration;
  `AContinueUnderADifferentContentSet_…` goes through the real Continue seam and asserts
  `Applied` + not-`Clean` + the reason name in the details. `Entries_AreASnapshotThatSurvivesALaterRegistration`
  pins the copy the interface promises.
- **Verified-not-falsified strength:** the fingerprint touches only `Id`/`Kind`/`SchemaVersion`, never another
  member, so a declaration whose computed member throws (the `TestDeclaredContentMod` fixture has one:
  `public float Weight => throw new InvalidOperationException(...)`) cannot break the handshake digest or a
  cut.

---

## D/E/F. Reference integrity, what the change does not say, contract shape

- No dangling path anywhere: the deleted ticket's old path has no surviving reference; the new self-check has
  its MANIFEST row (`docs/evidence/selfchecks/MANIFEST.md:322`); every row still resolves (the MANIFEST gate
  and the backlog cross-reference gate are green in the 572).
- **Consequence of this report (not a finding against the change):** writing
  `docs/evidence/selfchecks/mod-api/mod-content-fingerprint-review.md` makes
  `SelfcheckManifestGateTests.EverySelfcheckFile_HasExactlyOneManifestRow` fail with
  "1 self-check file(s) have no MANIFEST row: mod-api/mod-content-fingerprint-review.md" — the gate requires a
  row per file and this review's brief forbids editing `MANIFEST.md`. Measured after writing this file (gate
  project, whole run): 573 total, 571 passed, exactly two red — that one and
  `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`, which the brief excludes as deliberately
  unchecked until the cycle closes. With this report absent the change set is 572/572. The cycle must add the
  MANIFEST row in the same commit as this review (the previous cycle did exactly that for its review file).
- Unstated limits found: the value-vs-address gap (§5 of the self-check — nit 1), the late-registration limit
  on the mod author's page (MINOR 3), the empty-string-means-unknown manifest rule being absent from the
  glossary/wire wording (MINOR 2), and the Restore-library path (MINOR 4).
- Deleted rather than moved: the `todo` entry's candidate list and the "names the subset … refuses the rest"
  sentence (MAJOR 3). The acceptance row itself moved verbatim, as claimed, and the non-goals survived.
- **F. Contract shape: the claim holds and the new Runtime surface leaks nothing into the mod-visible
  contract.** No `Abstractions` file is in the change set, so `[ApiStability]`, the baseline and
  `ApiSurfaceGateTests` are untouched. The two new public types live in `Runtime` (which a mod may patch but
  is never promised): `ModContentStore` (public for `ModService`'s public constructor, write half internal,
  same shape as `ModStatusStore`) and `IModContentFingerprints` (`string` + `IReadOnlyDictionary<string,
  string>` — no erased type, no handle, nothing a mod could confuse for a stable contract).

---

## What I could not falsify

- The encoding's injectivity over the **reachable** input space: 12 adversarial pairs, 2 collisions, both
  requiring an input `ModContentAdapter`'s rails refuse (a `:` inside a content id; an empty namespace).
- The mode-row equivalence argument for the host-only `IsStateBearing` choice.
- The claim that the payload member was dead (`WorldSnapshotEncoder` reads members explicitly).
- The claim that no producer of `SaveManifestMeta` other than `WorldCutWriter.MetaOf` exists.
- The empty-set digest, and "absent on the wire reads as none" as the code's actual behaviour (what I could
  falsify is the *distinguishability* claim around it — MINOR 2 — not the behaviour itself).
- 4899/4899, 572/572, 0/0, the +19 decomposition, the four non-aggregate focused rows of §4 (9, 49, 26, 33),
  all four alignment hashes, the line-format pin, the acceptance row's verbatim move, the ticket's
  status/folder/index agreement.

## What I could not check at all

- **Anything needing the game running or two real clients** — the acceptance row's "two clients" scenario, the
  in-game content set (whether CUO's own registrations dominate the digest in practice), and whether a
  state-bearing mod's join refusal is acceptable to players.
- **`dotnet format`** — forbidden while the tree is frozen (`ApiSurfaceGateTests`-style formatting drift and
  the format gate therefore remain unverified; the self-check §4 reports exit 0).
- **Thread affinity at runtime** — `ModContentStore`'s "single-threaded by construction" claim (the class
  comment) rests on the handshake and the cut running on the main thread; I read the pump's own comments
  (`SessionNotices`, `ProjectionHealthCoordinator`) but could not prove it without a running process.
- **`reversing/`** — gitignored, so no native call site could be searched for A0(1); I named no file there.
- Pre-existing, out of scope, recorded so it is not mistaken for this change's: `docs/decisions/index.md`
  stops at decision 159 while the active register carries 254 (so decision 254's absence from that index is
  consistent with a long-standing drift, not a missing row this cycle owed).
