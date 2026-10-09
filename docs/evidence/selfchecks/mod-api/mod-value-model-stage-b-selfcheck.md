# Self-check — the typed value model (stage B: the stores and the last byte-shaped surfaces)

Cycle: 2026-10-09. Ticket: `docs/backlog/review/mod-api-no-opaque-envelopes.md` (stage A landed earlier the
same day; this cycle is the second half, and it is the half that empties the census). Scope landed:
`IModData`, `IModState` and its file, `IModNativeApi`'s admitted value surface, and the last `byte[]` on the
mod-visible contract.

## 1. What landed, mechanism by mechanism

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | `IModData` (`IModData.cs`, `ModDataStore.cs`) | `TryGet` / `TrySet` / `TryApplyShared` carry a `ModValue`; the slot keeps the value itself, so the clone on read and on write is DELETED rather than moved (a value is immutable) | `ModDataTests` (7 cases), the baseline's new lines |
| 2 | `ModDataPolicy` | the value rule IS the encoder's: `IsValidValue(value, out refusal)` calls `ModValueCodec.TryEncode` with the slot cap and hands the refusal to the caller's log line, so the policy owns no second rule to drift | `ModDataTests.ValueCaps_AreEnforcedByTheEncoderWithoutSilentTruncation`, `ModDataPolicy` (66 lines) |
| 3 | `IModState` (`IModState.cs`, `ModStateStore.cs`) | the table holds `ModValue`; a write is accepted exactly when the encoder can encode it; the adapter's role/permission gates are untouched | `ModStateTests` (9 cases), `ModStateStore` (338 lines) |
| 4 | The mod-state file (`ModStateFile.cs`, `ModStateStore.Persist`/`Load`) | each entry is written as the value's canonical encoding into the file's own byte field (a FILE's binary leaf, which the census keeps); on load a stored string that is not exactly one value is dropped BY NAME with its reason while the rest of the table loads; the disk version becomes **2**, because a version-1 file held whatever bytes the mod chose and one of those strings (a lone `0x00`) now decodes as the valid value `false`, so no migration can tell the two apart and the file is refused whole — the degradation the file already promises for an unknown version | `ModStateTests.AStoredByteStringThatIsNotAValue_IsDroppedByNameAndTheRestOfTheTableLoads`, `.AVersionOneFile_IsRefusedWholeRatherThanReinterpreted`, `.Persistence_SurvivesANewHostProcess` (a map/list/binary/number round trip), `ModStateFile` (63 lines) |
| 5 | `IModNativeApi`'s admitted value surface (`IModNativeApi.cs`, `ModNativeApiPolicy.cs`) | the `byte[]` arm becomes `ModValue` (encoded inside the same 64 KiB cap), and the array shapes move from twelve `T[]` TYPE PATTERNS to a rank + element-type check — see §4 for why a pattern list cannot state the rule | `ModNativeApiTests.PolicyRails_AreExact` (raw `byte[]` refused as argument and as result, `sbyte[]`/`int[]`/`string[]` still admitted, a jagged array refused) |
| 6 | The runtime moodle request | verified, not changed: stage A had already moved `Payload` to `Value`, and this cycle confirmed the two call sites — the projection builds the request from the store's `ModValue` (`ModStatusMoodleProjection`, the only construction site) and the resolver registry carries it (`ModStatusStore`, `ModStatusMoodleRuntimeAdapter`) | the survey in §2; no `Payload` remains on the moodle path |
| 7 | The docs, the registry, the baseline and the records | four pairs re-recorded in `standard/alignment.txt` (the reference block, the save-data how-to, the permissions page, the contributing page); both blocks' native value surface, mod-state and runtime-data sections rewritten; the terminology `value` note now says "carries across or stores in"; the baseline re-recorded with FIVE tombstones and five new lines, so the surface stays 865 entries and the tombstone count moves 57 → 62 | `git hash-object` per pair; `ApiSurfaceGateTests` green |
| 8 | Two leftover byte-era doc samples (found by this cycle) | the Mod UI snippet in BOTH blocks still called `context.Network.Broadcast(Encoding.UTF8.GetBytes("ping"))` and the save-data how-to in both blocks still encoded JSON — samples that cannot compile against the contract stage A landed, i.e. a stage-A miss | both rewritten onto the model; a grep for `Encoding.UTF8.GetBytes` over `docs/en` and `docs/zh` now finds nothing (the surviving hits are process records under `docs/evidence/` and `docs/backlog/`) |
| 9 | The stale byte-era XML comments — the stage A review's M5 family, re-found by this cycle's review | fixed ONE BY ONE instead of summarised: `IModContext` ("opaque key/value bytes"), `IModMoodleRuntime` ("opaque payload"), `ModMessageMsg` (three sentences: the frame's summary, the field, the tunnel form), `ModStatusProjectionKind` (three, including the `None` member), `ModStatusStore` (three: the header, the projection snapshot seam, the presence seam), `ModStatusAdapter`, `ModStatusVanillaProjection`, `ModStatusProjectionSnapshot`, `ModStatusProjectionReadModel`, `ModExample/ExampleMod`. "Opaque" now survives only where it means something else (the launcher's idle fade, a colour's alpha, the content binder's kind no provider binds) | grep `src/` for `opaque\|defensive copy\|defensive-copy`; the process lesson is recorded in `contributing/review-and-delivery.md` (both blocks) |
| 10 | The drop line is PINNED, not merely written | the state-file case now drives a `RecordingLoggerFactory` through the real composition root and asserts the WARNING names the dropped key (`test.state/legacy`) and the drop — the "By Name" its case name promises | `ModStateTests.AStoredByteStringThatIsNotAValue_IsDroppedByNameAndTheRestOfTheTableLoads` |
| 11 | The dated acceptance record | the two anchors broken by this cycle's test renames are excused through `docs/evidence/ticket-anchor-exceptions.json`, each with a reason naming the rename, and the 2026-09-27 record keeps the names the batch actually ran — the mechanism the anchor gate documents for exactly this case | `EveryDocumentedTestAnchorIsStillDeclared` green with the record's own names restored |
| 12 | Immutability and the 8-bit twin, stated exactly | `ModValue`'s and `IModState`'s immutability sentences say "no TYPED path mutates a value", and `TryGetBinary` names the `MemoryMarshal` caveat, because the leaf hands out the value's own memory; the native value bullet on both pages says the `byte[]` refusal is a rule about the contract's spelling (a signed `sbyte[]` stays an ordinary primitive array on a stack that does not separate the two at array level) | the two blocks' native section and the re-recorded pair; the ticket's leaf line now carries the same caveat |

## 2. The family audit (no piecemeal fix)

- The census rows moved WHOLE: the runtime-data row (`IModData.TryGet` / `TrySet` / `TryApplyShared`), the
  mod-state row (`IModState.TryGet` / `TrySet`) and the native-API row. Greps over `src/` for `byte[]` on a
  **mod-visible contract member** find none; the two shapes the census keeps on purpose are the save
  archive's own file content (`SaveArchiveEntry.Content`) and the model's own `Binary` leaf, and the state
  file's own `StateEntry.Value` is the same kind of thing one layer down (a FILE's field, not a contract
  member).
