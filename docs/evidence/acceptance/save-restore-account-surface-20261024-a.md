# Acceptance — S4.2's row 2: the ambiguous character claim in an IP-direct roster

- Ticket: `docs/backlog/done/save-restore-account-surface.md`
- Artifact: `0.1.0+7b02e9d3`
- Batch: `.acceptance/20261024-c/` (local, gitignored; runs `s42` and `s42b`)
- Date: 2026-10-24
- Result: **row 2 passes** — the only row the 2026-10-01 run (`20261001-o`) left unproven.

## Scenario

Two present players sharing a transport key (decision 177): in an IP-direct session the character key
is the display name, so two members both named `Bob` share `name-bob`. The archive must hold a
character for that key (written at a cut where only one `Bob` was present), and the restore must then
refuse the claim for BOTH claimants and carry the refusal in the itemized account.

## What was run

1. **Run 1 (the cut).** Three clients launched; the host opened an IP-direct session (listen port 26600)
   and one member joined with display name `Bob`; the host started the run and took `/save` twice — the
   first cut (revision 5) was too early and wrote `0 character(s)`, the second (revision 582) wrote
   **2 characters** into the snapshot: `live/characters/name-bob.json` and `name-xeron.json` (the host's
   own key `name-xeron`). The clients then quit.
2. **Run 2 (the ambiguous restore).** All three clients relaunched; the host re-opened the IP-direct
   session on the same port; BOTH guests joined with display name `Bob` (two present players, one key);
   the host took the game's own Continue entry (`PreRunScript.LoadRun`, the driver's `continue-run`).

## Result

The console history (host) reads:

```
CUO restored world w-20261023-be5c with 1 damaged item(s); the console history names them.
world w-20261023-be5c restored with damage: the stored character name-bob is claimed by more than one player present (duplicate display name in an IP-direct session), so it was handed to none of them
the stored character name-bob is claimed by more than one player present (duplicate display name in an IP-direct session), so it was handed to none of them
CUO restore of world w-20261023-be5c: the live world took every restored fact (0 block-state, 0 partial-damage, 33 keypad, 108 geyser, 132 recipe unlock row(s)); the live world took the restored item set (248 entries bound or materialized)
```

So the refusal is BOTH in the joined summary and as its own itemized line — row 2's expected shape —
and the restore itself completed (248 restored items, the world's facts taken, one damaged entry).

## Rows

| # | Row | State |
|---|---|---|
| 1 | A clean continue | pass (previous run `20261001-o` + the suite) |
| 2 | Two present players sharing a transport key | **pass — this run** (console history above) |
| 3 | A damaged live snapshot, opened from a backup | pass (previous run) |
| 4 | A continue with no world at all | pass (suite + previous run) |
| 5 | An attempt applied and then abandoned | pass (previous run + suite) |
| 6 | A damaged restore as the player sees it | pass (previous run) |
| 7 | A skipped entry's content id | pass (suite) |
| 8 | A cut and a restore of the same snapshot | pass (previous run) |
| 9 | A cut that wrote nothing | pass (suite) |

## Substitutions and limits

- The two members' shared display name was set through the Online UI's own `ip.display_name` field; the
  cut and the continue ran the game's own entry points (`/save` → the pump's cut; `PreRunScript.LoadRun`).
- The first cut at revision 5 wrote no characters (the member's first character report had not reached the
  host yet); the run took a second cut after the world's first minute, which wrote both. This is the
  documented "a member with no snapshot simply adds no file" rule (decisions 162/166), not a defect.
- No OS-level mouse or keyboard input was used.
