# CUO Backlog

Deferred and future work, grouped by domain. The most current, highest-priority items are the
2026-08-14 validation-feedback bugs tracked in the session todo list; the rest are longer-horizon
or lower-priority items gathered during the Claude Code → DeepSeek Harness migration.

## Current bugs (highest priority)

The seven 2026-08-14 validation-feedback items are tracked as project todos (see the `dtodo`
list). They are: unconscious drop-then-pickup view offset; F8 lobby re-create residue (#205);
reconnect spike not shown (TrapLayoutSnapshot link break); host-side guest reconnect teleport;
shuttle-door closed after reconnect; trashbag-contents regression; double position-restore apply.

## Phase 4 Mod API remaining

- Content registration, custom entities, host commands, UI, mod-state saves, dependency ordering.
- Full permission model (architecture.md §5) + semantic version comparison.

## Lobby domain

- Guest that created its own lobby then joins the host via Steam friends does not follow the
  host into a run (WorldJoin follow broken) — lobby-domain refactor.
- F8 lobby re-create residue: old lobby not destroyed before creating a new one (#205).

## World time flow

- Multiplayer time domain: the base game supports wait/fast-forward and sleep-acceleration, which
  do not fit multiplayer. Undecided: host-authoritative world time, how fast-forward/sleep degrade
  or disable on the guest, forced-sleep residual handling.

## Item / entity domain

- #89 use-event sync: `ItemComponentSyncMsg` broadcast + `RenderItemIdentity` matching to remove
  the 1 Hz use latency (design decided, not implemented).
- #87 loading-screen wait info (bottom-right, to be redesigned).
- #119 held light direction on remote clones (points at the local mouse).
- #122 GameAdapter assembler (`AdapterDomain`, no DI — collapse ~25 hand-wired fields to 1).
- #118 Steam P2P cert error (transient self-heal on idle — recorded, not investigated).
- Heater cooker meat→steak conversion (item domain).
- TutorialHandler claw double-give in the tutorial world (tutorial domain).
- Trade domain #132: implemented but acceptance deferred — pending simulation coverage.

## Character / presentation / combat

- Attack animation sync (ArmsSwing etc.).
- Block HP progressive sync (currently only the break instant is synced).
- Death-pose / limb / bleed / mining presentation-state sync.
- Configurable state-stream frequency (currently hard-coded 20 Hz).
- NPC position/state sync (host-simulated + snapshot; late-joiner full snapshot).
- Online UI (create/join room, player status, nameplates + off-screen arrows).
- Command system + permission model (host-authoritative, host can authorize guests).
- Damage events (environment damage local; player-vs-player local-compute → host arbitrate → broadcast).
- Character sound / block sound sync.
- Direct player interaction (view/take items, carry, view vitals, heal).
- Periodic keyframe self-healing (partially implemented; extend to remaining domains).

## Persistence

- Character data disk persistence (currently in-memory, lost on host exit).

## Config

- BepInEx `ConfigFile` → `IOptionsMonitor<T>` adapter (bridge `ConfigEntry.SettingChanged` to
  `OnChange`); trigger: Phase 4 Mod API or when config entries appear in bulk. Standalone JSON only
  when structured/nested/array config is needed.
