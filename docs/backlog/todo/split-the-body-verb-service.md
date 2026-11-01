# Split the body-verb service

- Status: Todo — the debt it records is real and this ticket owns it. `PlayerHealResetService` is 619 aggregate lines
  (limit 600) after the kill operation joined the heal family; `docs/architecture-debt.json` names this ticket until the
  split lands.
- Priority: Medium (it blocks nothing today, but the family grows one verb at a time and each one lands in the same class)
- Category: Structure / console verb family
- Source: the `/kill` work (2026-11-02). Threading the second operation through `Execute` pushed the class over the
  aggregate ceiling; the ticket that added it recorded the split as its own next step rather than doing it mid-verb.

## Design

Read from `docs/design/mechanics/console.md` (the console verb family and its cheat gate): a verb family has ONE control
plane — the gate, the target roster, the host's judgement and the answer — and the operation is a parameter of it, not a
second protocol. That is why the kill operation rode the heal messages instead of adding four new ones. The split must
keep that property: what moves out is the per-target APPLICATION and the ANSWER BUILDING, not the plane itself.

## The split

The class currently holds five jobs: the wire events, the permission ruling, the target roster, the per-target
application (local body vs instruction) and the answer text (fallback sentence plus keyed parts).

| piece | goes to | why |
|---|---|---|
| answer building (`DescribeLocal`, `DescribeRemote`, `AddClauseParts`, keyed parts per operation) | a small `BodyVerbAnswers` type | it is the only part that differs per operation and it is pure formatting |
| per-target application (local heal / local kill / instruction to a member) | a `BodyVerbApplier` type, or one method per operation behind the existing control interface | it is the only part that touches the body seams |
| wire events, permission, roster, pending-instruction table | stay | that is the control plane, and it is what the family must keep in one place |

The target is the same behaviour with the class under the ceiling: the full suite and the gates green, and the `/kill`
and `/heal` acceptance rows still judged the same way.

## Acceptance rows

| # | Action | Expectation |
|---|---|---|
| 1 | `/heal @a` after the split | the same answers and the same bodies as before |
| 2 | `/kill @a` after the split | the same answers and the same bodies as before |
| 3 | a member's request, granted and not granted | the same local refusal and the same host answer as before |
| 4 | the recorded ceiling | `docs/architecture-debt.json` no longer lists the class |
