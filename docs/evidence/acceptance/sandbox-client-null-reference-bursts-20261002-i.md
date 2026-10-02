# Acceptance record — Out-of-world item streams stay out of the menu (and the re-entry residual)

- Ticket: `sandbox-client-null-reference-bursts` — verdict: **back to `todo/`** (status field:
  `- Status: Todo — Rejected (batch 20261002-i …)`). The batch-`20261002-h` NRE storm and the
  251-copy rounds are gone against the deployed artifact; row 5 (re-entry convergence) failed: a
  re-entry still leaves 9 (guest) / 14 (alternate) unbound generation-time locals beside the host's
  bound set.
- Batch: `20261002-i` — tickets: `sandbox-client-null-reference-bursts`
- Commit: `b6702483771cc45929b968954533bf0d3f7e39d3` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+b6702483771cc45929b968954533bf0d3f7e39d3`; `tools/verify-deploy.ps1` exit 0
  ("Deployment matches this tree's build output"); both sandbox plugin dirs absent (no CUO shadow)
- Run: 2026-10-02, one session (host on the physical machine, guest and alternate in their sandboxes;
  three clients, one world) · Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports
  present · Entry gate: no `CasualtiesUnknown` process, `session-environment.ps1 -Mode status`
  = active=cuo, game-running=false
- Windows staged with the verified member route (`leave-world` → menu dwell across at least two
  keyframe cycles → `home.leave` → `join-lobby` → inWorld), every window read for the DEDUPED
  diagnostic FIRST, and an absence read re-read once after a bounded pause
- Artifacts: `i-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact
  ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | menu dwell: the out-of-world client receives no item rows (no `World-item snapshot received`, no `[ItemSpawn] materializing`) and the host targets the in-world member only | machine | pass | `i-w1-guest-menureads.log` / `i-w2-alt-menureads.log` = `NO MATCHING LINES` over the menu mark; `i-w1-host-itemkeyframe-menu.log` / `i-w2-host-itemkeyframe-menu.log` = `[ItemKeyframe] sent 266 item(s) to 1 in-world member(s)`; `i-w1-guest-menu-census.json` / `i-w2-alt-menu-census.json`: scene `PreGen`, `worldExists=false`, `items=0`; second census `i-w1-guest-menu-census-2.json` / `i-w2-alt-menu-census-2.json` same |
| 2 | the whole window (leave + dwell + re-entry) logs no `[BrokenItemUpdate]` and no `Item.DMD<Item::Update>` / `Unity:Exception` burst | machine | pass | `i-w1-guest-brokenitem-menu.log` + `-reread.log`, `i-w2-alt-brokenitem.log` + `-reread.log` = `NO MATCHING LINES`; `i-w1-guest-nre.log` / `i-w2-alt-nre.log` = 0 matches since the window mark |
| 3 | in-world peer control: the keyframe keeps arriving | machine | pass | `i-w1-alt-snapshot-menu.log` (2 receipts, 8.3 s apart); `i-w2-guest-snapshot-menu.log` (2 receipts, 8.4 s apart) |
| 4 | host and the third peer clean in the window | machine | pass | the non-dwelling client and the host show zero `BrokenItemUpdate` / `Item.DMD` matches (the peer reads of row 2's windows); close sizes `i-close-checks.txt` (host 1.14 MB) |
| 5 | re-entry converges: no duplicate world items | machine | **fail** | guest `i-w1-settled-guest-dupcheck.json`: 278 items (267 with id + 11 without) against `i-w1-settled-host-dupcheck.json`: 269 (266 + 3) — `dupIds=0`, but 9 id-less world locals match host-bound objects within 1.6 units (`i-w1-settled-guest-idless.json`, `i-w1-settled-guest-itemsdump.json`, `i-w1-settled-host-itemsdump.json`); the round: `i-w1-guest-bind.log` = 257 binds, `i-w1-guest-materialize.log` = 9 materializations. Alternate: `i-w2-settled-alt-dupcheck.json` 283 (267 + 16) vs `i-w2-settled-host-dupcheck.json` 269; `i-w2-alt-bind.log` = 252, `i-w2-alt-materialize.log` = 14 |
| 6 | initial entry not regressed | machine | pass | `i-entry-genitems-host.log` = `[GenItems] host published 266 ground items`; `i-entry-genitems-guest.log` / `i-entry-genitems-alt.log` = `[GenItems] applied 266 entries`; `i-w0-*-dupcheck.json` = 269 items on every side (host 266 + 3, guests 267 + 2) |
| 7 | host sender log names the target set | machine | pass | `i-w1-host-itemkeyframe.log` = `sent … to 2 in-world member(s)` before the leave report landed, then to 1 for every later cycle; `i-w2-host-itemkeyframe-menu.log` = to 1 throughout the dwell |

## What the run established

- The batch-`20261002-h` defect is fixed against the deployed artifact. Window 1's menu dwell held
  ~97 s across ~10 keyframe cycles and window 2's ~40 s across ~4; both out-of-world clients kept
  `items=0`, zero `[BrokenItemUpdate]` and zero `Item.DMD<Item::Update>` lines. The one keyframe that
  still reached the window-1 guest arrived before its leave transition was processed (the client was
  in the world then, `i-w1-guest-scene-transition.log`: receipt 12:49:37.749, `Scene state: InMenu`
  12:49:40.030) — exactly the race the receiver-side live-world gate backstops.
- The host-side targeting is observable at the sender: the keyframe went from 2 to 1 in-world
  member(s) on the first cycle after the leave report landed, and the dwelling client's absence reads
  are clean from the menu mark on.
- The generation publish is not regressed: the host published 266 ground items and both guests
  applied them at entry (`i-entry-genitems-*.log`), and the three sides sat at 269 items each at the
  baseline.
- Row 5 is the rejection: a re-entry binds most generation-time objects by position (257 guest / 252
  alternate of the host's 266) but misses 9 / 14 — each missed host entry materializes beside the
  local object, which stays id-less, so the client ends 9 / 14 objects above the host with zero
  duplicate ids. That is the residual half of the batch-`20261002-h` related finding (rows applied
  against a receiver baseline that is still producing its own objects), now at ~3–5% of the host's
  table instead of the 251-pair round batch `20261002-h` measured.

## Residuals for the user

None — every row is machine-judged.

## Limits

- One session, two windows; the keyframe cadence is adaptive (~8–10 s here), so the dwell is measured
  in the in-world peer's receipts, not on a wall-clock constant.
- The probes are point-in-time snapshots; the census totals drift by one or two objects between
  moments (the guest was 65 items mid-generation at the first re-entry probe and 278 once settled).
- The instantiate-null half did not reproduce (no `breaksNow` item, as in batch `20261002-h`); batches
  `20261002-c`/`-d` remain its only direct evidence, and the `GroundBlood.Start` frame was not staged
  again.
- The row-5 mechanism (9 / 14 bind misses at re-entry) is measured twice but not root-fixed here; the
  ticket carries it as the next work item.
- The receiver guards themselves have no automated coverage (the GameAdapter's Unity dependency keeps
  them out of the suite); a refusal logs at Debug, which this run enabled on all three clients.