- The stale-prose family was swept because a review found it twice: the stage A review listed six comment
  sites, its disposition table in stage A's self-check reduced them to "a stale `<see cref="TryHandleStatusPayload"/>`
  — fixed", and the other five were still standing when this cycle's review looked. All of them are fixed
  now (§1.9), and the process lesson — a review's findings are dispositioned one by one, never summarised
  into one row — is recorded in `contributing/review-and-delivery.md` on both sides, which is where the next
  cycle loads it from.
- The moodle row was checked rather than assumed: the only construction site and both registry sides were
  read, and nothing on that path still speaks bytes.
- The array-type-test family was swept because this cycle touched it: grep for `T[]` type patterns,
  `is byte[]`, `as byte[]` and `(byte[])` casts over `src/` finds `ModNativeApiPolicy` as the ONLY site, so
  the platform behaviour in §4 had exactly one occurrence in the tree and it is fixed here. No sibling site
  needed a ticket.
- The reference family moved with the ticket: it went `todo/` → `review/`, the index row moved, and every
  prose reference that names its old path was updated (the decision register, four tickets, the backlog
  index) — `BacklogReferenceGateTests` is what proves it.
- The two acceptance-record anchors broken by the renamed mod-data cases were re-pointed at the names the
  cases carry now (`BacklogIntegrityGateTests.EveryDocumentedTestAnchorIsStillDeclared`).

