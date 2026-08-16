# Openable keypad prefab mapping — mechanism inventory and self-check

Owner cycle: backlog item-domain TODO "Openable keypad prefab mapping
(entity-features table follow-up)". Decision: close it with a full
serialized-asset sweep instead of waiting for the next runtime component
sweep — the game's shipped `resources.assets` already contains every
serialized `Openable` component, and the sweep is repeatable evidence with
field-level decoding, not a runtime-only guess. No protocol change, no code
change.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `Openable` is a runtime prefab configuration | `reversing/.../Openable.cs:8-32`: `OnUse` branches on `instantOpen` → open directly, `isKeypad` → `KeypadMinigame` (lazy-generating `code`), otherwise `LockpingMinigame` with `lockpickAnglePrecision * runSetting`. The three public fields are prefab data; no code assigns them |
| 2 | Keypad is an `Openable` path only | `KeypadMinigame` is constructed only in `Openable.OnUse` (`Openable.cs:21`); no other decompiled call site exists. So the keypad prefab set equals the set of serialized `Openable` components with `isKeypad = true` |
| 3 | Serialized component identity | The game stores every `Openable` MonoBehaviour in `resources.assets`; its `m_Script` points at MonoScript path id 1276 (`globalgamemanagers.assets`, `Openable`, Assembly-CSharp). The sweep walked every asset file under `<game-dir>/CasualtiesUnknown_Data`: 2688 MonoBehaviours scanned, exactly 17 with that script id, all in `resources.assets` |
| 4 | Field-level decode | The MonoBehaviour head is 32 bytes (`m_GameObject` PPtr 12 + `m_Enabled` UInt8 aligned to 4 + `m_Script` PPtr 12 + `m_Name` aligned string 4). The three serialized `Openable` fields follow at raw offsets 32 (`instantOpen`, bool), 36 (`isKeypad`, bool) and 40 (`lockpickAnglePrecision`, little-endian float32); `code` is private and not serialized (`Openable.cs:40`) |
| 5 | Decode sanity, instant-open family | `foodbox` root (and the nested `foodbox` inside `BioContainer`) decodes `instantOpen = true`; the locale documents food boxes as "no lockpick, click to open" (`v1.8.3.json:661-662`) |
| 6 | Decode sanity, keypad family | `dropcapsule` root and the two nested `dropcapsule` children inside `Structures/BrickLoot` decode `isKeypad = true`; the locale documents the drop-capsule keypad easter-egg code 2296 (`v1.8.3.json:651-652`), which is the `KeypadMinigame` special match (`KeypadMinigame.cs:76`) |
| 7 | Decode sanity, lockpick family | `containercrate` decodes precision 0.5, `medcrate` 1.25 and `lifepodchest` 4.0 — the same values the lockpick screen displays as 1.0 / 2.5 / 8.0 degrees (`LockpingMinigame.cs:Start` doubles `anglePrecision` for the locale text) |
| 8 | Prefab roots | Each `Openable` GameObject's Transform parent chain resolves to one of eleven root prefabs: the five standalone crate prefabs (`containercrate`, `dropcapsule`, `foodbox`, `lifepodchest`, `medcrate`) and six world structures that nest copies (`BioContainer`, `Structures/BrickLoot`, `Structures/CratePod`, `Structures/LongCorridor`, `Structures/MedicalBuilding`, `Structures/MiniPod`) |

## 2. Whole-family audit

The item was a table-note gap, not a sync-family gap; the audit is the whole
family of serialized `Openable` instances. Every instance is accounted for in
the table below (17 = 17, no unresolved or unverified component).

| Root prefab | Nested GameObjects | Count | keypad | instantOpen | lockpick precision |
|---|---|---|---|---|---|
| `dropcapsule` | `dropcapsule` | 1 | 1 | 0 | 0 |
| `Structures/BrickLoot` | `dropcapsule`, `dropcapsule (1)`, `medcrate` | 3 | 2 | 0 | 0 / 1.25 |
| `foodbox` | `foodbox` | 1 | 0 | 1 | 0 |
| `BioContainer` | `foodbox`, `containercrate`, `medcrate`, `medcrate (1)` | 4 | 0 | 1 | 0 / 0.5 / 1.25 |
| `containercrate` | `containercrate` | 1 | 0 | 0 | 0.5 |
| `Structures/LongCorridor` | `containercrate` | 1 | 0 | 0 | 0.5 |
| `Structures/MiniPod` | `containercrate`, `medcrate` | 2 | 0 | 0 | 0.5 / 1.25 |
| `medcrate` | `medcrate` | 1 | 0 | 0 | 1.25 |
| `Structures/CratePod` | `medcrate` | 1 | 0 | 0 | 1.25 |
| `Structures/MedicalBuilding` | `medcrate` | 1 | 0 | 0 | 1.25 |
| `lifepodchest` | `lifepodchest` | 1 | 0 | 0 | 4.0 |

