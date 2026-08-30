# GameAdapter construction readability split — self-check

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/selfchecks/MANIFEST.md` and
> `docs/architecture-evolution/protocol.md` before citing.

Owner cycle: backlog item #122 "GameAdapter assembly (possible readability
grouping)". Decision: keep the state-belongs-to-its-owner design and the
existing direct constructor wiring (no DI collapse, no factory), but move the
adapter's owned state + constructor dependency wiring out of the coordinator
file into a dedicated `GameAdapter.Construction.cs` partial. The coordinator
file then only owns lifecycle/session wiring and the thin `IPatchBridge`
forwards.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Construction/state block | The field declarations and the <see cref="GameAdapter"/> constructor lived at the top of `GameAdapter.cs` (previous 594-line file). They are now in `GameAdapter.Construction.cs`; the constructor body is byte-for-byte unchanged except for the file/class-doc split. |
| Readonly assignment rule | All `readonly` fields are still assigned directly inside the constructor — no helper-method assignment (which C# forbids for readonly fields), no non-readonly downgrade. |
| Partial existence | `GameAdapter` was already `partial`; the new file uses the same partial type and does not restate the base interface list. |
| No behavior change | No domain construction order or ownership changed; no static seam / DI registration / patch surface / wire format touched. |
| Existing domain partials | `GameAdapter.CharacterSound.cs`, `GameAdapter.Heater.cs`, `GameAdapter.Time.cs` already own fields for their domains; the moved block does not duplicate those fields. |

## 2. Whole-family audit

- The `GameAdapter` family is one coordinator plus deliberately separated
  domain partials. This change only relocates the coordinator's state and
  construction block; it does not alter any domain partial.
- No new class, no new identity, no new state bool, no new protocol message.
- The static `PatchBridge.Bind` remains the only static seam, called from the
  same constructor location.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Coordinator cursor | `GameAdapter.cs` drops the state/constructor block (previous 594 → 472 lines) | Build; line count |
| Construction cursor | New `GameAdapter.Construction.cs` owns the adapter state + constructor (155 lines) | Build; line count; one top-level type per file |
| Readonly semantics | Unchanged direct constructor assignment | Source diff; build |
| Domain ownership | Unchanged — domains still own their state, coordinator only forwards | Source diff |
| Partial consistency | No duplicate fields (`_characterSoundSync`, `_heaterCookSync`, `_worldTimeSync` remain in their existing partials) | Build + compile |
| Runtime behavior | N/A — no wire/behavioral surface | Full suite |

## 4. Verification design (development-period, no manual acceptance)

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx --no-build` — full suite 1066 passed.
- `dotnet format CasualtiesUnknownOnline.slnx` — clean.
- `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1` — all pass.
- Deploy to the real game dir only via `tools/deploy.ps1`; no runtime dual-side
  acceptance (user rule 2026-08-16: no manual acceptance during development).

## 5. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.

## 6. Structure review

- `GameAdapter.cs` 472 lines; `GameAdapter.Construction.cs` 155 lines — all
  under the 600-line gate.
- No new expression-state bool fields; no state moved out of its owner.
- No dead mechanisms: the old block was moved, not duplicated or replaced.
