# Acceptance record — source, permission and data checks (stage S1a/S1b rows)

- Ticket: `validation-and-source-checks` — verdict: **stays in `todo/`** (rows 3 and 4 pass in full;
  rows 1–2 wait for stage S2 and row 5 was not exercised — see Limits)
- Batch: `20261014-a` — tickets `item-data-write-commands`, `validation-and-source-checks`,
  `online-ui-art-and-controls-overhaul`, `online-ui-layout-and-input-detail-pass`
- Commit: `16c0d603` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+16c0d6034766394af57fa88106a185f2e4f47fbf`
- Run: 2026-10-14 22:2x → 23:0x · Host: physical machine · Guest: `Steam1` sandbox
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `capture`, `input`, `logs`,
  `artifacts` (preflight exit 0)
- Artifacts: the `20261014-a` subdirectory of the directory named by `acceptance-artifacts-dir`

The reports this ticket is about are the ones a member's own client cannot produce. The run produced them
from the guest's own process through the product's own sender (`forge` probe: one `IKernelProtocolControl.
SendCommand` per invocation, the same call the product's senders use), and read the host's reaction from
the host's log, the host's console and the host's own kernel rows.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 3a | A member reports a kind only the host's simulation authors | machine | pass | Forged from the guest: `f1-guest-forge-hostonly.json`. The host refused it BY NAME and ANSWERED the sender — `g1-host-seam-refusals.txt` line 1: `Command admission refused (answered): RecordBuildingEntityHealthCommand actor … sender … — NotAuthorized: a host-only fact is authored by the host's own simulation, never by a member.` The sender's side read the answer: `g5-guest-answer-lines.txt` — `Kernel command rejected by host for item 0: RecordBuildingEntityHealth refused (NotAuthorized)`. The host's kernel did not move: `e6-host-row-1202911823811.json` … `e6-host-row-5197989525.json` (283 rows, global revision 710) versus `g2-host-row-after-…` — identical, every revision and condition unchanged |
| 3b | A member's command report is refused unless the host's rule grants it (the console verb half) | machine | pass | Judged in this batch as rows 4 and 4b of the write ticket: `item-data-write-commands-20261014-a.md` |
| 4a | A member reports ITEM STATE for somebody else's item | machine | pass | Forged from the guest: `f2-guest-forge-state.json` (the host's `#1202911823811`). `g1-host-seam-refusals.txt` line 2: `Command admission refused (no answer, as before): UpdateItemStateCommand … — item 1202911823811 is another member's (carried, installed or contained), so its state is not the sender's to report.` No row moved: the same three `g2-host-row-after-…` reads as the baseline (`revision 3`, `condition 1`, `Carried`, owner = the host), and the host's console listed the same two recorded items (`g3-host-data-get-after.json`). No answer reached the sender: `g5-guest-answer-lines.txt` holds exactly ONE line, the host-only refusal — this shape is dropped in silence, as its own rule says |
| 4b | A member syncs ANOTHER MEMBER'S CONTAINER (the parent half) | machine | pass | Forged from the guest: `f3-guest-forge-container.json` (parent = the host's `#1207206791107` trashbag, one child). `g1-host-seam-refusals.txt` line 3: `Command admission refused (no answer, as before): SyncContainerItemsCommand … — container 1207206791107 is another member's (carried, installed or contained), so its contents are not the sender's to rewrite.` The container's row and the child's row are the baseline reads (`g2-host-row-…`: `condition 1`, child still `Carried` by the host) |
| 4c | The container sync's CHILD half — a listed child that is another member's carried item | machine | pass | Forged from the guest with the GUEST's own container as the parent and the HOST's carried item as the child: `j3-guest-forge-child.json`. `j4-host-child-refusal.txt`: `Command admission refused (no answer, as before): SyncContainerItemsCommand … — child 1202911823811 is another member's (carried or installed), so it cannot be moved into the sender's container.` The item did not move: `j5-host-row-after-child.json` — `revision 3`, `condition 1`, `Carried`, owner = the host |
| 6 (first half) | An ordinary session with unmodified clients produces no new refusals | machine | pass (this half) | `g6-host-all-admission-lines.txt`: from world entry (the run's own mark) to the end of the session the host logged exactly FIVE admission refusals, and all five carry the timestamps of the deliberate actions above (22:28:38.863 / .988 / 22:28:39.145 for the three forged kinds, 22:29:05.344 for the closed-rule write, 22:29:41.561 for the child forgery). Nothing before them: joining, entering the world, the fixtures and the host's own console writes produced none |

## Residuals for the user

None. Every verdict above is a log line, a console line or a kernel row read by this run.

## Limits

- **Rows 1–2 are not judged and cannot be**: they are stage S2's (a destruction report's drops checked
  against the block's own table), which has not landed. The ticket keeps them.
- **Row 5 (a report arriving before its creation) was not exercised**: this session's fixtures had all
  reported before the rows ran, and the invariant lives above the admission seam rather than in it. The
  ticket's own note that it already runs above the seam is unchanged by this run.
- The forged submissions are the member's own client sending what no CUO gesture sends; the run did not
  modify the client binary, and it used only the sender the product itself uses. What a DETERMINED client
  can still do (report the same state under a kind the host admits) is the ticket's own recorded limit and
  is not contradicted here.
