# Global adaptive report-rate flow control — Stage 3: cumulative stream coalescing

- Status: Review
- Priority: Medium
- Category: Network / adaptive sync / flow control
- Parent: `../todo/global-adaptive-report-rate-flow-control.md`

## Objective

Extend the global adaptive flow-control framework from `LatestWins` overwrite
streams to streams whose intermediate frames are cumulative or position
progress and may be coalesced/lightened without changing final committed state.
This stage lands the first two loss-tolerant medical progress streams:

1. **Injection frame-level deltas** (guest → host): the native syringe minigame
   produces many small per-frame `DeltaMl` reports. Coalesce them into fewer,
   larger, still-reliable `MedicalOperationUpdate` frames at the adaptive
   cadence; the terminal `EndRequest` total continues to reconcile the exact
   delivered amount.
2. **Shrapnel ordinary held-piece positions** (guest → host): ordinary
   non-ownership moves are absolute per-piece positions. Coalesce the latest
   position per piece at the adaptive cadence while ownership transitions
   (grab/release/break-grasp/removal) remain immediate and reliable.

No wire-protocol version change: only message rate/coalescing changes.

## Design decisions

- Medical injection update transport stays **reliable**. Each accumulated
  delta is still delivered reliably, but the number of frames is reduced by
  adaptive coalescing. This avoids cumulative-error drift when a user performs
  a very slow treatment: a lost intermediate delta could otherwise accumulate
  into a materially wrong final amount; reliable coalescing keeps the exact
  amount without per-frame chatter.
- Shrapnel ordinary positions remain **unreliable** during steady movement
  because each position is an absolute overwrite; a lost intermediate position
  is harmless and the next authoritative state covers it. The buffered final
  position before release/end/cancel is flushed **reliably**, and ownership/
  semantic transitions stay reliable.
- `AdaptiveStreamProfile` gains an optional `BaseHz` so medical progress streams
  can preserve high frame-level cadence in `Optimal` mode while still lowering
  under pressure, without repurposing the global 20 Hz player/enemy baseline.
- A shared `MedicalOperationIdAllocator` feeds injection, shrapnel and
  other-action domains so same-numbered operations from different medical
  sub-domains cannot confuse terminal routing on clients.

## Stream catalog additions

| Stream | Mode | Direction | BaseHz | Transport |
|---|---|---|---|---|
| `MedicalInjectionReport` | `Cumulative` | guest → host | 60 | reliable (coalesced deltas) |
| `ShrapnelPositionReport` | `LatestWins` | guest → host | 60 | unreliable (latest per piece) |

## Non-goals

- No anti-cheat/malicious-rate policing.
- No prediction/interpolation.
- No wire/protocol change.
- No host-migration or save changes.
- No fluid/item/trader integration (Stage 4).
- No remote-medical feature work; only the adaptive rate/coalescing mechanism.

## Verification

- Focused tests:
  - injection first delta immediate, burst deltas coalesced into one frame;
  - cancel flushes buffered injected delta before `MedicalOperationCancel`;
  - end still reconciles exact total after a buffered delta;
  - shrapnel ordinary position coalescing is per-piece and keeps the latest
    position, ownership changes bypass the buffer;
  - release/end/cancel flush the final buffered shrapnel position reliably;
  - release preserves the last authoritative position;
  - `PacketSender` classifies injection, shrapnel ordinary and other medical
    updates into separate traffic observation payload types;
  - adaptive policy now adapts `Cumulative` streams, uses `BaseHz`, and the
    catalog/wire-mapper contains all new streams.
- Full solution build: 0 warnings / 0 errors.
- Full test suite: 2462 runtime tests + 16 normative gates passed.
- `dotnet format`: clean.
- Independent adversarial self-check: three fresh-subagent passes; all found
  issues (release reset, buffered final-position loss, wire classification
  cross-pollution, per-operation overwrite, cross-domain id collision,
  terminal unreliability, disposed buffer cleanup) were addressed and
  re-reviewed.
- Deployment: `tools/deploy.ps1` deployed to the real game directory; SHA-256
  hashes of all six CUO DLLs match the build output.
- Self-check: `docs/evidence/selfchecks/protocol/global-adaptive-report-rate-stage-3-cumulative-streams-selfcheck.md`.

## Remaining before final unified acceptance

- Real dual-client acceptance remains the user's final unified acceptance pass.
