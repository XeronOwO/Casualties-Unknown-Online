# CUO world archive: package format and world repository

Normative format contract for the CUO save system. It defines the on-disk shape only;
the code that produces and consumes it lives in
`src/CasualtiesUnknownOnline.Runtime/Persistence/` and is staged by the tickets listed in
`docs/backlog/in-progress/save-system-mid-run-and-layer-end.md`.

Decisions 162–166 in `docs/decisions/active.md` record the user's choices that this document
implements. Where this document and the implementation disagree, the implementation is wrong.

## 1. Model

A **world** is the unit of identity. It owns one folder archive holding the current state and
N compressed archive backups — the Minecraft world layout, plus backups.

- The save system is **independent of the native `save.sv`**. CUO never writes `save.sv`, and a
  restored world never depends on it. The native file format cannot express a mid-run cut, so a
  coexistence scheme would only create two sources of truth.
- The **host is the only writer** (`AGENTS.md`: the host is the only save authority). Guests
  hold their own local settings only; they never write a world archive.
- The **world repository root** is `<CUO data root>/cuo/saves/`, where `<CUO data root>` is the
  game's `Application.persistentDataPath`. Being under the persistent-data root keeps the
  archive out of the game installation, so a game update or reinstall never touches it.
- Guests store nothing here; a guest's client-side copy would create a divergent second truth.

## 2. Directory layout

```text
<CUO data root>/cuo/saves/
  index.json                                  # world list + last-opened pointer
  <worldId>/                                  # one folder archive per world
    world.json                                # metadata: display name, times, kind, counters
    live/                                     # the current state, as JSON files
      manifest.json
      run.json
      players.json
      items.json
      world-entities.json
      enemies.json
      fluids.json
      world-blocks.json
      world-transients.json
      characters/<playerKey>.json
      mod-state/                              # reserved: populated by a later stage
    backups/
      layer-end-<yyyyMMdd-HHmmss>.cuoz        # ZIP archive: the same file set, one snapshot
      mid-run-<yyyyMMdd-HHmmss>.cuoz
      auto-<yyyyMMdd-HHmmss>.cuoz
```

- `live/` stays **unpacked** so a save is a fast file write, and a human can inspect or repair a
  world without unpacking anything.
- Every backup is a **single ZIP archive** (`.cuoz`) containing the same file set with directory
  entries preserved. A new domain adds a file inside the archive; it never rewrites one blob.
- `worldId` is immutable and is the directory key: `w-<yyyyMMdd>-<4 hex>`, generated once at
  world creation. `displayName` is player-facing and freely renameable; renames never move or
  rename a directory.
- `<playerKey>` is the player's **transport-scoped identity**, not a raw account id:
  - Steam transport: `steam-<steamId64>`.
  - IP-direct transport: `name-<sanitized display name>` — the mode has no account identity, so
    the display name is the only claim available.
  The two modes are distinct key spaces; a world written over Steam is never silently claimed by
  an IP-direct name collision, because the prefix differs.

## 3. File contracts

### 3.1 `index.json`

`{ schemaVersion, worlds: [{ worldId, displayName, lastSavedUtc, kind, layerIndex, playerCount }], lastOpenedWorldId }`

- Written after every successful save and after a rename.
- It is a **cache for the picker**, never the source of truth: a world folder that exists on disk
  but is missing from `index.json` is still listed (rebuilt from `world.json`). A world listed in
  `index.json` but missing from disk is dropped from the list with a warning.

### 3.2 `manifest.json`

The manifest is the **only hard gate** in the whole format (see §6). It carries:

