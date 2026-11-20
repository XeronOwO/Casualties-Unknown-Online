# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [ ] Design comprehension — the save/recovery page's WorldBackupPromotion section and this ticket's A2 design row were read before the change
- [ ] Native parity — no native chain is mirrored by this change; it reorders three existing host writes and substitutes none
- [ ] Native parity probe — no substitute exists to probe; the two restore facts the reorder could break are pinned by tests
- [ ] Entry mapping — the reconnect restore has one entry and one write order
- [ ] Mechanism inventory — the family is every write the restore commits, in journal order: player facts, player row, item re-key, arbitration
- [ ] Whole-family audit — the code sweep's CharacterData block now counts 41 files and the two evidence ledgers name the moved lines
- [ ] Self-check table — the write order is asserted by the ordering test plus the 16 focused restore tests
- [ ] Verification design — focused tests first, then the full suite once, then format
- [ ] Plan approved by the user — the night-mode goal authorises self-chosen ticket work without asking
- [ ] Structure review done — the restore's writes left the store for one class with one entry, and the store dropped from 697 to 609 lines
- [ ] Build + dotnet format + dotnet test normative gates pass
- [ ] Release-cycle deployment/acceptance
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