Conclusion: **the only keypad Openable prefabs are `dropcapsule` and
`Structures/BrickLoot` (the latter via its two nested `dropcapsule` props).**
Every other `Openable` is lockpick or instant-open; none were left
unclassified.

## 3. Self-check table (mechanism x change x evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Backlog note | Open item rewritten as RESOLVED with the prefab set and the scan result | `docs/backlog.md` item-domain section |
| Entity-features narrative | Buildings section gains the prefab-configuration note; matrix `sync`/`path` cells stay `covered` / `BuildingEntityOpened` | `docs/entity-features.md`; `EntityFeaturesDocConsistencyTests` passes unchanged |
| No protocol / code change | No message, patch, registry or wire format touched | ProtocolVersion unchanged; no `src/` file in the commit |
| Scan completeness | Every MonoBehaviour in every serialized asset under the game data folder was read (2688), and the only Openable script id was resolved once (MonoScript 1276) | Sweep output (recorded in this document's evidence run); 17/17 instances decoded |
| Field decode correctness | Raw offsets 32/36/40 verified against known game semantics: foodbox direct-open, dropcapsule code-2296 keypad, lockpick precision values shown by the lockpick screen | `Openable.cs`, `KeypadMinigame.cs:76`, `LockpingMinigame.cs`, locale `v1.8.3.json` |
| Prefab-root correctness | Transform parent chain resolved for all 17 components | resources.assets GameObject/Transform head reads; roots match the `Resources.Load` sites in `WorldGeneration.cs:1951-1952, 2244-2245` |
| Game-update risk | A future game update can invalidate the mapping; the backlog item now documents the scan method (script-id + offset decode) so the next sweep re-runs rather than guesses | This document §1 and §4 |

## 4. Verification design (development-period, no manual acceptance)

- Repeatable asset sweep: load `globalgamemanagers.assets` once for the
  `Openable` MonoScript path id, then read every MonoBehaviour in every
  serialized asset under `<game-dir>/CasualtiesUnknown_Data`; decode the
  three fields at raw offsets 32/36/40 and resolve each GameObject to its
  root prefab through the Transform parent chain.
- Cross-evidence: the decoded field values must reproduce the known native
  behavior (foodbox direct-open, dropcapsule keypad 2296, the three lockpick
  precision texts) — the parser cannot silently accept an implausible
  keypad/lockpick split.
- Regression gate: `dotnet test` still runs `EntityFeaturesDocConsistencyTests`
  for the entity-features narrative/matrix pair after the note is added.
- Runtime verification box for this development-period cycle: **static asset
  evidence + L0 doc consistency, no manual acceptance** (user rule
  2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `docs/backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-16)

Development-period rule applied: **no manual acceptance** — the runtime
verification box is checked on static asset evidence plus the L0 doc
consistency gate (user rule 2026-08-16).

| Evidence | Result |
|---|---|
| Asset sweep | 2688 MonoBehaviours scanned; 17 `Openable` instances, all in `resources.assets`; only `dropcapsule` (root + 2 nested in `Structures/BrickLoot`) has `isKeypad = true` |
| Decode sanity | foodbox instant-open, dropcapsule keypad, lockpick precisions 0.5 / 1.25 / 4.0 |
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 899 passed / 0 failed |
| Post-deploy asset sweep | 2688 MonoBehaviours, 17 `Openable`, all in `resources.assets` |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| Deployment | docs-only cycle — `tools/deploy.ps1` was run against the real game dir only to keep the deployed build identical |
| Structure review | no `src/` or `tests/` code touched; no class-size or state-bool impact |

## 7. Structure review

- Touched artifacts: `docs/openable-keypad-prefabs-selfcheck.md` (new),
  `docs/entity-features.md` (narrative note), `docs/backlog.md` (write-back).
- No C# class was touched, so the 600-line gate, state-bool rule and
  dead-mechanism rule are trivially satisfied.
- Dead mechanisms: none — the existing `Openable` sync row stays
  `covered` / `BuildingEntityOpened`; the keypad mapping is documentation,
  not a new parallel mechanism.