- Schema identity: `schemaVersion`, `format` (constant `cuo-world-archive`).
- Provenance: `gameBuild`, `cuoBuild`, `protocolVersion`, `contentFingerprint`
  (the content-set fingerprint already used by world determinism; it is what lets a loader
  decide that a stored entry's content no longer exists).
- Cut identity: `worldId`, `displayName`, `kind` (`layer-end` | `mid-run` | `auto`),
  `runEpoch`, `globalRevision`, `layerIndex`, `biomeDepth`, `cutPhase`, `savedAtUtc`.
- Integrity: `files: [{ path, sha256, bytes }]` for every file in the snapshot, and
  `checksumPolicy` so the loader knows whether checksums are mandatory.
- Provenance of the cut: `cutPhase` names the host main-thread pump seam the cut was taken at —
  `layer-boundary` (the kernel committed a layer advance) or `frame-end` (the CUO pump's last step;
  see §4) — and `saveReason` records what triggered it (`layer-advance`, `menu-return`,
  `command`, `auto-interval`, `pre-restore-backup`). The two are independent on purpose: the phase
  says WHICH seam, the reason says WHO asked.

### 3.3 `world.json`

Player-facing metadata that must survive without reading the whole snapshot:
`worldId`, `displayName`, `createdAtUtc`, `lastSavedUtc`, `lastKind`, `layerIndex`,
`playerCount`, `runEpoch`, `saveCount`, `backupCount`.

### 3.4 Domain files

`run.json`, `players.json`, `items.json`, `world-entities.json`, `enemies.json`,
`fluids.json`, `world-blocks.json`, `world-transients.json` — one file per domain table. The DTOs
are Runtime types mirroring the typed kernel checkpoint (the wire DTOs a late-joining guest
receives, so a restored host holds exactly what a join would have given it) and are mapped in
Runtime; the `CasualtiesUnknownOnline.GameState` project stays dependency-free and never learns
the format.

**Every payload file's root is a JSON array of entries**, and the decoder is handed ONE entry at a
time: that is the seam §6's per-entry salvage runs on, so a single unmaterializable row — an item
definition or prefab a mod update removed, an unmappable id — is skipped by itself. A file whose
root is not an array is a whole-file defect, never guessed at. A single-record table (a character
file) is therefore an array of one entry; `run.json` writes one row per shape it carries (below).

**A table whose facts have more than one shape writes typed rows.** `world-entities.json` rows
carry `kind` (`trap-consumption` | `building-health` | `opened-entity` | `trap-state`) and the row's
own payload; `enemies.json` rows carry `kind` (`enemy` | `removed`), where `removed` is the
terminal tombstone that stops a killed enemy from being resurrected. A single-shape table writes
its row type directly.

`run.json` carries two kinds: **`run`** (the kernel baseline) and **`native-run-fields`** (the run
values no CUO domain owns — the run clock base and the recipe table's unlock state), and the native
row is the shape §6's "a dropped field must be named" rule reads: a snapshot without it is restored
with the live clock and recipe state, and the report says so. The kernel row's
`lootRarityMultiplier` / `trapRarityMultiplier` are the world-generation inputs the game accumulates
once per layer (`WorldGeneration.cs:1061-1062`, `:4174-4177`); a cut STAMPS the cut instant's values
onto that row, because a mid-run cut is taken inside the layer the row describes, and the row is what
a peer that later generates the layer is handed. They are part of the baseline, not decoration: a
guest that generated with the game's fresh `1f` would build a different layer than the host's
(S3.4). `run.json` is still ONE file whose root is one entry array — the two rows are typed rows of
the same file, exactly like `world-blocks.json`'s `block-state` and `native-block-damage`.

`world-blocks.json` is the in-layer block diff, and its rows carry `kind` (`block-state` |
`native-block-damage`) with the row's wire payload: `blockState` is `{x, y, block}` — a block whose
id differs from the generated baseline, mined, destroyed, built or reverted — and
`nativeBlockDamage` is `{x, y, damage}` — the accumulated damage of a block that has NOT broken yet.
The cell is the identity of every fact, so no offset or instance id is stored.
`world-transients.json` carries the transient set the cut captured, keyed by `kind`: `keypad`
carries `{position, code}` and `geyser` carries `{position, liquidType}` — both DECIDED values that
must be carried and never re-rolled — and `radiation-line` carries `{active, timeGone}`. The
payloads are the same wire DTOs the late-joiner snapshot already sends, so a restore hands the
existing appliers their own shape instead of introducing a second validation path. A layer-end cut
records no in-layer fact at all — the layer it names is regenerated from the run baseline — so both
files are empty arrays for one, and the encoder writes that empty form regardless of what a caller
gathered. The same rule covers `items.json` and `world-entities.json`: a WORLD-ROOTED item row (a
ground item, and everything inside a container that lies on the ground) and every per-entity row (a
consumed trap, a trap-state fact, an opened lockable, a building-health record) describes the layer
being replaced, so the encoder drops them for a layer-end cut, names the count in the log instead of
trimming a caller's rows silently, and keeps what does cross the boundary — for items, the carried
records (and their contents) and the terminal tombstones, because those do describe the world the
restore builds. The item rule uses the very predicate the layer-boundary reset uses
(`ItemLocationChain.IsWorldRooted`), and the world-entity rule is the boundary reset's own set: the
two can never disagree about what "in the layer" means, and a later guest join cannot be handed a
kernel checkpoint whose per-entity facts describe a layer the world no longer is. A produced
`layer-end` archive therefore holds only boundary-crossing item records and no in-layer fact at all,
which is exactly what a restore of one can give back. The enemy and fluid tables are deliberately NOT
part of that rule: a layer boundary resets their LIVE rows (§3.4), and a live enemy row and a fluid
chunk both describe the layer being replaced, so a `layer-end` restore drops them the same way it drops
the world-rooted item rows and the per-entity facts (the enemy TOMBSTONES are terminal facts and stay —
a killed enemy never comes back). A `layer-end` restore therefore also runs the two host-local kernel
resets; the mechanism and its red/green pair for the live path are in
`docs/backlog/todo/save-mid-run-consistent-cut.md`, and decision 174 records the rule.

The game's own `WorldGeneration.world.blockDamages` list is the ONLY partial-damage table there is:
CUO keeps no registry beside it, and the partial block damage has no Runtime half in the fact port
either. Its rows carry their own kind — `native-block-damage` — because only the Game Adapter can
read and write that list, and the bound it obeys is the game's own: 128 entries with the game's own
oldest-first eviction (`WorldGeneration.cs:732-737`). The late-joiner snapshot is read from the same
list at send time, so a member's set fits its own identically-bounded list by construction — the two
bounded sets that used to disagree about which cells they held are gone, because there is only one
set. Reading the native tables is all-or-nothing and can FAIL: they exist only while a generated
world object does, so a reader that met no world reports that instead of an empty list, and the cut
is REFUSED rather than writing a snapshot whose damage table would read back as "no cracks"
(`NativeWorldFactCapture`). A build that predates this writes a `block-damage` kind for the CUO
registry that no longer exists: the reader treats it as an unknown kind and SKIPS it by name (§6),
while the same archive's `native-block-damage` rows still restore. An older archive therefore
degrades in a named way rather than being refused, which is why the manifest `schemaVersion` did not
move.

`characters/<playerKey>.json` holds one player character per file (an entry array of one), in the
native `SaveInfo` shape plus CUO extensions. It is the ONLY persistent copy of a character: the
pre-archive reconnect store (`CasualtiesUnknownOnline.character-data.bin`,
`Session/CharacterData/CharacterDataFileStore`) is retired (decision 178), and the in-memory
`CharacterDataStore` is filled by the live 1 Hz reports and by a restore's claim, never reloaded from
disk. A save writes one file per member PRESENT at the cut; a stored key nobody claims at
restore time means that player is absent from the session and joins as a new character with fresh
starting supplies (decision 162) — the file stays in the archive for a later claim. A key TWO present
peers claim is a different fact and is REFUSED, not handed to one of them (decision 177): IP-direct
keys a character by display name and that mode deliberately allows duplicate names, so the claim
verdict is three-valued and a refusal is named in the restore's account. The character's
`position` is a claim about the layer the cut NAMES, and the cut kind decides whether that claim is
real: a `layer-end` cut names the layer being entered, which the restore REGENERATES, so a position
captured while the body still stood in the layer being left is DROPPED at that restore (the native
save carries no position at all and the game places the body itself); a mid-run cut names the layer
its bodies stood in, so its position is the one to restore. Restoring the LOCAL player's own file is
the adapter's job and not the Runtime's — it is handed back with the continue outcome and queued on
the same local restore path a next-level respawn uses (decision 170).

The character file also carries the **native character fields** the game's own save kept for one
character (`CharacterDataMsg.NativeFields`, decision 171): the body's happiness history
(`Body.lastHappiness`), the per-run calorie counter (`PlayerCamera.caloriesConsumed`) and the wound
window's `cInfo` (the height/age/id/version `WoundView.SetCharDetails` writes). They are read off the
live scene at the same instant as the rest of the snapshot — the host's own at the cut, a guest's at
its 1 Hz report — and the read is all-or-nothing: it spans three objects, so a read that met a body
but no camera describes no coherent instant and captures nothing rather than zeros. A snapshot the
restore cannot fully put back is NAMED, never silently defaulted: no sub-message at all, a happiness
history that is not the game's own ten-value window (the game averages all ten slots and reads slot 9
for its last-chance evaluation, so a prefix write is a broken history), or a details array that is not
the four `SetCharDetails` takes. That floor exists because the game skips its own fresh
character-details roll when a run is continued (`PlayerCamera.cs:726-729`) — the alternative is a
continued character that quietly shows 0 cm / 0 y / #0 and a zeroed calorie counter.

`mod-state/` is reserved and empty until its own stage.

JSON is written with `System.Text.Json` (Runtime-owned package), UTF-8 without BOM, indented,
keys in a stable order, floats round-trip safe. Enum-valued fields (an item location kind, a trap
phase) are written as numbers; the format's own enumerations (cut kind, checksum policy) use their
documented spellings. Compression is ZIP (deflate); no extra runtime package is needed because
`System.IO.Compression` ships with net48.

## 4. Cut phases

A save is a **consistent cut**: it must not interleave with a command batch or a frame flush. The
capture seam runs on the host main-thread pump and reads one frozen revision; `cutPhase` in the
manifest records which seam the cut was taken in so a restore can prove what it holds.

There are exactly two seams:

- `layer-boundary` — taken when the kernel COMMITS a layer advance (the host's generation
  boundary). The cut therefore holds the run baseline of the layer being entered: the layer index
  and the generation random state a restore must replay to regenerate the same layer. Taking it
  before the commit would store the previous layer's baseline and regenerate a different world. A
  layer-end cut records no IN-LAYER fact (§3.4), so no in-flight state can be lost by one and the
  transient policy below does not apply to it — but it DOES carry the native run fields, because the
  run clock and the recipe unlocks outlive the layer. Its native reads are narrowly scoped for that
  reason: it takes the run fields only and never touches the keypad table, whose capture ROLLS the
  codes the game has not decided yet.
- `frame-end` — the CUO pump's LAST step (`GameAdapter.Update`, after every domain update and
  after the frame's drop/break flushes): the one point where no CUO command batch is mid-commit and
  no CUO frame flush is mid-send. What makes the cut a consistent one is not Unity's frame boundary —
  the game's own scripts may run before or after this pump — but that the whole cut is ONE
  synchronous read taken between those seams, so it can never straddle half of a batch or half of a
  flush. Both remaining triggers ARM a cut and are taken here, never inside the callback that asked:
  - the `/save` console command (a command runs inside the game's input handling, where a cut could
    read a half-applied frame), and
  - the deliberate return to the main menu by whoever OWNS the world — a host or a solo player (solo
    carries no session role, so it is its own save authority; a guest returns without a cut) — a full
    mid-run cut, because every world object is still alive at that moment. The leave happens AFTER the
    cut; leaving first would destroy the world the cut has to read. If the cut cannot be written (no
    run baseline, an unreadable native table, a failed transaction) the leave still happens, the
    previous snapshot stays intact, and the reason is logged and shown — never a snapshot whose
    tables read as empty. A TUTORIAL entry owns no archive at all (`TryBeginRun(isTutorial)` releases
    the previous run's identity instead of creating a folder): the game generates it with
    `biomeOverride == Tutorial` and disables its own save surface there, and an archive is what the
    Continue entry opens.

**The cut waits for its in-flight state.** Some live operations span frames and only reach the
kernel through the very flush they are waiting for: a local break holds its report one frame for the
drops' `Item.Start`, a destructive trap holds its event two frames for the death branch's drops, a
drop holds its report one frame for the throw velocity. Capturing those states is impossible — their
items do not exist yet — and dropping them would lose items, so an armed `frame-end` cut is
DEFERRED: the request stays armed and the seam retries, bounded by
`WorldSaveService.MaxCutDeferralFrames` (eight pump frames). A state that outlasts the deadline (a
stuck pending record) is NAMED in the cut report instead of starving the request.

Every other in-flight class has an explicit row in `WorldTransientPolicy`: `capture` (the world fact
the cut carries as data — the decided keypad/geyser/radiation values), `resolve-before-save` (the
three frame windows above), or `drop-with-log` (the state's world effect is already a kernel fact,
or the restored world re-derives it). Each row also declares whether CUO can COUNT it at the cut:
an `Observed` row is named with its count, while a `Standing` row (the game's crafting coroutine,
item velocity, the run clock, the earthquake timers) has no CUO counter, so every cut names the
CLASS without claiming a count — saying nothing about a class that a restore cannot carry would be
the silent loss this table exists to prevent. A class the cut does not carry is named in the report
(§6); an owner that reports an undeclared class REFUSES the cut, because a snapshot whose in-flight
state is unaccounted for is exactly what §6 forbids.

The host's **Continue entry** (the native `PreRunScript.LoadRun`, decision 165) opens the world
`index.json`'s `lastOpenedWorldId` names, and the newest world when that pointer is missing or
names a folder that is gone. There is no picker yet; choosing among worlds is the management
surface a later stage owns.

## 5. Write transactions

Every save is a transaction; a crash can only leave the previous snapshot intact.

```text
stage:   write <worldId>/.staging/  (manifest last, after all payload files)
verify:  re-read every file and compare against manifest checksums
backup:  ZIP .staging/ → <worldId>/backups/<kind>-<stamp>.cuoz.tmp → rename to .cuoz
commit:  rename <worldId>/live → <worldId>/.previous/ → rename .staging → live
         then delete .previous/ and refresh index.json
```

- The staging directory is always inside the target world folder, so the final rename is a
  same-volume metadata operation.
- `live/` is replaced only after the archive is safely written, so a crash between any two steps
  leaves either the old snapshot or the new one — never a half-written world.
- A leftover `.staging/` or `.previous/` from an interrupted run is detected at load: `.staging/`
  is discarded, `.previous/` is restored, both with a warning.

## 6. Restore and repair semantics

Decision 163: restore minimizes loss, and salvage is **per entry, not per domain**.

- The **manifest is the only hard gate**. If `manifest.json` cannot be read or parsed, the archive
  is *damaged*: it is never silently loaded. The loader falls back to the newest backup archive
  whose manifest reads, and reports the fallback loudly.
- **The manifest's `kind` and the payload must agree.** A `layer-end` cut records no in-layer fact
  (§3.4), so a snapshot whose manifest names one while `world-blocks.json` or
  `world-transients.json` carries rows is self-contradictory: applying them would graft one
  layer's mutations onto the layer the restore regenerates, and dropping them quietly is exactly
  what this section forbids. Such a snapshot is REFUSED as a whole.
- A refusal is not yet backed by a backup retry: the reader's fallback runs while the MANIFEST is
  read, and a decode-level refusal happens afterwards. Until the recovery surface lands, such a
  world stays unopenable and the reason is reported with the `worldId`. No build writes that shape
  (a layer-end cut always writes the two empty arrays), so this guards a corrupted or foreign
  snapshot, not a produced one.
- If the manifest reads, the load proceeds in **repair mode**. Per domain file:
  - An unreadable domain file is skipped with a warning; the other domains still load.
  - A readable domain file is decoded **entry by entry**: an entry that cannot be materialized —
    unknown content id, prefab or template removed by a mod update, unmappable id — is skipped
    with a per-entry warning, and the remaining entries of that domain still apply.
- Repair mode never regenerates the layer, never changes `layerIndex`, and never writes over the
  player's current progress. It converges on the same world, minus the entries it names.
- Every skipped entry, every fallback and every mismatch is surfaced in-game (not only in the log):
  the count per domain, the reason, and the affected content id. Silent loss is forbidden.
- **A restore has halves in time, and the later ones report too.** The Continue click applies
  the kernel checkpoint and the Runtime fact tables; the values only a live world can take (the
  block diff, the game's own partial-damage list, the decided keypad/geyser values, the radiation
  line, the restored per-entity facts, the run clock base and the recipe unlock table) are written at
  the world-entry seam afterwards, and a mid-run cut's restored item set is reconciled a frame after
  the generation-finished edge. Every half travels back to the caller that started the restore
  through `WorldRestoreAudit`: the Runtime table's per-row apply counts AND each live-world write's
  refused counts, so a restore can never be reported as a success while the game's own bounded
  (128-entry) table refused a row. A snapshot that carries no native run-field row is named in the
  same account rather than silently continuing with a clock that restarts at zero and every recipe
  re-locked; a recipe row whose index no longer exists in the live table is refused by name and
  leaves that recipe's live state alone.
- **A dropped in-flight class is reported at the CUT.** Deciding a class `drop-with-log` means the
  player is told what the cut left behind and why (§4): the cut's report names the count and the
  class for every row a CUO owner counted, names the `Standing` classes it can never carry, and
  names a pending window that outlasted the deferral deadline. The command console renders that
  report for the cuts the player asked for, and the log keeps every trigger. A cut that cannot be
  written at all (no run baseline, an unreadable native table, a failed transaction) is reported as
  a refusal with its reason instead of silently writing nothing.
- **Load twice = same world.** Restoring an already-restored snapshot is idempotent; validation
  applies the same dedup and exactly-once rules as the live restore path.
- **A stored character goes to exactly one present claimant, or to nobody.** The claim is three-valued
  (decision 177): a key no present peer claims is decision 162's absent player (a new character, the
  file kept for a later claim, NOT damage), while a key TWO present peers claim — an IP-direct session
  keys by display name and allows duplicate names — is refused for both of them, and so is a whole
  snapshot whose key space differs from the live transport (a Steam world opened over IP-direct). Both
  refusals DROP a character a present player may have earned, so both are named in the restore's
  account — the `WorldContinueOutcome` summary the continue caller logs — rather than left to the log.
  The same rule governs the WRITE side: a key that two present players who BOTH carry a snapshot map to
  is carried by no file at all and the cut report names the players who share it, because a file under a
  shared key could later be claimed by the wrong player (a cut that wrote one anyway was refused
  outright by the writer's duplicate-path guard, naming no cause). A same-named player who reported no
  snapshot is not a sharer — that character really is the only one that exists, so it is written.

### 6.1 Where a restored cut lands in the live world

A restore does not hand the world back to the game that wrote it: the Continue click runs before
the scene loads, so the kernel checkpoint and the Runtime-owned world-fact tables come back at the
click, the game regenerates the saved layer from the restored run baseline, and the in-layer facts
are then written onto that fresh copy. The seams are fixed and different on purpose:

- **Runtime world facts** (the block diff, CUO's partial damage, the radiation line) are in the
  Runtime tables at the click — a level-end cut carries none of them, so a layer-end restore leaves
  the normal layer lifecycle alone. A cut that DID carry one marks a **pending live-world replay**;
  the adapter's world-entry hook (the host's first frame after the generation completed, the same
  seam the world-entry broadcasts use) reads and clears it.
- **Native world facts** (keypad codes, geyser liquid types, the game's own `blockDamages` rows)
  are held by the adapter from the handover until that same edge, because the entities and cells
  they belong to exist only after the generation created them.
- **Order inside the edge is load-bearing**: the generated baseline is captured first (it is the
  reference the diff is measured against), then the restored facts are written, then the world-entry
  keypad/geyser broadcasts go out — the peers must receive the restored values, not freshly rolled
  ones. The **layer-boundary reset is skipped** for that generation: the restored tables ARE the
  restored layer's facts, not a previous layer's leftovers.
- **Restored world items reconcile against the regenerated layer.** A mid-run (or autosave)
  cut's world-item set is the truth for the layer the host regenerates: the kernel restore
  puts it back and marks a pending item reconcile, which suppresses the normal generation
  publish. Without that, the objects the game just generated would be published under fresh
  ids beside the restored records — two item families at one physical spot, and a duplicate
  next to the ground copy the player's restored inventory claims. The reconcile runs ONE FRAME
  AFTER the generation-finished edge, at the same moment the normal publish would
  (`GeneratedItemAuthority`), because corpse loot spawns in `CorpseScript.Start` after the
  edge: it binds a matching local object to the restored id, materializes what the generation
  did not create, destroys the standalone leftovers the cut never described, and verifies each
  write by looking the restored id up in the live scene (a refused entry is named, never
  silently dropped). It is ONE of a mid-run restore's live-write contributions, and the report
  waits for all of them before it is raised — the world facts and the restored world-entity facts
  at the world-entry seam, then this item reconcile — because each is a separate writer with its own
  handover and its own refusal account. The count is the writers that are ACTUALLY armed when the
  click returns (`WorldRestoreApplier.LiveWorldHalves`: three with both halves armed, one when
  neither is — a layer-end cut, whose world-entity rows and item rows both describe the layer being
  replaced and are dropped before the audit begins), never the cut kind alone, so a composition
  missing one writer cannot leave the restore awaiting a report that will never come; and a restore
  reports exactly once, because the world-entry seam runs the replay again on every later generation
  and a completed restore must not be re-reported. The generation a restore drives is
  also the one generation whose layer-boundary table reset is skipped: the restored set IS that
  layer's world table, and the suppressed publish would not rebuild it.
- **Restored world-entity facts land at the same seam — for a cut that describes the layer being
  restored.** The kernel's per-entity facts (consumed traps, the durable trap-state machine, opened
  lockables, building health) have two landing moments, and ROLE decides which one: a guest restores
  a checkpoint onto the world the host already generated, so `WorldEntityKernelProjection` raises its
  flat fact lists immediately; the host/solo side restores at the Continue click, when the only world
  alive is the layer being REPLACED, so the projection HOLDS the facts
  (`IRestoredWorldEntitySource`) and `RestoredWorldFactReplay` writes them at this seam through the
  same three appliers the guest path uses — the trap facts replayed position-keyed, the opened
  lockables applied as `health = 0` plus a REMOTE death mark, and the building-health rows written
  with that same remote-death marking, so a death the saved world already rolled does not roll its
  drops a second time. Each applier counts what the live world took, so an entity the regenerated
  layer does not have (divergence) reaches the restore report instead of the log alone — and so does
  an entity that IS there and cannot carry the fact: the trap action library answers a tri-state
  verdict (`TrapActionOutcome`), and "this copy cannot represent the row" is a REFUSED row while "the
  local copy already carries the state" is not (`TrapActionVerdict`), because the second one's state
  really is in the world. Without the
  write, a host that restored a mid-run cut kept those facts in the kernel and shipped them to its
  guests while its own fresh world showed every trap untouched. A `layer-end` cut's rows describe
  the layer being replaced and are dropped before the audit begins, exactly like its world-item rows
  — and the archive does not carry them either (§3.4). The arm is released wherever the layer it
  belongs to can disappear: the replay's own refusal/throw paths, a new run
  (`WorldSaveService.TryBeginRun`), a `layer-end` restore, and the session ending
  (`WorldEntityKernelProjection`'s own subscription) — an arm that outlived its layer would make the
  next generation skip its layer-boundary reset and write a previous layer's facts into a new world.
- **Native run fields** keep their own seams, because two of them need different worlds than
  the other two. The two rarity multipliers (world-generation inputs) and the run clock base
  must be in place BEFORE `WorldGeneration.Start` derives the layer's time limit and trap budget,
  so the adapter writes them at the slot where the native `SaveSystem.TryLoadGame` used to run
  (`WorldGeneration.cs:252-262`, S3.4). The recipe unlock table (`Recipes.recipes[].hasMadeBefore` /
  `.INT`) lands at the world-entry seam with the other native layer facts instead: the game REBUILDS
  the table in `WorldGeneration.Awake` and CUO's mod-content provider appends the custom recipes on a
  later Update frame, so a write at the save slot would see a table still missing every custom
  recipe and would refuse those rows. A snapshot that carries no native run-field row is restored
  with the live clock and recipe state, and the restore report names the gap; a recipe row whose
  index the world's finished table does not have is refused by name in the same report.
- **The local player's own character** is the one restored fact only the adapter can apply, and only
  once the scene has given it a body. Every stored key a present peer claims is bound into the
  character table its reconnect path sends from, but the key the LOCAL player claims comes back with
  the continue outcome and is queued on the local restore path — the same two-frame wipe a respawn
  uses (decision 170). That queue records WHICH RUN queued the snapshot, because the cancel rule has
  to tell the two apart: a restore this client's own run queued is dropped when this client instead
  follows a start it did not restore (and a run this client starts on its own drops whatever waited),
  while a restore a PEER handed over — the host sends a reconnecting player its character before the
  `WorldJoin` that starts its follow — belongs to exactly the follow being started and is never
  touched. Without the local apply, a continued host starts the layer with no character at all:
  `WorldGeneration.WorldPlacePlayer` hands out the starting supplies only on the run's first layer
  (`WorldGeneration.cs:1891-1919`), and the live 1 Hz character snapshot writes the character-table
  slot within a second, so the archive's copy would be gone before anything read it.
- **The character's native fields** (`lastHappiness`, `caloriesConsumed`, `WoundView.cInfo`) land on
  the same local restore path, on its SECOND pass — after the body exists, after the wipe and after
  the items, which is the point the native load wrote them (`SaveSystem.cs:438-441`, decision 171).
  A value the native contract cannot hold is refused BY NAME rather than truncated or skipped: a
  happiness history that is not the game's whole ten-value window (the body's array is the game's own
  and its updater shifts that array in place), or a details array that is not the native four. A
  refused field keeps the live value, and every refusal reaches the restore report: a character whose
  snapshot carries none of the three is named as damage, and so is one whose fields are malformed —
  the report is the account, not the log alone. Only characters actually BOUND to a present peer are
  described: a stored file nobody claims restores nothing, and a damage line about it would describe
  a degradation the player never gets.

Verification boundary: the codec, the routing per row kind, the tables' caps and the replay
lifecycle are machine-verified in the Runtime suites; the parts that read the live game tables
(the keypad/geyser scans, the crack-sprite refresh) and the per-cell result of a replayed diff are
verified in-game — an adapter-level reflection host can read the real game list but cannot run
`GetBlock` (it calls a netstandard-2.1 API the test host lacks) or `Object.FindObjectsOfType`.

### 6.2 Version and build gating

- `protocolVersion` mismatch: the world is still opened in repair mode, with a loud warning that
  entities created by a newer protocol may not restore.
- `gameBuild` mismatch: repair mode with a warning.
- `schemaVersion` newer than the reader: the per-file/domain payload is skipped (unreadable file)
  rather than guessed; `schemaVersion` older than the reader needs an explicit reader-side
  mapping, never a silent assumption.
- Unknown JSON properties are preserved on rewrite when the writer has them, otherwise rejected
  explicitly — never silently dropped.

## 7. Backup policy

- Backups are written on: every save (the transaction in §5 always archives), layer transition,
  explicit player request, and the configurable interval (`auto` kind).
- Retention keeps the newest N archives (default 10), never deletes the newest archive, and never
  lets a prune failure corrupt a world.
- The interval and retention count are configuration, defaulting to an interval-based autosave
  that the host can turn off; the values are decided in S5 with the config surface.

## 8. Non-goals

- No cloud saves, no cross-machine save sharing, no anti-cheat on save contents, no host
  migration.
- No second archive format for backups — backups reuse this package format exactly.
- No wire-protocol change: this is a local disk/persistence surface.
- No coexistence with the native `save.sv` (decision 165).
