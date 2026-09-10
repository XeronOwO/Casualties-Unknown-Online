# S4 — Multiplayer restore, validation/recovery, scheduled backups

- Status: Todo (blocked on S2 and S3)
- Priority: High
- Category: Persistence / save system
- Source: Stage 4 of `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md` (the backup half is the user's Q3/Q6 answer: "可配置的定时备份")
- Related: `docs/architecture/save-archive-format.md` §6/§7, `todo/save-layer-end-save-and-restore.md` (S2), `todo/save-mid-run-consistent-cut.md` (S3), `todo/systemic-save-backup-management.md` (owns the broader backup/restore product surface)

## Scope

1. **Guest restore claim** — decision 162: identity is transport-scoped
   (`steam-<steamId64>` for Steam, `name-<sanitized display name>` for IP-direct). A guest
   present at the cut who joins the restored world claims their own character. A player absent
   from the package is a **new player**: fresh character and starting supplies, logged. A
   collision (same display name, different SteamID; or a Steam world opened over IP-direct) must
   never silently hand over another player's character.
2. **Recovery semantics** — the repair-mode reporting rules in the format doc §6 become
   player-visible: per-domain skipped counts, the affected content ids, the "fell back to backup
   X" message, and a one-time in-game report of what was lost. Deciding the surface for this is a
   user-visible design question; use an existing CUO surface (console output / notification) and
   ask before adding any new UI.
3. **Observability** — one log line per save/restore with world id, kind, cut phase, revision, and
   per-domain record counts, so a mismatch is diagnosable from the log alone (no
   "change code → deploy → reproduce → add logs" loop).
4. **Scheduled autosave and retention (configurable)** — interval autosave writing `auto-*.cuoz`
   (default off or a measured default, decided with the user), retention count N (default 10), a
   pre-restore backup (`pre-restore-backup` reason), and the config surface (BepInEx
   `ConfigFile` → `IOptionsMonitor`, per decision 25). Backup failure must never damage a world.
5. **Failure degradation** — disk full, read-only directory, pruning failure, concurrent host
   instances: each has a defined, logged, non-crashing behaviour.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | Host + 2 guests save; both guests rejoin | Each claims their own character; no cross-claim; two sessions in a row stay stable |
| 2 | A Steam world opened over IP-direct | No silent cross-mode claim; the mismatch is reported and the player joins fresh |
| 3 | A guest who was not in the package joins | Fresh character + starting supplies, logged |
| 4 | Interval autosave over several cycles | The expected number of backups, retention honoured, the newest never pruned |
| 5 | Disk full / read-only save directory | Loud failure, previous snapshot intact, session continues |
| 6 | Restore from a backup after a damaged live snapshot | Live snapshot preserved as evidence, backup promoted, action reported |

## Verification limits

The multi-client rows (1–3) cannot be machine-verified: they need the sandbox dual-client pass and
the user's acceptance run. The ticket's completion report must separate machine-verified rows from
user-verified rows, and must not claim dual-client verification before the user runs it.
