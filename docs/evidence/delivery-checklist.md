# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [ ] Design comprehension
  `docs/design/model/item-custody.md`: the receiver's own client applies the pickup, so the row had to place the receiver
  where the host's own record keeps it, which is what `docs/design/cross-cutting/saves-and-recovery.md` already says about
  a continued run's carried rows.
- [ ] Native parity
  game's `PickUpToSlot`; the row only moves the receiver and reads what the game did (`.acceptance/give-1` note "the member
  was placed 1.5 units from the host, in the host's sight").
- [ ] Native parity probe
  `[PickupCheck]` line the row went on to satisfy, read in `.acceptance/ground-1` (refused at 46.1 units) and absent from
  `.acceptance/bs-1` (PASS).
- [ ] Entry mapping
  (`SessionDriver.BringTheMemberBesideTheHostAsync`, which reads both ends back), the block scan
  (`tools/acceptance/recipes/block-find-breakable.cs`), and the cell read (`block-read-at`); the offset between the last two
  is the defect the ticket records.
- [ ] Mechanism inventory
  of cells the scan misnamed" lists all three mechanisms with their readings.
- [ ] Whole-family audit
  `block-reconcile`, `block-break-sound` and `overlay-sound` all PASS, so no other caller depended on the misnamed row.
- [ ] Self-check table
  write; (3) the chain is teleport → host's record writes it back → pickup refuses on distance; (4) removing the write and
  using the placing entry is the cause, not the symptom, because the entry verifies both ends.
- [ ] Verification design
  17.8 s), `.acceptance/bs-1` (block-sync PASS 34.5 s), `.acceptance/wf-10` (four block-family rows PASS, two fingerprint
  rows still parked on their own readings).
- [ ] Plan approved by the user
  acceptance line without asking (`AGENTS.md` rule 10, `docs/backlog/in-progress/1.0-one-acceptance-run-covers-every-case.md`).
- [ ] Structure review done
  gained only the frame correction and its comment; the three case files gained calls to an existing helper instead of new
  mechanisms.
- [ ] Build + dotnet format + dotnet test normative gates pass
  837/837 normative gates and 6762/6762 unit tests; `dotnet format CasualtiesUnknownOnline.slnx whitespace
  --verify-no-changes` exits 0.
- [ ] Release-cycle deployment/acceptance
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
