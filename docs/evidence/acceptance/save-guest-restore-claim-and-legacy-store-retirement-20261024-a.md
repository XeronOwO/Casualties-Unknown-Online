# Acceptance — S4.1's IP-direct claim rows (1–3)

- Ticket: `docs/backlog/done/save-guest-restore-claim-and-legacy-store-retirement.md`
- Artifact: `0.1.0+7b02e9d3`
- Batches: `.acceptance/20261024-c/` (local, gitignored; runs `s42b`, `s43`, `s44`)
- Date: 2026-10-24
- Result: **rows 1–3 pass** — the three IP-direct scenarios the 2026-10-01 run left unproven. Rows 4–9
  passed in `save-guest-restore-claim-and-legacy-store-retirement-20261001-o.md` and are not re-run here.

## Row 1 — an IP-direct world restored while two present players share a display name

Staged as in `save-restore-account-surface-20261024-a.md`: an IP-direct cut from a run with ONE `Bob`
(the snapshot holds `name-bob`), then a restore with BOTH members named `Bob` present.

- The host's console history names the refusal for both claimants:
  `world w-20261023-be5c restored with damage: the stored character name-bob is claimed by more than one player present (duplicate display name in an IP-direct session), so it was handed to none of them`.
- Neither guest received a stored character: both entered the restored world as new players — each guest's
  log carries `World join received — starting a run to follow.` and
  `Remote entered the world — clone rebuilt on rejoin.`
- The restore itself completed (248 restored items; the world's facts taken).

## Row 2 — a Steam session opening an IP-direct world

The host launched with the Steam transport and called the game's own Continue (`PreRunScript.LoadRun`) on the
IP-direct save `w-20261023-26d0`. The console history reads:

```
CUO restored world w-20261023-26d0 with 1 damaged item(s); the console history names them.
world w-20261023-26d0 restored with damage: the snapshot was written in the IpDirect key space and this session runs Steam, so none of its 1 stored character(s) was claimed (decision 162)
```

No stored character was claimed, and the cross-space mismatch is a NAMED loss in the itemized account —
not a silent info line.

## Row 3 — a cut taken while two present players share a display name

A run started with both members named `Bob` present; the host took `/save`. The cut was WRITTEN (not
refused), and the report names both claimants:

```
cut written (world w-20261023-26d0 at revision 861, layer 0 (9 file(s), …)); NOT carried: … the 2 players
sharing the character key name-bob (7697845804531505658, 13430324927451837844) are carried by no file: the
archive holds one character per key
```

The log's warning repeats the reason (`Character key name-bob is shared by 2 present players …; no character
is written for it`), and the archive confirms it: `live/characters/` holds only `name-xeron.json` (the host's
own key) — no file for the shared key.

## Substitutions and limits

- The display names were set through the Online UI's own `ip.display_name` field; the cut and the Continue
  ran the production entry points (`/save` → the pump's cut; `PreRunScript.LoadRun`).
- Row 1's "both join as new players" is judged from the refusal plus both guests' world-join lines; the
  individual starting-supply grant is not read separately (decision 180's supplies rule is its own ticket).
- No OS-level mouse or keyboard input was used.
