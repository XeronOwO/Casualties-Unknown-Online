# The typed value model (stage B: the stores and the last byte-shaped surfaces) — independent review

Independent adversarial review of the uncommitted change set "stage B of the typed value model: `IModData`,
`IModState` and its file, the runtime moodle path and `IModNativeApi`'s admitted value surface", run in a
fresh context against the FROZEN working tree at `e79aa1a6` (branch `master`; 31 modified paths plus the
cycle's own untracked self-check — the dirty tree is the artifact under review). Risk tier: **FULL** — a
mod-visible contract, a save file's version and two stores move together. Read-only: the only file this
review writes is this report. The change's claims were attacked, never the evidence for them; every claim
was re-opened in the tree, and every suite and probe that can run without the game was run.

Ticket: `docs/backlog/review/mod-api-no-opaque-envelopes.md` (*What landed (2026-10-09, stage B)*).
Self-check: `docs/evidence/selfchecks/mod-api/mod-value-model-stage-b-selfcheck.md`. Decision: entry 249.

## 1. Verdict

**No blocker. Two minors, four nits.**

The mechanism is sound: the two stores really keep the value and no longer copy, the value really is
immutable for the paths a mod can reach (including the container views a *previous* review found open), the
value rule really is the one encoder's and both policies really have no second rule, the mod-state file
really writes a canonical encoding and really refuses a version-1 file whole, and the native value surface
really refuses a raw `byte[]`. Every figure the change states reproduces exactly (§2), including the single red
gate the change declares.

What did fall:

- **M1 (minor)** — the byte-era prose is only partly retired. Four sites on the surfaces the ruling moved
  still tell a reader that state is "opaque … bytes" or that the store makes "defensive copies"; one of them
  (`IModContext`) is the property declaration for the very store this cycle moved, and another
  (`IModMoodleRuntime`) sits on the moodle path the self-check declares byte-free. The stage A review listed
  these as its M5; the disposition table written afterwards names only the dangling `cref`, so the dropped
  bullets were neither fixed nor recorded.
- **M2 (minor)** — the dated acceptance record was edited to carry test names that did not exist on the day
  it ran, instead of using the excuse mechanism the repository keeps for exactly that case
  (`docs/evidence/ticket-anchor-exceptions.json`, whose own header says history must not be rewritten for a
  rename).
- The four nits are the `sbyte[]` spelling hole in the "no raw byte array" rule, one deep immutability escape
  in the model's binary leaf, two loosely scoped grep/scope sentences in the self-check, and a new case whose
  name promises more than its assertions.

## 2. What was run (evidence)

- `dotnet build CasualtiesUnknownOnline.slnx --nologo -v q` → **0 warnings, 0 errors** (so no test below ran
  against stale binaries: the incremental build settled the outputs against these sources first).
- `dotnet test tests\CasualtiesUnknownOnline.NormativeGates.Tests --nologo -v q` → **573 total, 572 passed,
  1 failed** — the one failure is `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`. So
  `ApiSurfaceGateTests`, `DocumentationTreeGateTests`, `BacklogReferenceGateTests`,
  `BacklogIntegrityGateTests`, `SelfcheckManifestGateTests` and the shape gates are green on the frozen tree,
  and the self-check's "573 total; see §4" is satisfied.
- The same single gate with `-v n` → `Delivery gate failed (8 issue(s), 0 boxes checked)`, listing exactly the
  eight required checklist lines the diff reset to `- [ ]` (the ninth, *Release-cycle deployment/acceptance*,
  is skipped by the gate and was already unchecked).
- `dotnet test tests\CasualtiesUnknownOnline.Tests --nologo -v q` → **4850 passed / 4850** (net48).
- `dotnet test tests\CasualtiesUnknownOnline.Tests --nologo -v q --filter "FullyQualifiedName~CasualtiesUnknownOnline.Tests.Mods"`
  → **461 passed / 461**.
- `dotnet test tests\CasualtiesUnknownOnline.Tests --nologo -v q --filter "FullyQualifiedName~ModDataTests|FullyQualifiedName~ModStateTests|FullyQualifiedName~ModNativeApiTests|FullyQualifiedName~ModValue"`
  → **49 passed / 49**, and per class with `--no-build`: `ModDataTests` **7**, `ModStateTests` **9**,
  `ModNativeApiTests` **7**, `ModValueTests` **7**, `ModValueCodecTests` **19** — 7+9+7+7+19 = 49 exactly.
- Baseline arithmetic: `docs/contracts/abstractions-api-baseline.txt` holds **865 surface entries** (761
  `member|` + 104 `type|`) in both HEAD and the working tree, **62** lines matching `^\*REMOVED\*` now against
  **57** at HEAD, and the diff adds exactly **five** tombstones and changes exactly **five** member lines.
- Line counts (`Get-Content … .Count`): `ModValueCodec` **591**, `ModValue` **413**, `ModStateStore` **338**,
  `ModStateFile` **63**, `ModDataPolicy` **66**, `ModNativeApiPolicy` **133** — so the 600-line architecture
  gate (`SourceShapeGateTests`' aggregate-per-type limit, exempting what `docs/architecture-debt.json` records)
  is not breached, and `docs/architecture-debt.json` carries no entry for either model file.
- Alignment: every one of the **39** recorded pairs reproduces under `git hash-object` (0 mismatches),
  including the three the diff re-recorded (`save-mod-data-across-sessions`, `permissions-and-security`,
  `mod-api`) — and those three are exactly the human pairs the diff edits.
- Tier-1 grep checks: no `todo/mod-api-no-opaque-envelopes` reference survives under `docs/`; no
  `MaxByteArrayLength` reference survives anywhere; `Encoding.UTF8.GetBytes` survives nowhere under
  `docs/en` or `docs/zh`; `byte[]` inside `src/CasualtiesUnknownOnline.Abstractions` survives only as prose
  and as `ModValue`'s own private field.
- An independent probe against the frozen net48 build (see §4.4), compiled and run outside the tree and
  **deleted** before this report was written.

## 3. Claim by claim

| # | Claim | Verdict | Where it was checked |
|---|---|---|---|
| 1 | No `byte[]` on the mod-visible contract outside the model's own binary leaf; `IModData`/`IModState` take and return `ModValue`; the slot and the table keep the value itself, no copy on read or write | **Holds** | `IModData.cs` / `IModState.cs` declare `ModValue` on all five members; `ModDataStore.TryGetValue` answers `value = slot.Value;`, `TrySetValue` stores `slot.Value = value;`, and `ModDataSlot.Value`'s own comment says "The stored value itself, shared rather than copied"; `ModStateStore.TryGetValue` answers `value = stored;` and `TrySet` stores `entry.Values[key] = value;`; the only `byte[]` left in `Abstractions` is prose plus `ModValue`'s private `_binary`. That the sharing is SAFE is verified in §4.4, not assumed |
| 2 | The value rule is the encoder's, and there is exactly one of it | **Holds** | `ModDataPolicy.IsValidValue` and `ModStatePolicy.IsValidValue` are each exactly `ModValueCodec.TryEncode(value, MaxValueBytes, out _, out refusal)`; both stores log the refusal (`"… with a value the framework cannot carry — {Reason}"`); a grep for `ModValueCodec.TryEncode|TryDecode` over `src/` finds nine call sites and **no** second value predicate anywhere |
| 3 | The file writes the canonical encoding, its disk version becomes 2, a version-1 file is refused whole, a non-value entry is dropped by name while the rest loads | **Holds** | `ModStateFile.CurrentVersion = 2`; `ModStateStore.Persist` encodes each entry with the production encoder and skips (LogError) what it cannot encode; `Load` decodes with the same cap and drops a bad entry with `"{ModId}/{Key} carries a stored value the framework cannot read — dropped ({Reason})."`; `ModStateFileStore.TryLoad` refuses `file.Version != CurrentVersion` before any entry is read, so the refusal is whole-file |
| 4 | The native value surface swapped `byte[]` for `ModValue` and decides arrays by rank + element type, because on this stack a signed and an unsigned array of the same width are not distinguishable by pattern — and that is the RUNTIME's behaviour, not the compiler's; `ModNativeApiPolicy` was the only such site in `src/` | **Holds — reproduced independently, see §4.4** | The probe printed `Byte[] → is byte[] = TRUE, is sbyte[] = TRUE`, `SByte[] → TRUE/TRUE`, `Int16[]/UInt16[]`, `Int32[]/UInt32[]`, `Int64[]/UInt64[]` likewise, `Boolean[]`, `Single[]`, `Double[]`, `Decimal[]`, `String[]`, `Char[]` exact; the twelve-arm policy with the `byte[]` arm deleted classifies a real `byte[]` as `sbyte[]`; and each pattern method's own `isinst` operand resolves to a DISTINCT type (`System.Byte[]` vs `System.SByte[]`), so the equivalence is the runtime's. The frozen build refuses `byte[]` as argument and as result, admits `sbyte[]`/`int[]`/`string[]`, refuses a jagged and a rank-2 array, and refuses an over-cap `ModValue` (`ModNativeApiTests.PolicyRails_AreExact` asserts the same) |
| 5 | The runtime moodle request needed no code; this cycle verified its two call sites | **Holds, with M1 on its prose** | `ModStatusMoodleRequest.Value` exists and no `Payload` member does; the only construction site is `ModStatusMoodleProjection` (`Value = value,` built from `_statusStore.TryGetBodyValue` / `TryGetLimbValue`) and the registry side is `ModStatusMoodleRuntimeAdapter.TryRegisterResolver`; a grep for `Payload` over `src/` finds it only on the frame (`ModMessageMsg.Payload`) and the channel's transport parameters. But `IModMoodleRuntime`'s doc still says "(opaque payload + stable limb slot/name)" — see M1 |
| 6 | Documents and records: both blocks' byte-era samples rewritten; three alignment pairs re-recorded; the ticket's move updated the index and every prose reference; two acceptance anchors re-pointed; decision 249 added | **Holds, except the acceptance edit's method (M2)** | `context.Network.Broadcast(Encoding.UTF8.GetBytes("ping"))` is now `Broadcast(ModValue.Text("ping"))` in `docs/en|zh/reference/mod-api.md`, and the save-data how-to's encoded JSON is a `ModValue.Map` in both blocks; the three pairs' hashes reproduce; no `todo/…` reference survives; decision 249 exists with the full stage-B statement; the two anchors are re-pointed — judged in §6/M2 |
| 7 | The numbers: mod family 461/461; focused run 49/49; baseline 865 entries with 62 tombstones (57+5); gates 573 total with exactly one red; `ModValueCodec` 591 / `ModValue` 413 lines | **Holds exactly** | Every figure reproduced in §2. Two caveats worth stating rather than hiding: the 461 is a measurement, not "stage A's 460 plus one case" — stage A's own commit deleted `ModPayloadNullCollectionTests.cs` and added `ModNullCollectionRuleTests.cs`, so the family count is not decomposable from the earlier record; and the focused run's 49 does decompose exactly (7+9+7+7+19) |

## 4. The apparatus, not the wording

### 4.1 Do the new and rewritten cases pin what their names claim?

- `ModStateTests.AVersionOneFile_IsRefusedWholeRatherThanReinterpreted` — **discriminating.** It writes a
  version-1 file whose single entry is `[0x00]`, a payload that WOULD decode as `false`, then asserts
  `Count == 0` and `TryGet("legacy") == false`. A loader that ignored the version would load one entry, so
  the case can only pass for the reason its name gives. Its fixture is built with the production
  `ModStateFile` type, so the file really is this build's format with an old version field.
- `ModStateTests.AStoredByteStringThatIsNotAValue_IsDroppedByNameAndTheRestOfTheTableLoads` — the "rest of
  the table loads" half is pinned hard (a valid entry produced by the production encoder is asserted to
  round-trip out of the same file, and `Count == 1`). The "by name" half is **not** asserted: nothing checks
  the drop line, only the key's absence. See N4.
- `ModDataTests.ValueCaps_AreEnforcedByTheEncoderWithoutSilentTruncation` — pins three ends: a null value
  refused, a ~100 KiB value refused, an empty text accepted and read back, and `ModValues.AtTheRail()`
  accepted **and read back**. One honest gap in the family, not in this case: the ENCODE side has no case at
  exactly `rail + 1` — `ModValueCodecTests.AValueOverTheSurfacesOwnRail_IsRefusedByNameAndSize` uses the same
  `ModValues.OverCap()` (~100 KiB), while the DECODE side does pin the exact byte
  (`APayloadOverTheRail_IsRefusedBeforeItIsWalked`: `new byte[Rail + 1]`). The admitted end of the rail is the
  end this change asserts, and it is pinned exactly, so nothing here is unproven; the one-byte-over encode case
  is simply absent.
- `ModNativeApiTests.PolicyRails_AreExact` — pins the rule this change exists to state, in both directions:
  `new byte[] { 1, 2 }` refused as argument and as result, `sbyte[]`/`int[]`/`string[]` still admitted, a
  jagged array refused, an over-cap `ModValue` refused, and `ModValue.Binary(new byte[] { 1, 2 })` admitted as
  a result. It does not pin the `sbyte[]` hole (N1) and does not need to: the hole is the design.
- The two rewritten store cases (`LocalOnly_AnyRoleCanReadWriteRemove_AndAValueIsImmutableNotCopied`,
  `AValueIsImmutable_TheCallersOwnArrayCannotReachTheStoredTable`) pin the half that is still assertable — the
  caller's later write to its own array cannot reach the store — through the factory's copy. The deleted half
  ("mutating the array you got back") is unassertable by construction, and the diff, not the case, is what
  proves the copy is gone; the names now say what they pin rather than what was removed, which is the right
  resolution of the class of nit the stage A review raised.

### 4.2 Is the rail arithmetic really exact? (checked, both ends)

From `ModValueCodec`: map = 1 tag + 4-byte count; each entry = 4-byte name length + name bytes + the value
(1 tag + 4-byte length + bytes for text). `ModValues.AtTheRail()` is four single-letter text fields, so
`5 + 4 × (4 + 1 + 5) + (16384 + 16384 + 16384 + 16339) = 45 + 65491 = 65536`, and `Fits` accepts
`stream.Length <= maxBytes` — **exactly** at the cap, not one byte under it. `ModValues.OverCap()` is a list
of 100 × 1 KiB texts: `5 + 100 × (1 + 4 + 1024) = 102905`, comfortably over every 64 KiB rail while each
element is legal on its own (1 KiB texts, 100 entries). Both helper doc comments state the arithmetic and both
match the codec. So "at the rail" and "over the rail" are what they say.

### 4.3 Is the production path reachable, and is anything else on it?

- The composition root is singular: `Plugin` passes
  `Path.Combine(Paths.ConfigPath, "CasualtiesUnknownOnline.mod-state.bin")` into `ModComposition`, which
  builds the `ModStateFileStore`; `ModService` constructs `new ModStateStore(stateFile, log)` and
  `new ModDataStore(log)` and hands them to `ModLifecycle`, which builds the per-mod adapters. Nothing else
  in `src/` constructs, reads or writes either store, and `IModData`/`IModState` have exactly one
  implementation each (the nested adapters) — no test double implements them, so the suites exercise the
  production path end to end.
- `ModStateFile` is touched only by its own file store (load/save) and by `ModStateStore` (encode on persist,
  decode on load); no console command, backup path or UI reads the byte field directly.
- `ModNativeApiPolicy` is called from exactly one place, `ModNativeApiAdapter.TryInvoke` (with
  `TryGetLocalPlayerState` delegating to it), which logs and refuses before and after the Game Adapter seam.

### 4.4 Falsifying claim 4 myself, and the immutability premise it rests on

A probe was compiled with the SDK's Roslyn for **net48** against the .NET Framework 4.8 reference assemblies,
run on the frozen build (`tests\CasualtiesUnknownOnline.Tests\bin\Debug\net48`), and deleted afterwards; the
work tree was not touched. It printed, among other things (the matrix's columns re-labelled for width and one
row elided):

```
sample   is byte[]  is sbyte[]  is short[]  is ushort[]  is int[]  is uint[]  is long[]  is ulong[]  is bool[]  is float[]  is double[]  is decimal[]  is string[]  is char[]
Byte[]   TRUE       TRUE        .           .            .         .          .          .           .          .           .            .             .            .
SByte[]  TRUE       TRUE        .           .            .         .          .          .           .          .           .            .             .            .
Int32[]  .          .           .           .            TRUE      TRUE       .          .           .          .           .            .             .            .
…
the TWELVE-arm policy (byte[] arm gone) byte[] -> sbyte[]
IL: Is_byte -> System.Byte[] ; Is_sbyte -> System.SByte[] ; Is_int -> System.Int32[] ; Is_uint -> System.UInt32[]
frozen build: Byte[] argument admitted = False ; SByte[] = True ; Int32[] = True ; Byte[1025] = False ; 17 arguments = False
```

Three things follow. (a) The claim is true on the stack the tests use, so the pattern list really could not
state "no raw byte array" — I could not break it, and the author's reason for the element-type comparison
stands. (b) The distinct `isinst` operands settle the "compiler mistake" alternative: the runtime matched a
token that names `System.SByte[]`. (c) I also tried to widen the new rule: `sbyte[1025]`, `string[1025]`,
`byte[1025]`, a `byte[,]`, a `byte[][]`, `object[]`, `char[]` and 17 arguments are all refused, 16 are
admitted, and a rank-1 non-SZ `int[*]` (which the OLD pattern list also admitted, since `int[*] is int[]` is
true here too) is admitted — so the rank+element rule is behaviourally the old list minus `byte[]`, not a new
hole.

The same probe tried to falsify the premise the deleted copies rest on — that a `ModValue` cannot be changed
from outside:

```
Items runtime type  : System.Collections.ObjectModel.ReadOnlyCollection`1[ModValue]
Fields runtime type : System.Collections.ObjectModel.ReadOnlyDictionary`2[String,ModValue]
(ModValue[])Items                : refused: not an instance of ModValue[]
(Dictionary<string,ModValue>)Fields : refused: not an instance of Dictionary`2
MemoryMarshal.TryGetArray        : True
writing through that array reached the value: True
```

So the container escape the stage A review found is **closed** — the cast a mod would need now fails, which is
what makes the stores' "no copy needed" argument true rather than hopeful. The one path left is the binary
leaf's backing array through `MemoryMarshal.TryGetArray` (N2).

## 5. Contract shape (the five new baseline lines, judged — no scan claims to enforce this)

| New line | Judgement |
|---|---|
| `member\|Stable\|….IModData.TryApplyShared(string key, ModValue value, ulong senderSteamId)\|method\|-> bool` | The payload is CUO's own data model (`ModValue`), the identity is a primitive, the return is a status `bool` — a typed definition, no envelope |
| `member\|Stable\|….IModData.TryGet(string key, out ModValue? value)\|method\|-> bool` | A typed model value; absence is the `false`, not a null payload |
| `member\|Stable\|….IModData.TrySet(string key, ModValue value)\|method\|-> bool` | Same; the `byte[]` envelope is gone rather than wrapped |
| `member\|Stable\|….IModState.TryGet(string key, out ModValue? value)\|method\|-> bool` | Same |
| `member\|Stable\|….IModState.TrySet(string key, ModValue value)\|method\|-> bool` | Same — and the FILE keeps its own byte leaf, which is not on this surface |

No added member takes or returns `object`, `Delegate`, `dynamic` or `IntPtr`; no handle is `object`; the one
byte-shaped value in the model is the explicit `Binary` leaf (`ReadOnlyMemory<byte>`, not `byte[]`), which is
a value inside the model rather than the API's envelope. The five tombstones are exactly right: each names a
key whose parameter list contained `byte[]`, each such key is genuinely absent (`ApiSurfaceGate` reports
`TOMBSTONE '{key}' is tombstoned and still listed as a live entry` if it is not, and that gate is green), and
each reason matches the diff ("the store keeps the typed model
instead of an opaque byte array" for the three runtime-data members, "the file stores the value's canonical
encoding" for the two state members) — the signatures are gone, not renamed in place with a different
parameter type and double-counted. The two members whose *type* changed without a tombstone in stage A
(`IModNetwork.MessageReceived`, `ModStatusUpdate.Value`) are the gate's `CHANGED` convention for keys without
a parameter list, and this change's five all have parameter lists, so all five correctly carry tombstones.

**One thing the change does not say, and should not be read as saying:** `IModNativeApi.TryInvoke(string
operation, object?[] arguments, out object? result)` is still an erased-key shape on the mod-visible contract
(the baseline records it as `Advanced`). That is pre-existing, deliberate debt with its own ticket
(`docs/backlog/todo/mod-api-typed-seams.md`, whose own table names that exact line), and the census row this
cycle moved says so ("the registry's own shape is `mod-api-typed-seams.md`") — so it is a known gap, not a
new one, but rule 15's letter is not yet satisfied by the contract as a whole.

## 6. Findings

### M1 (minor) — The byte-era prose is only partly retired, including one site this cycle owns

`src/CasualtiesUnknownOnline.Abstractions/IModContext.cs` — the property a mod reads to reach the store this
cycle moved still says:

> "Host-persistent per-mod state (opaque key/value bytes, scoped to this mod id)."

The values are `ModValue` now (`IModState.cs` was rewritten in this same diff to say exactly the opposite),
so the summary a mod author sees through IntelliSense contradicts the contract beside it. This site is **new**
— it was not on the stage A review's list.

Three sites the stage A review did list are still standing, and one of them is on the moodle path this
cycle's §2 declares byte-free:

- `src/CasualtiesUnknownOnline.Abstractions/IModMoodleRuntime.cs` — "the mod receives a plain
  `ModStatusMoodleRequest` (opaque payload + stable limb slot/name)", directly above
  `TryRegisterResolver(string statusId, Func<ModStatusMoodleRequest, string?> resolver)`; the request carries
  `ModStatusMoodleRequest.Value` (`ModValue?`) and the self-check's own §1.6 says "no `Payload` remains on the
  moodle path".
- `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModStatusStore.cs` — "It owns the ephemeral per-mod
  status slot table and defensive-copy mechanics" and "The store only keeps opaque mod payloads keyed by
  status id + player + optional limb slot": both sentences describe the code stage A deleted (there is no
  defensive copy left; `value = stored;` is the store's own comment "no defensive copy — a value is
  immutable").
- `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/ModMessageMsg.cs` — "The payload is opaque to the
  framework: the mod owns its serialization (JSON, hand-written, whatever its own dependencies allow)."; the
  bytes are CUO's canonical encoding of a `ModValue` and the mod owns no serialization at all.
  (`ModStatusProjectionKind.cs`'s "Opaque mod-owned status" / "the mod still owns its payload bytes" and
  `ModExample/ExampleMod.cs`'s "one opaque payload and one callback" are the same class.)

How this was verified: read every one of the stage A review's M5 bullets against the current file, and grepped
`src/` for `opaque|defensive copy` (note the hyphenated "defensive-copy", which a grep for `defensive copy`
misses). The stage A self-check's disposition table reduces that review's M5 to "a stale
`<see cref="TryHandleStatusPayload"/>` on the renamed interface — fixed", so the other bullets were neither
fixed nor named as dropped; the fix is one sentence per site (or a recorded debt line naming the ones left).

### M2 (minor) — The dated acceptance record was rewritten to carry test names the batch never ran, instead of using the excuse the gate provides

The diff edits `docs/evidence/acceptance/mod-data-sync-model-20260927.md` in two places, for example row 5:

> "…`ModDataTests.LocalOnly_AnyRoleCanReadWriteRemove_AndAValueIsImmutableNotCopied` and
> `ModDataTests.LocalOnly_IsIndependentBetweenHostAndGuest` are `Passed` — any role may read, write and
> remove…"

and row 3:

> "the rails are exercised in part: `ModDataTests.ValueCaps_AreEnforcedByTheEncoderWithoutSilentTruncation`
> (the value cap) … both `Passed`"

The record's own header fixes the run: batch `20260927-b`, "the run was performed on the working tree over
`a23a43b1`", commit, deployed artifact hash, and `docs/acceptance/AGENTS.md` rule 5 requires the record to be
reproducible from itself. At that commit the two cases were named
`LocalOnly_AnyRoleCanReadWriteRemove_AndCopiesAreDefensive` and
`ValueCaps_AreEnforcedWithoutSilentTruncation`, with different bodies (the new value-cap case additionally
asserts the encoder path, the null refusal and the rail's inclusive end). The row-level verdict survives —
I read both bodies and the behavioural half ("any role can read/write/remove", "the value cap is enforced")
is still covered — but the record now asserts `Passed` for code the batch never ran, i.e. it silently
re-attributes its evidence pointer.

Why the edit was made is not hard to see: the gate checks anchors in `docs/evidence/acceptance/`, and the
renames broke two of them. But the repository has a mechanism for exactly this, and it says so twice:

- `BacklogIntegrityGateTests` (the anchor gate's own summary): "Point-in-time records are deliberately NOT
  anchor-checked: a selfcheck, an audit or a closed ticket is the record of what was true when it was written,
  so **a test renamed afterwards must not force history to be rewritten**."
- `docs/evidence/ticket-anchor-exceptions.json`, empty and waiting: "Add here only for a HISTORICAL record
  inside a gated document — a red/green log or a dated pass narrative — never to excuse a live claim", and the
  gate fails on a stale exception, so the entry stays honest.

What would prove it addressed: either the two old anchors are excused through that file (with a reason naming
the rename), or the record keeps the old names and the rename is annotated in place ("renamed in the
2026-10-09 cycle to X; the batch ran the earlier name") — both leave the batch's judgement exactly as
recorded. Editing it silently is the one option that does not.

### N1 (nit) — The "no raw byte array" rule is a spelling rule, and the page states only the spelling

`ModNativeApiPolicy.IsSafeArray` admits `sbyte` among its element types on purpose (the ticket: "The
element-type comparison states it and keeps the signed twin admitted"), and my probe shows a `sbyte[]` is
admitted while a `byte[]` is refused — including on this runtime, where `(sbyte[])(object)new byte[]{1}`
**succeeds**, so a mod can hand the surface a signed view of its own byte buffer for free. `sbyte[]` is an
8-bit array: the envelope the change exists to remove is still reachable in a different spelling. The reach is
small and I looked: `ModNativeApiAdapter` passes `object?[]` to a CUO-registered operation, the only registered
operation (`local.player.state`) takes no arguments, and a mismatch against an operation's declared parameter
type fails in the provider — so nothing today can carry bytes through it. The ticket discloses the design
choice; `docs/en|zh/reference/mod-api.md` says only "A raw `byte[]` is not admitted any more", which a reader
will take as the stronger claim. What would prove it addressed: either drop `sbyte` from the admitted element
list (the model's binary leaf and real numeric arrays are the mod's paths now) or say the rule as it is — "no
8-bit element type is admitted" — on the page as well as in the ticket.

### N2 (nit) — The immutability the stores now depend on has one deep escape: `MemoryMarshal` reaches the binary leaf's array

The change deletes both stores' defensive copies because "a value is immutable" (`IModState`'s new doc: "a
value a mod read back can never be changed from anywhere, and only an explicit `TrySet` replaces what is
persisted"), and the ticket's reason for the leaf's spelling is that "a `ReadOnlyMemory<byte>` cannot be
mutated through the model". The container half of that is now true (probe: `Items` is a
`ReadOnlyCollection<ModValue>`, `Fields` a `ReadOnlyDictionary<string,ModValue>`, both casts refused — the
escape stage A's review found is closed). The leaf half is not absolute: `TryGetBinary` hands back the value's
own memory, `MemoryMarshal.TryGetArray` returns the array behind it, and writing through it changed what the
value holds (probe output in §4.4). So a mod that keeps a reference to a value it stored can change the
framework's copy without a `TrySet`. It is a nit, not a minor: it needs a deliberate `MemoryMarshal` call, a
mod is in-process trusted code, and nothing a peer sends can reach it. What would prove it addressed: one
sentence where immutability is claimed ("no typed path mutates a value") or a copy on `TryGetBinary`.

### N3 (nit) — Two scope sentences in the self-check are looser than the tree

§2: "Greps over `src/` for `byte[]` on a mod-visible member find none; the two byte shapes left are the ones
the census keeps on purpose — the state FILE's own field and the model's own `Binary` leaf." The first clause
is true; the second reads as a statement about `src/`, where a grep finds the frame's `ModMessageMsg.Payload`,
`ModChannel`'s `byte[]` parameters, `SaveArchiveEntry.Content` and the codec's own buffers as well — and it
names a different pair from the ticket's "the file's own leaf and the model's binary leaf are the only two
rows left" (the census's kept rows are the save archive's file content and the model's leaf; the mod-state
file's field is not a census row at all). What would prove it addressed: say "on the mod-visible contract" and
name the kept shapes exactly.

§1.8: "`grep Encoding.UTF8.GetBytes docs/{en,zh}` now finds only the evidence records" — a grep over
`docs/en` and `docs/zh` finds **nothing at all**; the evidence records live under `docs/evidence/`. The
substance (both blocks' samples are rewritten) is true and I verified it; only the sentence is wrong.

### N4 (nit) — The new "dropped by name" case does not assert the name

`ModStateTests.AStoredByteStringThatIsNotAValue_IsDroppedByNameAndTheRestOfTheTableLoads` asserts
`Count == 1`, the kept entry's round trip, and `TryGet("legacy") == false`. Nothing checks the drop line
`"[Mods] {ModId}/{Key} carries a stored value the framework cannot read — dropped ({Reason})."` — so the
"By Name" its name promises is proven by reading the production code, not by the case (the same
name-promises-more-than-the-assertion class the stage A review recorded, in a brand-new name). The apparatus
exists (`tests/CasualtiesUnknownOnline.Tests/Fakes/RecordingLoggerFactory.cs` with `Entries`/`Messages`,
reachable through `TestNode.Create(extraRegistrations:)`), so this is cheap; if wiring it into the store's own
logger is not worth it, rename the case after what it asserts.

## 7. What could NOT be falsified (checked, no gap)

- **A0, the premise, checked against the diff before anything else.** The 31 changed paths are documents,
  three `Abstractions` files (`IModData`, `IModState`, and `IModNativeApi`'s summary comment — no signature),
  six `Runtime/Session/Mods` files, three test files and the baseline. No `GameAdapter`, patch, protocol
  message, kernel, UI or input file is touched, so no player-facing gesture, view or entry changes and no
  entry mapping is owed; the delivery checklist's entry-mapping box should be filled with exactly that
  reasoning (the pre-reset revision did). The one user-reachable effect is through a mod: a host's
  `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin` from an earlier build is discarded whole, which is
  the file's own degradation contract, is stated in the ticket, in decision 249 and on the mod API page in
  BOTH blocks ("the file's version 1 — which held whatever bytes the mod chose — is refused whole rather than
  reinterpreted"; the how-to page states the generic rule, not the version), and costs pre-release mod state
  only, not game save data — the file is config-adjacent, not inside the save archive.
- **The version-1 refusal is necessary, not lazy.** I tried to construct a migration that keeps the old data:
  a version-1 entry is arbitrary mod bytes, and `0x00` is a valid new value (`false`), so any entry that
  happens to decode is indistinguishable from a real value and any entry that does not is lost anyway; there
  is no field that says which era wrote it. Refusing the whole file with a warning is the only option that
  cannot silently reinterpret data.
- **The one-encoder claim.** Nine `ModValueCodec` call sites, no second value predicate in `src/`; the two
  policies are one line each and pass their own cap and refusal through; the state file's load and persist use
  the same encoder as the write path.
- **The stores' sharing is safe for everything a mod can reach** (§4.4), including the container escape a
  previous review found open.
- **The five new baseline lines and the five tombstones** (§5), judged by hand rather than deferred to the
  green gate.
- **Reference integrity.** The moved ticket's index row, its five prose references, the MANIFEST row for the
  cycle's self-check, the three re-recorded alignment pairs (all 39 pairs' hashes reproduce), the terminology
  edit, and the absence of dangling `todo/…` references — plus the gate set, green apart from the checklist.

## 8. What could not be checked at all

- **No game process and no two-client session** were available, so a live mod's runtime data, a cross-session
  state read and a native operation call against the real registry are unverified here — the change's own
  limit says the same, and the acceptance batch is where those rows close.
- **The game's own runtime was not measured.** The array equivalence in claim 4 was measured on net48 (the
  runtime the tests use); the game runs Unity's runtime, which this review cannot start. It does not weaken
  the fix — the element-type comparison behaves identically on any runtime, and the failure mode if the game's
  runtime were stricter is "a change that was unnecessary", not "a wrong acceptance" — but the author's
  "measured on net48" should not be read as "measured where the mod runs".
- **The author's own probe is not in the tree**, so its "fourteen sample types × five `T[]` patterns" and its
  IL-token output cannot be audited; I reproduced the substance independently instead (§4.4) and reached the
  same conclusion.
- **No intermediate state of the uncommitted cycle is visible**, so the self-check's §4 claim that the file
  cases and the family run were "re-run after the change with every file at its committed content" can only be
  confirmed for the frozen end state, which I did.
- `dotnet format` was not run (it rewrites files) and nothing was deployed or rebuilt into a game tree, both by
  instruction.

## 9. Measured state, and this file's own row

Frozen-tree state as measured: build clean (0 warnings, 0 errors); behaviour suite **4850/4850** (net48); mod
family **461/461**; focused run **49/49**; normative gates **573 total, 572 passed, 1 failed** — the single red
being `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` with 8 required boxes unchecked, which
is the cycle's own declared position (the checklist is reset at the start of a cycle and filled, one line at a
time, before the commit; the ninth box, the release-cycle acceptance, is out of the development gate). Baseline
865 entries / 62 tombstones. 39 alignment pairs reproduce.

This report's own row in `docs/evidence/selfchecks/MANIFEST.md` is added by the author after this review
returns, so `SelfcheckManifestGateTests` goes red the moment this file exists. Measured after this file was
written, with the same command as §2: **573 total, 571 passed, 2 failed** —
`EverySelfcheckFile_HasExactlyOneManifestRow` naming this report, plus the checklist red above. That is
expected, and it is the only thing about the gate state that this report changes.

The probe built for §4.4 lived outside the repository and was deleted (its source, its executable and its
directory) before this report was written; nothing under the work tree was created, modified or deleted by this
review except this file.
