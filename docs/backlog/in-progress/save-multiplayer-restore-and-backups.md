# S4 — Multiplayer restore, validation/recovery, scheduled backups

- Status: In progress (split into S4.1–S4.4; S4.1 landed 2026-09-14)
- Priority: High
- Category: Persistence / save system
- Source: Stage 4 of `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md` (the backup half is the user's Q3/Q6 answer: "可配置的定时备份")
- Related: `docs/architecture/save-archive-format.md` §6/§7, `review/save-layer-end-save-and-restore.md` (S2), `todo/save-mid-run-consistent-cut.md` (S3), `review/save-guest-restore-claim-and-legacy-store-retirement.md` (S4.1), `todo/systemic-save-backup-management.md` (owns the broader backup/restore product surface)

## Stage split

The stage is split into four deliverable-sized increments (AGENTS.md convention 11); each lands with
its own tests, gates, deploy-hash verification and independent adversarial review before the next
begins. This file stays the roadmap and holds no implementation work of its own.

| Stage | Ticket | Scope | State |
|---|---|---|---|
| S4.1 | `review/save-guest-restore-claim-and-legacy-store-retirement.md` | Scope 1's claim rules (transport-scoped, collision-safe) + scope 6 (retire the legacy `.bin` reconnect store) | landed (review) |
| S4.2 | `review/save-restore-account-surface.md` | Scope 2 + 3: the player-visible restore account (repair/recovery reporting of §6, the S4.1 claim refusals), and one account log line per save/restore with the per-domain record counts | landed (review) |
| S4.3 | this file, scope 1 (second half) | A player the world has no character for joins as a NEW player: the run's configured starting supplies, granted once per body | open |
| S4.4 | this file, scope 4 + 5 (+ S3's scope 7) | Interval autosave, retention, the config surface, the failure-degradation matrix, and the decode-level refusal's backup-promotion recovery (acceptance row 6) | open |

## Design decisions frozen with the user

- 2026-09-14 — **New-player supplies (S4.3)**: any entry into a world where the world has no character
  for that player is supplied once with the run's `startingsupplies` setting, whether the player
  arrived through a restore or joined mid-run. The native grant is first-layer-only
  (`WorldGeneration.cs:1891`: `totalTraveled <= 0 && biomeOverride == None && debugStartDepth == 0`), so
  every later entry — and every restored world — otherwise gets nothing.
- 2026-09-14 — **Autosave default (S4.4)**: interval autosave on by default, every **10 minutes**,
  retention **10** archives (the format doc §7 already fixed "on by default, host-switchable, keep the
  newest 10").

## Scope

1. **Guest restore claim** — decision 162: identity is transport-scoped
   (`steam-<steamId64>` for Steam, `name-<sanitized display name>` for IP-direct). A guest
   present at the cut who joins the restored world claims their own character. A player absent
   from the package is a **new player**: fresh character and starting supplies, logged. A
   collision (same display name, different SteamID; or a Steam world opened over IP-direct) must
   never silently hand over another player's character.
   *S4.1 landed the claim rules; the "starting supplies" half is S4.3.*
2. **Recovery semantics** — the repair-mode reporting rules in the format doc §6 become
   player-visible: per-domain skipped counts, the affected content ids, the "fell back to backup
   X" message, and a one-time in-game report of what was lost. Deciding the surface for this is a
   user-visible design question; use an existing CUO surface (console output / notification) and
   ask before adding any new UI.
3. **Observability** — one log line per save/restore with world id, kind, cut phase, revision, and
   per-domain record counts, so a mismatch is diagnosable from the log alone (no
   "change code → deploy → reproduce → add logs" loop).
4. **Scheduled autosave and retention (configurable)** — interval autosave writing `auto-*.cuoz`
   (default 10 minutes, decided with the user), retention count N (default 10), a
   pre-restore backup (`pre-restore-backup` reason), and the config surface (BepInEx
   `ConfigFile` → `IOptionsMonitor`, per decision 25). Backup failure must never damage a world.
5. **Failure degradation** — disk full, read-only directory, pruning failure, concurrent host
   instances: each has a defined, logged, non-crashing behaviour.
6. **Retire the legacy character file** — `CasualtiesUnknownOnline.character-data.bin`
   (`Session/CharacterData/CharacterDataFileStore` + `CharacterDataFile`) was the pre-archive
   reconnect store: the host kept the last report per SteamID on disk so a reconnecting guest could
   be restored inside the same run. The world archive now holds one
   `characters/<playerKey>.json` per member PRESENT at the cut, per world — two persistent copies of
   the same facts, which is exactly the second source of truth decisions 162/164 set out to remove.
   The module is unreleased, so there is no migration burden: **deleted** in S4.1 (file, protobuf
   DTO, its composition-root parameter, its `PluginDeps`/deploy references and its tests), and the
   in-memory table (`CharacterDataStore`) is fed from the archive instead — a reconnecting guest is
   restored by the claim in Scope 1, not by a stale local cache.
   Decided by the user on 2026-09-10 ("模组尚未发布, 无需考虑破坏性更新风险").

## Acceptance

| # | Scenario | Expected | Stage |
|---|---|---|---|
| 1 | Host + 2 guests save; both guests rejoin | Each claims their own character; no cross-claim; two sessions in a row stay stable | S4.1 |
| 2 | A Steam world opened over IP-direct | No silent cross-mode claim; the mismatch is reported and the player joins fresh | S4.1 (rule) + S4.2 (report) |
| 3 | A guest who was not in the package joins | Fresh character + starting supplies, logged | S4.3 |
| 4 | Interval autosave over several cycles | The expected number of backups, retention honoured, the newest never pruned | S4.4 |
| 5 | Disk full / read-only save directory | Loud failure, previous snapshot intact, session continues | S4.4 |
| 6 | Restore from a backup after a damaged live snapshot | Live snapshot preserved as evidence, backup promoted, action reported | S4.4 |
| 7 | A guest disconnects and rejoins a restored world, with no `character-data.bin` on disk | The character comes from the world archive's `characters/<playerKey>.json`; nothing else is written for it | S4.1 |

## Known constraint for the remaining stages

`WorldSaveService.cs` is the class S4.4's interval trigger and retention policy will grow, and after
S4.2 it sits at 596 of the 600 aggregate-line limit (`SourceShapeGateTests`). S4.4 must split before
adding: the natural seam is the cut-TRIGGER family (arming, the deferral deadline, the transient
policy hand-off) or the continue entry, both of which are separable from the world identity the class
owns. Do not buy headroom by shrinking comments or moving code without a responsibility split.

## Verification limits
The multi-client rows (1–3) cannot be machine-verified: they need the sandbox dual-client pass and
the user's acceptance run. Each stage's completion report must separate machine-verified rows from
user-verified rows, and must not claim dual-client verification before the user runs it.