## 3. Tests

| Suite | Result |
|---|---|
| `ModDataTests` (7) + `ModStateTests` (9) + `ModNativeApiTests` + `ModValueTests` + `ModValueCodecTests` (the focused run) | 49/49 |
| The mod family, `--filter "FullyQualifiedName~CasualtiesUnknownOnline.Tests.Mods"` | 461/461 |
| `CasualtiesUnknownOnline.NormativeGates.Tests` | 573 total; see §4 |

Two cases are new and pin the file's decode seam (`AStoredByteStringThatIsNotAValue_...`, which also
asserts the WARNING that names the dropped key, and `AVersionOneFile_IsRefusedWholeRatherThanReinterpreted`);
the store cases that used to assert "the array is copied in both directions" now assert what replaced it (a
value is immutable, so the caller's later write cannot reach the slot).

## 4. Verification (how the runtime proves it)

- **The store paths under test are the production ones.** Both suites drive the real composition root
  (`TestNode` → `ModService` → `ModContext` → the per-mod adapter → the store), so a write passes the
  adapter's role/scope gate, the policy and the encoder; the read-back is the value the store keeps.
- **The file path under test is the production one.** The two new cases BUILD a file this build did not
  write (a v2 file whose one entry is not a value; a v1 file whose entry would decode as `false`) and let
  the production loader read it; the entry the case wants KEPT is produced by the production encoder, so
  the fixture carries what the framework would write.
- **The three ends of the value rule are cases**: a null value and a ~100 KiB value are refused
  (`ModValues.OverCap()`), and a value whose encoding is exactly the cap is accepted
  (`ModValues.AtTheRail()` = 65536 bytes), in both stores. A refusal is one log line naming the path inside
  the model and the budget it broke.
- **The platform behaviour behind the native value list was MEASURED, not reasoned about.** The array
  shapes had to move off type patterns because a `byte[]` was still being admitted after the `byte[]` arm
  was deleted: a probe compiled against the built assemblies printed a matrix over fourteen sample types ×
  five `T[]` patterns, and `value is sbyte[]` is TRUE for a `byte[]` and the other way round (the same
  holds for `short`/`ushort`, `int`/`uint`, `long`/`ulong`; `bool[]`, `float[]`, `decimal[]`, `string[]`
  and `char[]` are exact). The probe also resolved the compiled IL of its own methods, which shows the
  tokens are correct and DISTINCT (`System.Byte[]` vs `System.SByte[]`) — so the equivalence is the
  runtime's, not the compiler's, and no pattern list can express "not a raw byte array". `PolicyRails_AreExact`
  is the regression that keeps the refusal in place; the state-file cases, the mod family run and the gates
  were re-run after the change with every file at its committed content.
- The gates: the API surface gate (865 entries, 62 tombstones, every tombstone reason supported by the
  diff), the documentation tree gate (three pairs re-recorded), the backlog integrity and reference gates
  (the moved ticket and the renamed-test anchors), the self-check manifest gate and the delivery checklist.

## 5. The independent review (2026-10-09)

`mod-value-model-stage-b-review.md` is the report, run in a fresh context against the frozen tree. No
blocker; **two minor and four nits**, every one of them dispositioned in this same commit:

| # | Finding | Disposition |
|---|---|---|
| M1 (minor) | The byte-era prose was only partly retired, including a site this cycle owns: `IModContext`'s `State` summary still said "opaque key/value bytes" (what IntelliSense shows a mod author, contradicting the interface beside it), and five of the six comment sites the STAGE A review had listed were still standing — `IModMoodleRuntime`, `ModMessageMsg` (three sentences), `ModStatusProjectionKind` (three), `ModStatusStore` (three), `ModStatusAdapter`, `ModStatusVanillaProjection`, `ModExample/ExampleMod` | all of them rewritten against the code beside them (§1.9), plus the three `None`-projection wordings that called a typed kind "opaque"; stage A's disposition table had reduced its own M5 family to a single line, and that process defect is now recorded as a rule in `contributing/review-and-delivery.md` (both blocks) |
| M2 (minor) | This cycle edited the dated acceptance record `mod-data-sync-model-20260927.md` so its anchors matched the renamed cases — making a 2026-09-27 record claim it ran cases that did not exist that day, when the repository documents `docs/evidence/ticket-anchor-exceptions.json` for exactly this ("a test renamed afterwards must not force history to be rewritten") | the record's own names are RESTORED and the two anchors are excused in the exceptions file with a reason each (§1.11); the case names stay honest and the judgement the batch made is untouched |
| N1 (nit) | The "no raw byte array" rule is a rule about spelling: `sbyte[]` is admitted on purpose, and this stack does not separate it from `byte[]` at the array level, so the page's "A raw `byte[]` is not admitted any more" reads stronger than the code | the rule is now stated as it is on both pages (the signed twin stays an ordinary primitive array) rather than tightened into an arbitrary ban on a legitimate numeric array; the ticket already disclosed the choice |
| N2 (nit) | `MemoryMarshal.TryGetArray` reaches the binary leaf's backing array, so "a value is immutable" has one deep escape — a mod could change a value the framework holds without a `TrySet` | the claims are made exact instead of absolute: `ModValue`'s class doc and `IModState`'s summary say no TYPED path mutates a value, and `TryGetBinary` names the caveat; the ticket's leaf line carries it too. Not "fixed" by copying on read: the escape needs a deliberate call, the mod is in-process trusted code, and nothing a peer sends reaches it |
| N3 (nit) | Two scope sentences in this record were looser than the tree (the `src/` census sentence, and a `docs/{en,zh}` grep claim that actually found nothing there) | both rewritten to what was measured (§2, §1.8) |
| N4 (nit) | `AStoredByteStringThatIsNotAValue_IsDroppedByNameAndTheRestOfTheTableLoads` never asserted the name it promises | the case now drives a `RecordingLoggerFactory` through the composition root and asserts the warning names the dropped key (§1.10) |

What the review could not falsify (its §7) is worth keeping: every claim of the change reproduced; the
rail arithmetic is exact at both ends; the values are immutable through every typed path (the stage A
container escape is closed); the production path is reachable and nothing else calls the stores; the
runtime array equivalence is real and the only site of it in `src/` is the one fixed here. What it could
not check: anything needing the game, and the acceptance batch's rows.

## 6. Limits (recorded, not hidden)

- **No game process and no two-client session were run this cycle.** The stores are L0-testable end to end
  through the real composition root, and a live mod's runtime data and a cross-session state read are the
  acceptance batch's rows.
- **The file's version 1 is DISCARDED by design.** No migration can distinguish a v1 byte string from a
  value, so the whole table is refused with a warning and the next write replaces it; that is the file's own
  degradation contract, recorded in the ticket and in decision 249.
- **A write still encodes a value to validate it** (the one-validator trade-off stage A's review accepted),
  and the state store encodes again when it persists — that second encode is the file write's own cost, not
  a second rule.
- **`ModNativeApiPolicy` admits single-dimensional arrays only, decided by element type.** Signed and
  unsigned twins of the same width are admitted interchangeably by the runtime itself (measured), which is
  why the check is written the way it is; a multidimensional array is refused. The `byte[]` refusal is
  therefore a rule about the CONTRACT'S SPELLING, and the review's N1 says so on the pages: an `sbyte[]` is
  an ordinary numeric array, and nothing today can carry bytes through the surface anyway (the only
  registered operation takes no arguments).
- **Immutability is a statement about the typed API.** `MemoryMarshal.TryGetArray` can reach the binary
  leaf's backing array (the review's N2, measured); a mod that does that changes only a value it built
  itself, and the stores' decision to share rather than copy rests on the typed guarantee, which holds.
- **The wire did not change** (the stores are process-local and the file is a save), so the protocol number
  stays where decision 241 froze it.
