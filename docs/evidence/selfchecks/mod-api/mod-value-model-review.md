# The typed value model (stage A) — independent review

Independent adversarial review of the uncommitted change set "the typed value model replaces the opaque
envelope on the mod-facing wire surfaces", run in a fresh context against the FROZEN working tree at
`af2789be` (branch `master`; the dirty tree is the artifact under review). Risk tier: **FULL** — a
mod-visible contract, a wire encoding and cross-module call sites move together. Read-only: the only file
this review writes is this report. The change's own claims were attacked, never the evidence for them;
every claim below was re-opened in the tree, and the suites and probes that can run without the game were
run.

Ticket: `docs/backlog/todo/mod-api-no-opaque-envelopes.md` (*Frozen shape (2026-10-09)* and *What landed*).
Self-check: `docs/evidence/selfchecks/mod-api/mod-value-model-selfcheck.md`. Decision: entry 248.

## 1. Verdict

**No blocker. Two majors, three minors, four nits.**

The mechanism is sound and the shape the ticket froze is recognisably the code that is here: one canonical
encoding, one validator, budgets enforced while writing and while reading, refusals that name the path, and
the removed surfaces really removed rather than bypassed. I could not falsify the encoder/decoder contract
on any input a *mod* can build, and I could not break the decoder with a length lie, a depth bomb, a
negative count or a truncated payload.

What did fall:

- **M1 (major)** — `ModValueCodec.TryDecode` is not total: a peer-chosen payload whose map carries an
  EMPTY field name makes `ModValue.Map` throw `ArgumentException` out of the validator instead of
  returning `false` with a refusal. Reproduced against the frozen build. It is the one place where the
  change's own headline sentence ("a malformed or hostile payload is refused rather than recursed into")
  is false, and it is on the peer-chosen path.
- **M2 (major)** — the Chinese how-to page was NOT carried over with its English pair: it still shows the
  pre-change call table and a code sample that references an undefined `payload` and hands a `ModValue` to
  `Encoding.UTF8.GetString`, and it is missing the two facts the English bullet gained — while
  `docs/standard/alignment.txt` records the pair as confirmed.

The three minors are a real immutability escape through `Items`/`Fields`, a set of figures in the
self-check/checklist/ticket that the frozen tree does not reproduce, and stale documentation (plus one
dangling `cref`) left on the very surfaces this cycle retyped.

## 2. What was run (evidence)

- `dotnet test tests\CasualtiesUnknownOnline.NormativeGates.Tests\CasualtiesUnknownOnline.NormativeGates.Tests.csproj`
  → **573 passed / 573**, 0 failed (measured on the author's tree, i.e. before this report file existed;
  §7 gives the state after it). So `ApiSurfaceGateTests`, `DocumentationTreeGateTests`,
  `SelfcheckManifestGateTests`, `BacklogReferenceGateTests`, `DeliveryChecklist_NoIncompleteRequiredBoxes`
  and the shape gates are all green on the frozen tree — the self-check §4's "see §4" is satisfied.
- `dotnet test tests\CasualtiesUnknownOnline.Tests\CasualtiesUnknownOnline.Tests.csproj --filter "FullyQualifiedName~Tests.Mods"`
  → **460 passed / 460** — reproduces the self-check's family figure exactly.
- `dotnet test tests\CasualtiesUnknownOnline.Tests\CasualtiesUnknownOnline.Tests.csproj` → **4846 passed /
  4846** (net48) — reproduces the delivery checklist's figure exactly.
- Per-class runs (`--no-build --filter`): `ModValueCodecTests` **17**, `ModValueTests` **7**,
  `ModNullCollectionRuleTests` **28** (= 2 facts + 26 theory rows), `ModMessageTests` **14**,
  `ModPacketsTests` **20**.
- Independent probes against the frozen net48 build (Windows PowerShell 5.1 → .NET Framework 4.8, the same
  runtime the test host uses; assemblies taken from
  `tests\CasualtiesUnknownOnline.Tests\bin\Debug\net48`, `ModValueCodec` reached by reflection because it
  is `internal`): every kind round-trips; text 16384/16385 and binary 32768/32769 encode as the caps say;
  an 8-deep chain decodes and re-encodes, 9-deep and 40-deep are refused with the path; a 4097-value walk,
  a negative entry count and a binary leaf claiming 2 GiB with one byte left are all refused before
  anything is allocated; and the empty-field-name payload throws (M1). Method note for reproduction: the
  net48 build references `System.Memory, Version=4.0.1.0` and the shipped file is 4.0.5.0, so the probe
  needs an `AssemblyResolve` handler returning the preloaded `System.Memory`/`Unsafe`/`Buffers` assemblies
  — that is a harness detail, not a repository problem (the test host has the binding redirects).
- `git diff HEAD` read in full (63 paths), plus `git status --porcelain`; counts below are all produced
  from that frozen state (`git diff HEAD | Select-String …`, `Select-String -Path …`).

## 3. Claim by claim

