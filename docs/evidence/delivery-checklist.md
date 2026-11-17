# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops.

- [ ] Design comprehension
      the ticket `docs/backlog/done/split-world-service-messages.md` names the seam; the design pages it cites
      (`docs/design/mechanics/world-interaction.md`, `docs/design/game/worldgen-and-layers.md`) are unchanged because no
      behaviour moves.
- [ ] Native parity
      late-joiner snapshot and the reconcile send, and `BlockStateChannel` keeps every log line's text.
- [ ] Native parity probe
      `block-sync` and `save-continue` rows on the deployed build, which drive real breaks and a real restore.
- [ ] Entry mapping
      `RemoveBlockState`, `SendBlockStateSnapshot`, `ReconcileBlockTable`, `CaptureBlockStates`, `ApplyBlockState`,
      `BlockStateReceived`, `FireBlockStateReceived`) as thin delegates, so `WorldService` and its callers are unchanged;
      the table, the cap, the reconciler and the save capture/apply surface moved to
      `src/CasualtiesUnknownOnline.Runtime/Session/World/BlockStateChannel.cs`.
- [ ] Mechanism inventory
      snapshot, drift reconcile, save capture/apply); `MaxDamagedBlocks` now lives on the channel and the service keeps an
      alias because one test asserts the pending-report table's cap against it.
- [ ] Whole-family audit
      not touched; the `WorldService` debt entry stays because that class is still 603 lines, and the ticket records that
      this is the first slice.
- [ ] Self-check table
      quotes were repointed to the new file, `dotnet format` exit 0; the service dropped from 613 to 450 lines.
- [ ] Verification design
      `save-continue` row (the restore audit's block-state row count) are the pass conditions, read from the report.
- [ ] Plan approved by the user
      the ticket's own acceptance rows are the check used.
- [ ] Structure review done
      the table's state, and the service keeps no block bookkeeping; the class docs state what moved and why.
- [ ] Build + dotnet format + dotnet test normative gates pass
      "通过: 6747"; the gate project reads "通过: 836" after the quote repoints; `dotnet format --no-restore` exit 0.

- [ ] Release-cycle deployment/acceptance: deploy and read a passing pod-team-wait with zero member errors.
- [ ] FORBIDDEN — never check this box
