# Carried-rider presenter split self-check

Cycle: 2026-09-27 (follow-on inside the carry ticket's acceptance-ready window)

Ticket: `docs/backlog/todo/carry-piggyback-rider-position-smoothing.md` — stays in `todo/`: its
deciding readings still need the user's dual-client run.

Tier: NARROWED (behaviour-preserving responsibility split; no rendered-frame change).

## 1. What the cycle did

`src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs` stood at 587 lines, 13
from the 600-line aggregate gate, and `docs/backlog/watchlist/architecture-watchlist.md` — whose rule
is that such a file "must be split ... before the next change lands in them" — had not carried it
while it carried smaller files. The file is split by responsibility:

| Owner | Lines | Owns |
|---|---|---|
| `RemotePlayerRenderer.cs` | 338 | the clone lifecycle (ensure/create/destroy/bind), the state write (`SessionStatePump.Apply`), the 1 Hz diagnostic line and its level decision |
| `CarriedRiderPresenter.cs` | 309 | the carry presentation: the local-carrier mount lifecycle, the per-frame pin of every carried rider clone to its carrier's visual anchor, the carry-role marks the sit suppression reads, and the read-only pin-drift reading with its anchor resolution |

No wire, save, authority, protocol or rendered-frame behaviour changes; neither type simulates a
remote body.

## 2. Why it is behaviour-preserving — the per-frame call order, member by member

| Frame point | Before (all in `RemotePlayerRenderer`) | After |
|---|---|---|
| carry-role marks, before the stream write | `Update` computed `IsCarriedRider`/`IsCarrier` from `_playerInteraction` and wrote them when the clone had a driver | `_carriedRider.MarkCarryRole(remote.SteamId, cloneDriver)` — same two lookups, same null guard, same point in the loop |
| drift reading, before the stream write | `MeasurePinDrift(localBody, remote.SteamId, clone, cloneDriver)` | `_carriedRider.MeasurePinDrift(localBody, remote.SteamId, clone, cloneDriver, _remoteClones)` — same body, same anchor resolution; the clone table is a parameter because the renderer owns it |
| attach pass, after every clone was placed | `ApplyRemoteCarrierAttachAll(localBody)` | `_carriedRider.AttachAll(localBody, _remoteClones)` — same iteration, same two anchor views, same store-after-pose order |
| the two re-pin entries | `RefreshLocalCarrierAttach` → `ApplyRemoteCarrierAttachAll` | `RefreshLocalCarrierAttach` keeps its name, signature and null guard and delegates; `BodyUpdatePatch` → `GameAdapterBridge.OnLocalCarrierBodyUpdated` and `Plugin.LateUpdate` → `PinCarriedPresentation` are untouched |
| 1 Hz diagnostics | inline parent-chain mount test + `CarryPresentationProbe.Describe`/`Report` + the three window resets | identical, with the mount test asked as `_carriedRider.IsMountedToLocalCarrier(clone)` |

The moved bodies are verbatim except for the `_remoteClones` → `clones` parameter, the owner in the
doc comments, and the two `RemoteBodyDriver` role-field crefs. Nothing else in the family moved: the
drift arithmetic (`CarryPresentationReading`), the anomaly decision (`CarryAnomalies`) and the
readings' rendering (`CarryPresentationProbe`) are untouched.

## 3. Verification

| # | Claim | Evidence |
|---|---|---|
| 1 | the same per-frame call order is the one compiled | the three source pins in `CarryPresentationProbePinTests` (drift read BEFORE the stream write, carry-role marks BEFORE it, store-after-pose in both views) pass against the built adapter |
| 2 | the mount surface exists on its new owner with the same shapes | `CarriedRiderMountTests` passes (reflection on the built adapter) |
| 3 | the build is clean | `dotnet build CasualtiesUnknownOnline.slnx` exit 0, 0 warnings / 0 errors (`%TEMP%/cuo-split-build.txt`) |
| 4 | the affected tests pass | focused run 45/45 — `CarriedRiderMountTests` + `CarryPresentationProbePinTests` + `CarryPresentationReadingTests` (`%TEMP%/cuo-split-focus2.txt`) |
| 5 | the repository gates pass | `CasualtiesUnknownOnline.NormativeGates.Tests` 287/287 with the delivery-checklist gate excluded while its own boxes were still open (`%TEMP%/cuo-split-gates2.txt`); that gate is run on its own before the commit |
| 6 | formatting is clean | `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`%TEMP%/cuo-split-format2.txt`) |
| 7 | the full suite passes | 4439 `CasualtiesUnknownOnline.Tests` + 287 gates, 0 failures, with build (`%TEMP%/cuo-split-full.txt`) |
| 8 | nothing else referenced the moved members | pre-change census (`grep` over `src/`, `tests/`, `docs/` for the nine member names): every source referrer is the renderer's own delegation or the two pins; the live doc pointers, the ticket and the watchlist were re-pointed in the same change |
| 9 | the extraction itself was reviewed independently | the NARROWED-tier review re-derived every moved body against `git show HEAD:…` and reproduced both numbers (§5) |

## 4. Limits

- No game run: behaviour preservation is argued by the unchanged call order plus the pins, never by a
  rendered frame. The carry ticket's deciding readings (`riderDrift`, `limbSeparation`, the
  `mounted-to-local-carrier` tag) are the same mechanism and still await the user's dual-client run —
  **nothing here claims the rider teleport is fixed or measured**.
- The third-party view, the limp-rider case and the picture stay exactly as the ticket records them.
- The split changes which type owns the carry writes, not what any write does. A behaviour difference
  would have to come from a reordered or re-worded body; §2 lists them member by member.

## 5. Review and process notes

- The independent adversarial review (fresh context, frozen tree, NARROWED tier) reported **0 blocker /
  0 major / 2 minor / 4 nit**; report: `%TEMP%/cuo-review-carried-rider-presenter-split.md` (session
  artifact, never committed). Both minors were fixed inside this change:
  (a) the split turned the mark+read block into two calls, and the existing pin only covered the
  reading — the pin class gained `CarryRoleMark_IsTakenBeforeTheStreamWrite`, so a mark moved after the
  state write can no longer stay green;
  (b) `docs/evidence/selfchecks/players/carry-rider-acceptance-readiness-selfcheck.md` — the judging
  document for the pending acceptance run — had the drift reading's owner corrected to the new one.
- Nits deliberately not actioned: the new file carries no `using Object = UnityEngine.Object;` alias
  (it has no `using System;`, so `Object.Destroy` resolves to `UnityEngine.Object`; adding that using
  later would be a compile error, not a silent defect), and the ticket's `## Landed` section keeps the
  owner names of the cycles that landed those changes — the ownership note added to
  `## Current implementation` covers the reader.
- The delivery checklist was reset after this cycle's implementation was already on disk (the previous
  cycle's boxes were still checked while its acceptance run is pending); each box carries this cycle's
  evidence, and the implementation sequence itself is §1/§2 above.