| # | Claim (ticket *What landed* / self-check) | Verdict | Where it was checked |
|---|---|---|---|
| 1 | `ModValue`/`ModValueKind` are the typed model: closed kind set, immutable, structural equality, bounded `ToString()`, no null kind, binary leaf spelled `ReadOnlyMemory<byte>` | **Holds, with one escape (M3)** | `ModValueKind` declares exactly the seven kinds 0..6 and no null; `ModValue`'s constructors copy (`Binary` → `value.ToArray()`, `List`/`Map` → new array/dictionary); `Equals`/`GetHashCode`/`==` are structural; `ToString` caps at 256 rendered characters and 16 levels. The escape is the *views*, not the factories — see **M3** |
| 2 | `ModValueCodec` is the framework's ONE validator: canonical encoding, budgets enforced on BOTH write and read, refusals naming the path | **Holds, except M1** | `grep` finds `ModValueCodec.TryEncode/TryDecode` at four production sites (`ModNetworkAdapter`, `ModPacketsAdapter`, `ModStatusPolicy`, `ModLifecycle`) and nowhere else; encode and decode share the same six constants and the same depth/entry/count checks; a refusal always begins `"$"` and appends the path (`[3]`, `.hp`) built on the way out |
| 3 | Refusals name the path inside the model (`$.targets[3].hp: a number must be finite`) | **Holds** | Reproduced: a 9-deep payload refuses with `$[0][0][0][0][0][0][0][0][0]: the payload nests deeper than 8 levels`; a text over its cap refuses with `$: the text is 16385 bytes encoded, the cap is 16384` |
| 4 | `IModNetwork`, `IModPackets`, `IModPacketContext`, `ModStatusUpdate`, `IModStatusRuntime`, `IModStatusTransport`, `ModBodyFormulaProjection`, `ModLimbProjection` no longer carry `byte[]` | **Holds** | `grep` over `src/CasualtiesUnknownOnline.Abstractions` finds `byte[]` only in `IModData` (3 members), `IModState` (2), doc mentions, and `ModValue`'s own private field — exactly the stage-B rows the ticket keeps |
| 5 | `ModPayloadCodec` and the `DataContractSerializer` decode seam are deleted | **Holds** | `src/CasualtiesUnknownOnline.Abstractions/ModPayloadCodec.cs` is gone; `grep` finds no `ModPayloadCodec`, no `DataContractSerializer`, no `[DataContract]`/`[DataMember]` under `src/`; the diff removes 22 attribute lines (`[DataContract]` ×3 on the three travelling types, `[DataMember]` ×19) |
| 6 | The per-delivery byte copy in `ModPacketsAdapter` is DELETED rather than moved | **Holds** | Both `(byte[])payload.Clone()` sites are gone from `Route` and `RunLocal`; `grep` finds no `Clone()` left in the mod domain (only the stage-B stores and `WorldEventSync`); `PacketContext.Value` is the caller's own instance |
| 7 | The null-collection census shrank by one member for a real reason (`ModNullCollectionRuleTests`, 26 rows; the model's `Items`/`Fields` are a named group, not rows) | **Holds** | `ExpectedConstructedMemberCount = 26` with the reason written beside it; the suite is 28 cases = 26 rows + 2 facts, i.e. the rows really are 26; `ReadOnlyValueViews` names `ModValue.Fields`/`ModValue.Items` and the assembly-wide equality assertion still fails on a new collection member in either direction |
| 8 | "the API baseline (27 tombstones)" / "27 removals carry a reason" | **False — 25** | `git diff HEAD -- docs/contracts/abstractions-api-baseline.txt \| Select-String '^\+\*REMOVED\*'` → **25**; the file holds 57 tombstones in total; the surface is 865 entries exactly as claimed. See **M4** |
| 9 | "the whole mod family, 460 cases" / "behaviour 4846/4846 (net48)" / "gates 573/573" | **Holds exactly** | The three suite runs above |
| 10 | "`ModValueCodecTests` (20 cases)" / "`ModValueTests` (12 cases)" / "the twelve `FromPayload` bodies it served" / "five defensive-copy sites" / "`ModValueCodec` 479 lines … `ModValue` 380 lines" | **False — 17, 7, 3, 8, 581/409** | Per-class runs and `Get-Content … .Count`; the `Clone()` and `FromPayload` counts come from the diff itself. See **M4** |
| 11 | "grep finds no `byte[]` on these surfaces, and the only remaining `byte[]` in `Abstractions` is the save archive's file content" | **First half holds, second half false** | The remaining `byte[]` in `Abstractions` is `IModData.TryGet/TrySet/TryApplyShared` and `IModState.TryGet/TrySet` — the same self-check's §5 says so. `SaveArchiveEntry` is not in `Abstractions` at all (`src/CasualtiesUnknownOnline.Runtime/Persistence/SaveArchiveEntry.cs`: `internal sealed record SaveArchiveEntry(string Path, byte[]? Content)`) |
| 12 | Both document blocks updated, the alignment pairs re-recorded, terminology gained `value` and lost the "opaque bytes" payload definition | **Holds for the reference block and the registries; the how-to pair is not carried over** | `docs/en|zh/reference/mod-api.md` agree section for section; `terminology.txt`, both glossaries, `protocol-messages.md` and `permissions-and-security.md` agree; the recorded alignment hashes reproduce (`git hash-object`). See **M2** for the how-to pair |

## 4. The apparatus, not the wording

### 4.1 Does encode/decode round-trip every kind? (question 1)

Yes for everything a MOD can build. Encoded and decoded through the real codec, comparing with
`ModValue.Equals`: `Boolean(true/false)`, `Integer(0 / long.MinValue / long.MaxValue)`,
`Number(0.0 / -0.125 / double.Epsilon)`, `Text("")`, `Text` with quotes and a tab (the suite adds
non-ASCII text), `Binary` of 0/1/4 bytes, empty `List` and empty `Map` — all equal after the round trip,
and the nested `Map(List(Map(("deep", Text))))` case is the suite's own
(`ModValueCodecTests.Containers_RoundTripIncludingNestingAndEmptyOnes`, green in my run).
Budgets are inclusive at the end the ticket describes:

- text exactly 16384 bytes encodes and decodes; 16385 is refused by encode (`$: the text is 16385 bytes
  encoded, the cap is 16384`) and by decode (`$: the text is 16385 bytes, the cap is 16384`);
- binary exactly 32768 bytes encodes and decodes; 32769 is refused by both;
- an 8-deep list chain decodes and re-encodes; 9-deep is refused **before descending further** and
  40-deep is refused by the same check (so the depth bound is a check, not a stack budget);
- `ExtremeValues` are not a special case: `long.MinValue`/`long.MaxValue` round-trip because the encoding
  is a fixed 8-byte little-endian field and the decoder reassembles it as
  `(uint)low | ((long)high << 32)`.

The one asymmetry I found is M1: **encode cannot produce what decode throws on**, so there is no
"encode succeeds, decode refuses" pair for a *mod-built* value — but there is a peer-built payload the
decoder cannot refuse.

Counterexamples I tried to construct and could NOT: a text that decodes to an unpaired surrogate
(rejected — .NET's strict UTF-8 decoder refuses surrogate code points and overlong forms, so a decoded
text is always re-encodable); a graph whose decode succeeds but whose re-encode refuses (the caps and the
counters are the same on both sides, so the decoded value is always inside every budget — I verified the
8-deep chain re-encodes and a 32768-byte leaf re-encodes); a value that encodes to exactly the 64 KiB
rail (the repository's own `ModValues.AtTheRail()` pins it, and the arithmetic in that helper is correct:
`5 + 4*(4+1+5) + 65491 = 65536`).

### 4.2 Is the decoder robust against a hostile payload? (question 2)

Asked as the four specific hazards:

- **A length trusted before the remaining bytes are checked — no.** `TryReadBytes` computes
  `var remaining = payload.Length - index;` and refuses `length < 0 || length > remaining` *before*
  `bytes = new byte[length]`, with the cap check after it; `TryReadCount` refuses `raw < 0 || raw > MaxEntries`
  before `new ModValue[count]`. Probe: a binary leaf whose declared length is `int.MaxValue` with one byte
  behind it refuses with `$: a binary leaf of 2147483647 bytes is beyond the 1 bytes left in the payload`
  and allocates nothing.
- **Recursion not depth-bounded BEFORE it descends — no.** `TryRead` checks `depth > MaxDepth` at entry,
  before reading a tag, and passes `depth + 1` to children; `TryReadList`/`TryReadMap` cannot loop without
  passing through it. Probes above.
- **Allocation proportional to an unvalidated number — no.** Entries are capped at 1024 and lengths
  against the bytes actually left, so a worst-case 64 KiB payload allocates well under a megabyte; the
  value counter (4096) bounds the node count independently of the entry caps.
- **A refusal that echoes attacker-controlled text into a log line unescaped — no, for the codec.**
  The only attacker-controlled text a refusal interpolates is a map field name, and it goes through
  `Sanitize`, which maps every character outside `' '..'~'` to `?`; lengths and tags are numbers. The
  *receive* path's own drop logs are a different story — nit **N4**.

The one hole is M1, and it is on exactly the path these four questions are about: a handshaken peer
(`IsModMessageSender` admits nothing else) can put ten bytes on the wire and turn the documented
"refused with the refusal, exactly like the over-cap drop beside it" into an unhandled
`System.ArgumentException`.

### 4.3 Census, second spellings and removals (questions 3 and 4)

- **No stage-A row still carries `byte[]`**, and no surface carries both spellings: `grep` finds no
  `TryHandleStatusPayload`, no `IModPacketContext.Payload`, no `ToPayload`/`FromPayload` on the four
  retyped types, no `ModPacketPolicy.IsValidPayload` (deleted, zero references), and no second
  overload pair anywhere. The remaining `byte[]` in `Abstractions` is the three `IModData` members and the
  two `IModState` members — stage B, as the ticket's staging section says, and `docs/en|zh/reference/mod-api.md`
  still documents those two surfaces as opaque bytes, which is truthful.
- **The removals are real, not bypassed.** `ModPayloadCodec.cs` is deleted rather than emptied; the three
  travelling types lost their serialization attributes (22 attribute lines) and their payload pairs;
  the eight defensive `Clone()` sites are gone and no copy was re-introduced elsewhere on the moved path;
  `ModChannel`'s byte fields are the frame's transport detail, which the ticket explicitly keeps.
- **Every new member has a named consumer.** `ModValue`'s factories/accessors are read by the codec, the
  two projections, `ModStatusUpdate`, the example mods and the suites; `TryEncode` by the two adapters and
  the status policy; `TryDecode` by the lifecycle receive path; the public `Field*` constants by the
  projections and `ModStatusUpdate` (and they are the wire contract the pages document). Nothing added is
  a form nothing reads, so the mechanism-inventory rule is satisfied.
- **The tombstones' reasons are true.** Each of the 25 new `*REMOVED*` lines names a key that really is
  absent from the surface (the gate would report `TOMBSTONE … still listed` otherwise) and a reason the
  diff supports ("a mod's own message carries a typed ModValue now…", "a status value is a ModValue now…",
  "the projection is a value of the model…"). The two `Stable` members whose *type* changed without a
  tombstone — `IModNetwork.MessageReceived` and `ModStatusUpdate.Value` — are `CHANGED` by the gate's key
  rule (a member key is owner + name + parameter list, a property/event key has no parameter), which is
  the convention the baseline file's own header states ("ANY line change is an API change").

### 4.4 Do the tests pin the claimed behaviour? (question 5)

Mostly yes, and the status family is the strongest part of the suite: `ModStatusUpdateTests` pins an
unknown field ignored, a missing field refused, a set frame without a value refused, and the round trip of
a nested value; `ModBodyFormulaProjectionTests`/`ModLimbProjectionTests` pin the field names, the absent
field (zero for the body formula, `null` for the limb overlay) and the wrong-kind refusal;
`ModMessageTests` drives the real three-node star and pins the structurally-equal value at the receiver,
including a value whose encoding is exactly the rail. Cases I judge **non-discriminating**:

- `ModPacketsTests.OneImmutableValue_ServesTheWholeDelivery` — it asserts `[value, value]` per copy, which
  is also what a per-delivery *copy* would produce; it pins the codec's round trip of a nested value, not
  the immutability its name claims. (The deletion of the copy is proven by the diff, not by this case; no
  case can distinguish the two, because a copy of an immutable value is indistinguishable — so the honest
  fix is to rename it after what it pins, not to add an assertion.)
- `ModMessageTests.AFullyPaddedFrame_IsAcceptedByTheRailAndDroppedAsAValue` — its only assertion is
  `Assert.Empty(Echo(w.Host).Received)`, which passes whether the frame left the sender and was dropped as
  a value (its claim) or never left at all. Its sibling `ModWithoutSendNetworkMessage_InboundFrameIsDropped`
  shows the stronger idiom in the same file (`RecordInbound` proves the frame arrived at the transport).
- `GlobalProjectionFrameworkTests` still writes `ModValues.Ints(1, 2, 3)` into a slot declared
  `ModStatusProjectionKind.BodyFormula`; the new projection readers refuse that value, so the case now
  proves the read model's presence bookkeeping only — which is what it always proved, so nothing is lost,
  but a reader could mistake it for a projection case.

The `ModNullCollectionRuleTests` census is genuinely two-directional: it asserts the equality of the
discovered rows with the union of the named groups, then `Assert.All` that each row reads non-null on a
fresh instance — a new collection member anywhere in the assembly fails it.

## 5. Contract shape (the three questions, asked of the NEW surface)

- **A payload is a typed definition or the framework's own data model.** The added members take
  `ModValue` — CUO's own model — and the added constants are field names. No `byte[]` enters the baseline:
  25 lines leave it (each with a tombstone) and **75 member lines plus 2 type lines** arrive, of which 29
  belong to `ModValue`/`ModValueKind`, 20 are the field-name constants the wire contracts now use, 6 are
  the `ToValue`/`TryFromValue` pairs, and the rest are the same members of the nine interfaces and four
  DTOs retyped to `ModValue` (`git diff HEAD -- docs/contracts/abstractions-api-baseline.txt |
  Select-String '^\+member\|'`).
- **A handle is a CUO-defined type, never `object`.** `ModValue`, `ModValueKind`, `IReadOnlyList<ModValue>`,
  `IReadOnlyDictionary<string, ModValue>` — no `object`, no `Delegate`, no `dynamic`, no `IntPtr`.
- **A binary value is an explicit leaf, not the envelope.** `ModValue.Binary(ReadOnlyMemory<byte>)` is one
  kind of seven, documented as the place a mod says "this really is bytes"; the envelope it replaced is
  gone from every surface this cycle moved. The spelling is deliberate and defensible: a mod cannot mutate
  what it hands over, and the framework never interprets it.
- **No scan claims to enforce this.** The rule lives in the review, as `AGENTS.md` rule 15 requires; the
  baseline gate only records the surface. The claim in the pages is a claim about the review, not about a
  checker — correct.

## 6. Findings

### M1 (major) — `TryDecode` throws on a peer-chosen payload: a map field name of zero length reaches `ModValue.Map`, which refuses it by exception

`ModValue`'s factory contract is that an empty field name is a programming error:
`src/CasualtiesUnknownOnline.Abstractions/ModValue.cs` — "A map field name cannot be null or empty." —
`throw new ArgumentException(…)`, pinned by `ModValueTests.ANullArgumentIsAProgrammingError`
(`Assert.Throws<ArgumentException>(() => ModValue.Map(("", ModValue.Integer(1))));`). The decoder reads a
field name with `TryReadText(payload, ref index, MaxKeyBytes, "field name", …)`, which accepts a length of
0, checks only for a *duplicate* key, and then hands the rows to that same factory:
`ModValueCodec.TryReadMap` ends in `value = ModValue.Map(fields);`. So the value the model refuses to be
*built* is built by the decoder — as an exception, not a refusal.

Reproduced against the frozen build (ten bytes: `07 01 00 00 00 | 00 00 00 00 | 00` = map, one entry, a
zero-length name, a `false` value):

```
HOSTILE EMPTY-KEY MAP -> THREW: System.ArgumentException :: A map field name cannot be null or empty.
Parameter name: fields
CONTROL key='a' -> RETURNED=True value={"a": false}
NESTED EMPTY-KEY MAP -> THREW: System.ArgumentException :: A map field name cannot be null or empty.
```

The control shows the same payload with a one-character name decodes correctly, so the trigger is the empty
name and nothing else. Why it matters: `ModValueCodec`'s own contract is "a malformed or hostile payload is
refused with a named path instead of being recursed into", and decision 248 restates it; the receive path
(`ModLifecycle.OnModMessageReceived`) is written as `if (!ModValueCodec.TryDecode(payload, ModChannel.MaxPayloadBytes, out var value, out var refusal)) { LogWarning(…); return; }`,
so the intended Warn + refusal is replaced by a thrown `ArgumentException` that unwinds through
`ModMessageHandler.Handle` and `PacketDispatcher.OnMessageArrived` to the transport's own catch-all
(`SteamTransport.Poll`: `catch (Exception ex) { _log.LogError(ex, "SteamTransport: one received message failed — continuing with the rest of the batch."); }`).
The session survives on the Steam path — that is why this is a major and not a blocker — but a peer's ten
bytes cost an ERROR with a stack trace instead of the designed one-line refusal, the frame's own handler
log is skipped, and any caller that trusts the `bool Try` shape (stage B's stores, or a suite calling
`TryDecode` directly) gets an exception from a validator. The blast radius is also transport-dependent:
every `TryDecode` caller inherits whatever the transport above it does with a throw.

What would prove it fixed: `ModValueCodec.TryReadMap` refuses a zero-length name itself (a refusal naming
the position, e.g. `$: the field name at index 0 is empty`), **or** the factory's rule is expressed as a
refusal on the decode side and a case pins the exact ten-byte payload above to `false` with a refusal that
starts at `$`. A regression case must exist either way: the current suite has a duplicate-field case and a
truncated case, but no empty-name case, which is why the next-cycle fix needs its own red.

### M2 (major) — The Chinese how-to page was not carried over: it shows the pre-change calls and a sample that cannot compile

`docs/zh/how-to/send-a-network-message.md` still carries the payload-shaped surface on every line the
English page changed, while the alignment registry records the pair as confirmed:

- the call table still reads `| SendToHost(payload) | … |`, `| SendToPeer(steamId, payload) | … |`,
  `| Broadcast(payload) | … |`, `| MessageReceived += (sender, payload) | … |` — the English table now
  reads `value` in all four rows and `docs/zh/reference/mod-api.md` (edited in this same cycle) also reads
  `value`;
- the handler is still `context.Network.MessageReceived += (sender, payload) => … Encoding.UTF8.GetString(payload)`
  — `MessageReceived` now delivers a `ModValue`, so the snippet does not compile;
- the new line `var value = ModValue.Map(("kind", ModValue.Text("ping")), ("at", ModValue.Integer(Environment.TickCount)));`
  was added, but the two lines that use it were not: `context.Network.Broadcast(payload);` and
  `context.Network.SendToHost(payload);` reference a variable that no longer exists — the page's sample is
  broken in both directions;
- the loss bullet still reads "载荷上限是 **64 KiB**，发送端拒绝、接收端再检查一次；" while the English bullet
  now adds two facts ("capped at 64 KiB **encoded**" and "a value the framework cannot encode is refused
  with one log line naming the path inside it").

Why it matters: the ticket's §5 makes "the two how-to pages that show `Encoding.UTF8.GetBytes` rewritten"
a deliverable, and `docs/AGENTS.md` is binding — "a page is added, renamed, moved or deleted on both sides
in the same change" and "Editing one side obliges the other in the same change". A mod author reading the
Chinese block is told the API still takes bytes and is handed a sample that will not build. The alignment
record does not catch it: the hashes reproduce (`git hash-object` gives exactly the `b2d302cc…` in
`docs/standard/alignment.txt`), because the gate compares hashes, not facts. The English page's own table
was also left with `payload` in its rows, so the English side is half-done in the same way, just not
broken.

What would prove it fixed: both blocks' tables, handlers, sends and loss bullets carry the same facts
(one `ModValue`, the encoded cap, the refusal line), and the alignment pair is re-recorded after the edit.

### M3 (minor) — `ModValue` is not immutable in practice: `Items` and `Fields` hand back the value's own array and dictionary

The ticket's reason for deleting every defensive copy is "every factory copies what it is handed, so a
value can be shared, cached and read from two threads without a defensive copy", and the status store
relies on it (`src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModStatusStore.cs`: `value = stored;`,
`entry.BodyValues[playerSteamId] = value;` — "the store keeps the value it was handed (no defensive copy —
a value is immutable)"). The two views are typed as read-only but their runtime types are the backing
storage:

```
decoded list = [1, 2]  (Items runtime type = CasualtiesUnknownOnline.Abstractions.ModValue[])
after writing through the IReadOnlyList reference the value renders as: [777, 2]
decoded map = {"a": false}  (Fields runtime type = System.Collections.Generic.Dictionary`2[System.String,ModValue])
after writing through the IReadOnlyDictionary reference: {"a": false, "smuggled": 5}
TryGetField(smuggled) = True
```

So `((ModValue[])value.Items)[0] = …` and `((Dictionary<string, ModValue>)value.Fields).Add(…)` both succeed
and mutate a value already held by the framework — including one inside the status store, where the change
silently changes what the store contains without a revision bump or a `StatusChanged` event, and one inside
a live delivery. Why it is minor rather than major: a mod is in-process trusted code that could reach the
same state through reflection; the damage is to the framework's own invariants, not to a boundary. Why it
should still be fixed: it is a two-line change (`System.Collections.ObjectModel.ReadOnlyCollection` /
`ReadOnlyDictionary` wrappers), the immutability claim is the load-bearing reason the defensive copies were
deleted, and the census even calls these members "read-only views"
(`ModNullCollectionRuleTests`: "the model's read-only collection members: the items of a list and the fields
of a map are views the framework answers"). Note the concurrency edge the same hole creates: a mod that
mutates a map while the framework is encoding it turns `TryEncode`'s `foreach (var pair in fields)` into an
`InvalidOperationException` that escapes the validator — the same class of defect as M1, one call site over.

What would prove it fixed: the properties return wrappers (the cast then fails), or the case that pins the
model's immutability asserts that a write through the view is impossible rather than that the factories
copy.

### M4 (minor) — Figures in the self-check, the delivery checklist and the ticket that the frozen tree does not reproduce

Each of these was produced from the frozen tree with the command shown; the review template's rule is that a
number must be reproducible from the tree it describes.

| Claim (where) | Quoted | Frozen tree | Command |
|---|---|---|---|
| ticket *What landed*, self-check §1.10, §4 | "the API baseline (27 tombstones)" / "27 removals carry a reason" | **25** tombstones added (57 in the file) | `git diff HEAD -- docs/contracts/abstractions-api-baseline.txt \| Select-String '^\+\*REMOVED\*'` |
| self-check §1.1 | "`ModValueTests` (12 cases)" | **7** | `dotnet test … --filter "FullyQualifiedName~ModValueTests"` |
| self-check §1.2; checklist | "`ModValueCodecTests` (20 cases)" | **17** | `dotnet test … --filter "FullyQualifiedName~ModValueCodecTests"` |
| checklist (structure review) | "`ModValueCodec` (479 lines including its docs …) and `ModValue` 380 lines" | **581** and **409** | `(Get-Content src\…\ModValueCodec.cs).Count` |
| checklist (structure review) | "five defensive-copy sites" | **8** `Clone()` sites removed (2 in `ModPacketsAdapter`, 6 in `ModStatusStore`) | `git diff HEAD \| Select-String '^-.*Clone\(\)'` |
| checklist (structure review) | "the twelve `FromPayload` bodies it served" | **3** `FromPayload` members removed by this diff (the nine content DTOs lost theirs in the previous cycle) | `git diff HEAD \| Select-String '^-.*(From\|To)Payload'` |
| self-check §2 | "the only remaining `byte[]` in `Abstractions` is the save archive's file content" | the remaining `byte[]` is `IModData` ×3 and `IModState` ×2; `SaveArchiveEntry` is `internal` in **Runtime**, not in `Abstractions` | `grep -n 'byte\[\]' src\CasualtiesUnknownOnline.Abstractions`, `Select-String -Path src -Pattern 'class SaveArchiveEntry'` |

Two of these matter beyond bookkeeping. "479 lines" understates `ModValueCodec` by 102 lines against a
600-line gate it is actually 19 lines from (`docs/architecture-debt.json` carries no entry for it, so the
gate is green — the number is the only wrong thing, not the file). And the §2 sentence is contradicted by
§5 of the same document, so a reader who trusts §2 will believe the `Abstractions` surface is byte-free
today. The likely origin of the 27 is 25 removed keys + the two `CHANGED` members
(`IModNetwork.MessageReceived`, `ModStatusUpdate.Value`), which is a real count of "surface lines that did
not survive unchanged" — it is just not a tombstone count. What would prove it fixed: the figures corrected
in the self-check, the checklist and the ticket, each naming what it counts.

### M5 (minor) — The retyped surfaces keep their old documentation, including one dangling `cref`

Documentation is part of the contract in this repository (the pages were rewritten for exactly this reason),
and the XML comments are what a mod author reads through IntelliSense. Left behind:

- `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/ModMessageMsg.cs` — "The payload is opaque to the
  framework: the mod owns its serialization (JSON, hand-written, whatever its own dependencies allow).",
  and on the field "The mod-owned payload …", and in the packet-id summary "one opaque payload per mod".
  The frame's bytes are CUO's canonical encoding of a `ModValue` now; the mod owns no serialization at all.
- `src/CasualtiesUnknownOnline.Abstractions/IModStatusTransport.cs` — "Guest only:
  `<see cref="TryHandleStatusPayload"/>` parses a host-originated typed frame …". That member was renamed to
  `TryHandleStatusUpdate` **by this diff**; the `cref` now resolves to nothing (the build does not generate
  XML docs, so no CS1574 catches it).
- `src/CasualtiesUnknownOnline.Abstractions/IModMoodleRuntime.cs` — "the mod receives a plain
  `ModStatusMoodleRequest` (opaque payload + stable limb slot/name)". The request carries `Value` now.
- `src/CasualtiesUnknownOnline.Abstractions/ModStatusProjectionKind.cs` — "which typed payload shape a mod
  is publishing", "`None` keeps the value opaque and framework-owned", "the mod still owns its payload
  bytes", and on the `None` member "Opaque mod-owned status". The edited pages say the opposite ("`None`
  statuses are the mod's own value and are never interpreted"), so the API doc and the page now disagree
  about the same member.
- `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModStatusStore.cs` — "defensive-copy mechanics", "The
  store only keeps opaque mod payloads keyed by status id + player + optional limb slot", and above
  `GetProjectionSnapshots` "it returns defensive copies" — the copies this cycle deleted are still
  advertised two paragraphs above the lines that stopped making them.
- `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModStatusAdapter.cs` — "mod-id scoping, and defensive
  copies live here".
- `src/CasualtiesUnknownOnline.GameAdapter/ModStatus/ModStatusVanillaProjection.cs` — "it never touches
  arbitrary opaque status payloads"; `src/CasualtiesUnknownOnline.ModExample/ExampleMod.cs` — "one opaque
  payload and one callback".

What would prove it fixed: each comment re-read against the code beside it (one sentence each), and the
`cref` renamed with the method that this diff renamed.

### N1 (nit) — The status store validates a value by encoding it and throwing the bytes away

`ModStatusPolicy.IsValidValue`: `value is not null && ModValueCodec.TryEncode(value, MaxValueBytes, out _, out _)`
— the only validator, which is the ticket's design, but the encoding is discarded and re-done by
`ModStatusTransport` (and, on the guest, the value was already decoded from bytes that passed the same
budgets). The path is per-write: `ModStatusStore.TrySetBodyValue` is called from `TryApplyBodyStatus` for
every received status frame, so a value near the cap allocates ~64 KiB, twice, per write. The old check was
`value.Length <= MaxValueBytes` — O(1). Not a correctness defect; worth a look because the fix is free
(pass the encoded bytes the caller already produced, or validate once at the transport and let the store
trust its caller's `Try`). What would prove it addressed: either the double encode disappears, or a comment
records the deliberate cost.

### N2 (nit) — Two cases that cannot fail for the reason their names give

`ModPacketsTests.OneImmutableValue_ServesTheWholeDelivery` (asserts two equal values per copy — a
per-delivery copy would satisfy it; the deletion is proven by the diff, and no assertion can distinguish an
immutable value from its copy) and `ModMessageTests.AFullyPaddedFrame_IsAcceptedByTheRailAndDroppedAsAValue`
(the claim "accepted by the rail" is not asserted; `Assert.Empty(Echo(w.Host).Received)` also passes if the
frame never left). Details in §4.4. What would prove it addressed: the first renamed after what it pins,
the second asserting the transport saw the frame the way its sibling does with `RecordInbound`.

### N3 (nit) — The frozen shape's accessor list names a `Count` that does not exist, and its exception rule is one type off

- `docs/backlog/todo/mod-api-no-opaque-envelopes.md`: "Accessors: `Kind`, `TryGetBoolean`, `TryGetInteger`,
  `TryGetNumber` (accepts `Integer` as a widening), `TryGetText`, `TryGetBinary`, `Items`/`Fields` …,
  `TryGetField(string name, out ModValue value)`, `Count`." The baseline records 22 `ModValue` members and
  none of them is a `Count`; `ModValue.cs` has no such member (the word appears once in a doc sentence).
  The two human pages do not mention it, so only the ticket is wrong.
- The same section: "A null argument is an `ArgumentNullException` (a programming error in the mod's own
  code) except a null `params` array, which is an empty container." A null or empty *field name* is an
  `ArgumentException` (`string.IsNullOrEmpty(key)` → `throw new ArgumentException("A map field name cannot
  be null or empty.", …)`), not an `ArgumentNullException`; the in-tree case pins
  `Assert.Throws<ArgumentException>` for it. Both are programming errors and both are documented as such —
  the exception type is simply one base class lower than the ticket states.

What would prove it addressed: the ticket's accessor list and exception sentence corrected (or the `Count`
accessor added, if it was meant as a real convenience).

### N4 (nit) — The receive path's drop logs echo a peer-chosen `ModId` unescaped (pre-existing, and this diff adds one more line to the family)

`ModLifecycle.OnModMessageReceived` drops an unknown mod with
`_log.LogWarning("[Mods] message for {ModId} from {Sender} — no local mod with that id, dropped.", msg.ModId, sender);`
— `msg.ModId` is raw frame text (`ModMessageMsg.ModId` is a protobuf string with no length or grammar
bound on the receive side), and the new decode-failure line beside it does the same with the same field.
The file already knows the rule: the packet id is checked first, with the reason written down — "The packet
id is mod-authored text that a peer chose: bound it by the registration grammar BEFORE it reaches a log
line, so an attacker-sized id is named by its length instead of being echoed." A handshaken member can
therefore write arbitrary text (newlines included, up to the transport's frame cap) into the host's log
through a field this cycle did not touch but did extend. The codec's own refusals are *not* affected
(§4.2). What would prove it fixed: the mod-id echo goes through the same bound the packet id already has
(or is replaced by its length), with a case that delivers an oversized id.

## 7. What could NOT be falsified, and what could not be checked

**Not falsified (checked, no gap).** The encode/decode contract for everything a mod can build, including
the budget ends (§4.1); the four hostile-input hazards except M1, each falsified by code reading *and* by a
probe (§4.2); the claim that the encoding is CUO's own and invisible to mods (`ModValueCodec` is `internal`
to Runtime, the baseline adds no byte-shaped member, `Abstractions` holds no codec); the claim that the
refusals cannot forge a log line (only `Sanitize`d field names and numbers are interpolated); the claim
that every moved surface has exactly one spelling (grep over `src/`, `tests/`, `docs/`); the tombstone
reasons (25 keys, all absent, all reasons supported by the diff); the 865-entry surface, the 460-case mod
family, the 573 gates and the 4846-case behaviour suite (all reproduced); the `ModNullCollectionRuleTests`
census in both directions (26 rows + 4 constructor-only + 2 named views, with an assembly-wide equality
assertion that a new member cannot slip past); the parity of the two `mod-api.md` blocks, section for
section, including the `None`-status bullet, the status-frame field list and the two projection bullets;
the other four edited pairs (`glossary`, `protocol-messages`, `permissions-and-security`, `add-a-ui-panel`)
and the alignment hashes. I also constructed and *dropped* two candidate defects rather than reporting
them: signed zero is not an `Equals`/`GetHashCode` inconsistency on net48
(`(-0.0).GetHashCode() == 0 == (0.0).GetHashCode()`, `Equals` true — measured), and the 64 KiB rail being
measured on the encoded frame (so the largest publishable shared status is slightly smaller than a
64 KiB value) is the same shape as before the change and is recorded as a limit in the self-check §5 and
on the pages.

**Not checkable here.** No game process and no two-client session were available, so the live rows — a
mod's message and a shared status actually arriving across two clients — remain unverified; the change's
own limit ("No game process was run this cycle") is the same statement, and the acceptance batch is where
it closes. `dotnet format` was not run (it rewrites files) and the deployment was not rebuilt or verified,
both by instruction. I also cannot see any intermediate state of an uncommitted cycle, so the self-check's
claim that the map round-trip defect "was red on the first run" is unverifiable — I can only confirm that
the frozen code round-trips a map whose field name is written bare and read bare (the encoder's
`TryWriteKey` and the decoder's `TryReadText` for a `field name` agree), which is consistent with the story.

**Cycle-close work this report cannot do, by instruction.** I did not touch any source, test or document
other than this file: so `docs/evidence/selfchecks/MANIFEST.md` has no row for this report
(`SelfcheckManifestGateTests.EverySelfcheckFile_HasExactlyOneManifestRow` is red from the moment this file
exists), the delivery checklist's remaining boxes stay as the author left them, and M1/M2/M3/M5 are for the
author to land in this cycle's commit — M1 and M2 in particular are the two I would not ship without.
Measured state after this file was written: gates **573 total, 572 passed, 1 failed** — the one failure
being `EverySelfcheckFile_HasExactlyOneManifestRow` naming `mod-api/mod-value-model-review.md`; adding that
one row returns the suite to the author's declared 573/573.
