# The content fingerprint: what a declaration produced, compared at both boundaries

Scope: ticket `docs/backlog/review/mod-content-fingerprint.md` (decision 254). One canonical text over the
ADDRESS of every registered content entry, its digest compared between peers at the handshake and recorded by
a cut, so two peers that materialize different content under one mod id and one mod version are named instead
of silently accepted.

## §1 Mechanism inventory

| # | Mechanism | What changed | Evidence |
|---|---|---|---|
| 1 | `ModContentFingerprint` (new, internal, pure) | the canonical line per entry — length-prefixed mod id, address, kind, schema version — its sort and its SHA-256 digest; the address is the canonical `namespace:path` when the registration carries a namespace, the mod-scoped id otherwise | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModContentFingerprint.cs`; `ModContentFingerprintTests` (9 cases: the exact line pin, the namespace address, sort/order-independence, one mutation per address field, the separator-forgery case) |
| 2 | `IModContentFingerprints` + `ModContentStore` (new) | the public read surface other layers compare (`Fingerprint`, `ByMod`) and the leaf that OWNS the definitions; `Entries` stays the snapshot the read view promises | `src/.../IModContentFingerprints.cs`, `src/.../ModContentStore.cs`; `ModContentFingerprintTests.ByMod_AnswersOnlyForModsThatRegisteredContent`, `.Fingerprint_CoversEveryModAndMovesWhenAnEntryIsRetired`, `.Entries_AreASnapshotThatSurvivesALaterRegistration` |
| 3 | `ModContentAdapter`, `ModContext`, `ModLifecycle`, `ModService` | the adapter keeps every rail (permission gate, id/kind/schema checks, duplicate, cap, logging) and the store holds what it accepted; the per-mod projection `ModContext.ContentRegistrations` is GONE, `ModLifecycle.Entries` reads the store | `src/.../ModContentAdapter.cs`, `ModContext.cs`, `ModLifecycle.cs`, `ModService.cs`; the whole content suite still passes over the real stack (`ModContentTests`, `ModContentDeclarationTests`, `ModContentCatalogTests`, `ModContentBinderTests`, `ModContentNullCollectionBindingTests`, `ModAuthoredDefinitionBindingTests`) |
| 4 | `ModHandshakeListProvider` + `ModComposition` (new leaf) | the session's `IModListProvider` composes the discovery registry with the store, so each wire info carries its mod's fingerprint; `ModRegistry` is untouched (still the pure discovery type) | `src/.../ModHandshakeListProvider.cs`, `Composition/ModComposition.cs`; `ModContentFingerprintTests.CurrentModInfos_CarriesTheFingerprintOfEachModsContent` (unit) and `ModContentTests.HandshakeList_CarriesTheFingerprintOfEachModsOwnContent` (over the real DI composition) |
| 5 | `ModInfoMsg.ContentFingerprint` (wire) | a new optional member; null = "this mod registered no content" on both sides, never "unknown" | `src/.../Protocol/Messages/ModInfoMsg.cs`; `ModHandshakeProtocolTests.HandshakeWithMods_RoundTripsExactly` (carries a value and asserts the null twin); the shape pin moved deliberately — `ModNativeBindingDeclarationTests.Declaration_TravelsOnTheWireShape` |
| 6 | `HandshakeHandler.NoteContentParity` | the host's own per-mod map (`IModContentFingerprints.ByMod`, built once per handshake) against the member's, per mod id and only for a mod both sides list: a difference is REPORTED — the line names the mod, both fingerprints and the mode — and the member is admitted, never refused (user ruling 2026-10-10) | `src/.../Session/Handlers/HandshakeHandler.cs`; `ModHandshakeTests` (5 cases: the difference named and admitted, equal content silent, member-only content named, the local-surface mode taking the same verdict, both-sides-none silent) |
| 7 | `WorldCutWriter` + `SaveManifestMeta` | the cut records the whole-set fingerprint in the manifest, replacing the hard-coded `string.Empty` | `src/.../Session/Persistence/WorldCutWriter.cs`; `WorldSaveCaptureTests.Cut_RecordsTheContentSetThisProcessMaterialized` (reads `contentFingerprint` back out of the live manifest and shows it follows the entries) |
| 8 | `WorldLoadOptions.ExpectedContentFingerprint` + `SaveArchiveReader.NoteContentMismatch` + `DamageReport.EntryReason.ContentMismatch` + `WorldRestoreApplier` | a load compares the recorded set with the live one and REPORTS a difference in repair mode; empty on either side is "unknown" and is not compared; the applier supplies the live value at the Continue seam | `src/.../Persistence/WorldLoadOptions.cs`, `SaveArchiveReader.cs`, `DamageReport.cs`, `src/.../Session/Persistence/WorldRestoreApplier.cs`; `SaveArchiveDamageTests.ContentMismatch_OpensInRepairModeWithAWarning`, `.EqualOrUnknownContentFingerprints_AreNotDifferences` (reader level) and `WorldRestoreReportTests.AContinueUnderADifferentContentSet_ReportsTheMismatchWithoutRefusing` (the Continue seam, judged from the report) |
| 9 | `SaveArchiveComposition` + the load-failure withdrawal | the save layer resolves the fingerprint leaf (no path to `ModService`); a mod whose load FAILS takes its registered content with it (`ModContentStore.RemoveMod` at `ModLifecycle`'s catch), so the framework-wide view only ever holds content of mods that are loaded | `src/.../Composition/SaveArchiveComposition.cs`, `src/.../Session/Mods/ModLifecycle.cs`; `ModContentTests.AModThatFailsToLoad_LeavesNoContentBehind` over the real composition, with `TestBindFailingMod` registering content and throwing in `Bind` (observed red before the fix — the entry was in the console/ownership/fingerprint view — then green) |
| 10 | DELETED: `WorldSnapshotPayload.ContentFingerprint` | a payload member nothing read (the manifest is what carries the value) — the family audit found it as the only dead carrier of the field | `src/.../Persistence/WorldSnapshotPayload.cs`; the three construction sites compile without it |
| 11 | The pages a reader meets | the handshake table gained the content row and the paragraph explaining what the fingerprint does and does not cover, in BOTH languages; the glossary gained the term in both (and separates the per-mod value from the whole-set one); the terminology registry gained it before first use; the archive format §3.2 (what the field means) and §6.2 (a mismatch opens in repair mode) | `docs/en/reference/mod-api.md` + `docs/zh/reference/mod-api.md`, `docs/en/reference/glossary.md` + `docs/zh/reference/glossary.md`, `docs/standard/terminology.txt`, `docs/architecture/save-archive-format.md`, `docs/decisions/active.md` (254); the alignment record re-recorded both changed pairs |
| 12 | The tests | 11 test files (2 of them new: the pure suite and the load-failure fixture), 20 new cases — 9 pure, 5 handshake, 2 reader-level, 1 cut-record, 1 Continue-report, 1 handshake-list carrier, 1 failed-load withdrawal | `tests/.../Mods/ModContentFingerprintTests.cs` (new), `tests/.../Mods/TestBindFailingMod.cs` (new), `ModHandshakeTests.cs`, `ModContentTests.cs`, `ModNativeBindingDeclarationTests.cs`, `ModHandshakeProtocolTests.cs`, `tests/.../Persistence/SaveArchiveDamageTests.cs`, `WorldRestoreReportTests.cs`, `WorldSaveCaptureTests.cs`, `WorldSaveFixture.cs`, `WorldSaveDegradationTests.cs` |

## §2 Whole-family audit

- **The readers are two, and both are named.** The fingerprint exists because two callers need one value: the
  session's handshake list (per mod) and the save layer (whole set). Nothing else reads it, and no surface was
  added that nothing consumes — the interface has exactly those two members.
- **The DI graph is the reason for the leaf, measured rather than assumed.** `ModService` reads
  `SessionService`, and the session builds the handshake (`SessionPeerMaintenance`) and receives it
  (`HandshakeHandler`), so neither reader may resolve the facade:
  `ModService(SessionService …)` and `SessionService(IModListProvider …)` are the two edges that close the
  cycle. The store is a leaf with NO dependencies (`ModContentStore` has no constructor parameter at all), and
  the new provider composes two leaves. `ModContentStore` is public only because `ModService`'s public
  constructor hands it to the mod domain's internal half; its write half stays internal, which is why the
  accessibility error (CS0051) appeared and was resolved this way rather than by widening the write surface.
- **Every field that can carry a separator was checked, not assumed.** The address is validated by
  `ModContentPolicy.IsValidId` → `ContentId.IsValidPath` (`[a-z0-9][a-z0-9_.-]*`), so it cannot contain a tab
  or a newline; a mod id is only non-blank (`ModRegistry.Discover`) and a kind only non-whitespace
  (`ModContentPolicy.IsValidKind`, ≤ 64 chars), so BOTH can. The length prefix is therefore what makes the
  canonical text injective, and the forgery case in the tests is the collision it prevents: one mod id that
  embeds a whole second line renders — unescaped — exactly as another pair of entries.
- **The "absent" encoding is one rule read at four sites** (guest filler, host comparison, manifest write,
  load compare): null/empty is "no content registered" or "unknown", never "unknown content"; the wire carries
  null and the manifest carries `string.Empty`, and neither is ever used as a fingerprint value. The
  empty-set digest (`e3b0c442…`, SHA-256 of the empty text) is a real value the whole-set property returns and
  is NOT what "no content" is spelled as.
- **The scope question the ticket left open — registered versus bound content — was decided and recorded.** The
  comparison covers what a mod REGISTERED (`IModContentControl.Entries`), not what the binder later put into a
  game table: binding is the binder's own pass (a provider may refuse an entry, and `ModContentBinder` logs
  that per entry), it runs once at the first frame, and it is not a declaration a peer can assert. "What a
  declaration produced" is therefore the registered set, which is also what a saved world's rows name.
- **The fingerprint is not cached** — `ModContentStore.Fingerprint` and `.ByMod` recompute the sort and the
  digest on every read, and `Lines` re-invokes the live `Definition.Id`/`Kind`/`SchemaVersion` getters. That is
  deliberate for freshness (a registration is visible to the very next reader, and the handshake and a cut can
  legitimately see different sets), and it is what the claim "one canonical FORM" means rather than "one
  computation": a definition a mod MUTATES after registering breaks the contract it was registered under
  (`IModContentDefinition`'s doc) and the digest follows the mutation, so a process that did that could hand
  the handshake one value and its own cut another. Recorded as a limit rather than defended with a snapshot,
  which would give the address a second home beside the registry that keys on it.
- **In-game consequences were checked, and the verdict is the user's:** a refusal never happens, so nothing is
  blocked by this comparison; a warning is written only when two sets actually differ, once per handshake
  attempt, which is the existing shape of `NativeBindingParity`'s warn policy. The ruling also fixes the
  failure direction: the check cannot be complete, so it reports instead of gating entry.

## §3 What landed, and what deliberately did not

Landed: the fingerprint (one canonical form, two readers), the wire member and the host's report, the manifest
half with its load comparison and report reason, the leaf structure that lets both readers exist without a
cycle, the withdrawal that keeps a failed load out of the framework-wide view, and the deletion of the payload
member nothing read.

Deliberately not:

- **No policy knob, and no refusal.** The verdict is the user's ruling (2026-10-10): a difference is recorded
  and the member is admitted. The comparison covers addresses and never a computed value, so it cannot be
  complete, and an incomplete comparison that gated entry would lock players out over a benign local
  difference — the reasoning the ruling gives verbatim ("做宽松了没效果，做严格了稍微出点瑕疵就玩不了"). A
  `HostRules` knob would only be needed by a verdict that blocks, which is exactly what was refused.
- **No per-entry wire list and no entry counts.** The handshake carries one digest per mod: a full list would
  be up to `ModContentPolicy.MaxDefinitionsPerMod` (1024) entries per mod on every handshake retransmit, and an
  entry count could only say "12 vs 13" without naming the differing entry. The report names the mod — and the
  per-entry detail is already in each client's own log (the `[Mods] {ModId} registered content …` lines).
- **No lifecycle re-check.** A mod may register content after the handshake (`TryRegister` is callable from
  `Update`); the comparison is the point-in-time fact the handshake can honestly carry, and the next cut
  records the newer set. Named as a limit in §5 rather than papered over with a live watcher.
- **No change to the mod-visible contract.** `Abstractions` is untouched (tools, definitions and permissions
  are unchanged), so the recorded API baseline does not move; the new surface is Runtime-internal plus one
  public read interface, and the only wire change is the `ModInfoMsg` member, whose number does not move
  pre-release (decision 241).
- **No second comparison of CUO's own content.** CUO's own registrations go through the same `TryRegister`
  path, so they are in the same fingerprint on both sides by construction; nothing needed a special case.

## §4 Verification

| Layer | Command | Result |
|---|---|---|
| Build | `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings, 0 errors |
| Focused (the touched areas) | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~CasualtiesUnknownOnline.Tests.Mods\|FullyQualifiedName~CasualtiesUnknownOnline.Tests.Persistence\|FullyQualifiedName~CasualtiesUnknownOnline.Tests.Protocol"` | 979/979 (the review reproduced 978 before the load-failure case landed, and the figure the first draft carried here — 959 — was a pre-change run, which the review caught) |
| The new pure suite | `--filter "FullyQualifiedName~ModContentFingerprintTests"` | 9/9 |
| The new handshake cases | `--filter "FullyQualifiedName~ModHandshakeTests\|FullyQualifiedName~ModContentFingerprintTests"` | 49/49 |
| The new save-side cases | `--filter "FullyQualifiedName~SaveArchiveDamageTests\|FullyQualifiedName~WorldRestoreReportTests\|FullyQualifiedName~WorldSaveCaptureTests"` | 26/26 |
| The new wiring + wire cases | `--filter "FullyQualifiedName~ModContentTests\|FullyQualifiedName~ModHandshakeProtocolTests\|FullyQualifiedName~ModNativeBindingDeclarationTests"` | 34/34 |
| Behaviour (whole suite) | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` | 4900/4900 (was 4880; the 20 new cases are the 9 + 5 + 2 + 1 + 1 + 1 + 1 listed in §1) |
| Normative gates | the same run's `CasualtiesUnknownOnline.NormativeGates.Tests` | 572/572 with the checklist gate excluded (mid-cycle it is red by design — the boxes are unchecked until the cycle closes); the full gate run with every box checked is the pre-commit run |
| Format | `dotnet format CasualtiesUnknownOnline.slnx` | exit 0; every new/edited C# file re-measured byte-wise for CRLF | 

## §5 Limits

- **A differing computed VALUE under an equal address set is not compared, and cannot be.** This is the first
  limit because it is the one the ticket's own motivating example was about ("a local config changes a
  weight"): the fingerprint pins identities — mod id, content id, kind, schema version — and a definition's
  values are re-read from the mod's own code, which is not stable enough to hash. Two peers whose definitions
  compute differently under an equal address set are therefore admitted in silence, and the mod version or the
  entry's schema version is the only place that difference can be declared. Named in decision 254, in the
  mod-api page a mod author reads (both languages) and in the self-check §2.
- **The digest localizes to the mod, not to the entry.** A reported difference names the mod and both
  fingerprints; it does not name which entry differs. Naming it would require shipping the entry list (rejected
  in §3), and the two clients' own registration lines are the per-entry record.
- **A content set that changes after the handshake is not re-compared.** The check is a point-in-time fact of
  the join, not a standing invariant; a later registration reaches the next cut's manifest but no peer.
- **A definition a mod MUTATES after registering moves the digest with it** (§2): the value is derived from the
  registry at every read, so the address has one home — the registry — and the mutation the contract forbids is
  what a reader would have to trust against.
- **The comparison is host-side and never blocking.** The member is admitted; what it learns is the host's log
  line, and a guest whose own log is the only place its registrations appear is the one that has to diff them.
- **Registered, not bound** (see §2): a provider that refuses an entry at bind is reported by the binder's own
  warning, and the fingerprint does not move for it.
- **No real two-client run in this cycle.** The peer comparison is proven over the real handshake with the real
  handler, the real registry and the real store, but the acceptance row's "two clients" is judged at the
  acceptance batch, on the deployed artifacts.
