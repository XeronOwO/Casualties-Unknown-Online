# Contracts

[Documentation](../README.md) > Contracts

---

Machine baselines and tables that code and gates read, in `docs/contracts/`. They are not
documentation: a page explains a rule, a file here is what a gate or a tool compares the tree against.

- [`abstractions-api-baseline.txt`](abstractions-api-baseline.txt) — the reviewed `Abstractions` public surface; `ApiSurfaceGateTests` re-derives it from the source, so an addition, a change and a removal without a tombstone all fail until the line is reviewed. Policy: [the modification policy](../en/reference/modification-policy.md).
- [`item-features-matrix.csv`](item-features-matrix.csv) — one row per item, one column per state-carrying feature; read and written through `tools/item-features.ps1`, never by hand.
- [`entity-features-matrix.csv`](entity-features-matrix.csv) — one row per world entity with its sync verdict and its covering path; `EntityFeaturesDocConsistencyTests` keeps the narrative table aligned with it, and `tools/entity-features.ps1` maintains it.
- [`event-replay-matrix.csv`](event-replay-matrix.csv) — one row per entity event with the sound, visual and state consumption each side owes; `RepositoryGateTests.EventReplayMatrix_Completeness` and `ReplayMatrixDataTests` are the gates, and `docs/evidence/sync-coverage-matrix.md` is the narrative over it.

The narrative pages over these tables are [Feature matrices](../en/reference/feature-matrices.md) and
[The mod API contract](../en/reference/mod-api.md), with their counterparts under
`docs/zh/reference/`. The entity tables of the feature-matrices page are the narrative
`EntityFeaturesDocConsistencyTests` cross-checks against `entity-features-matrix.csv`. The
document-system registries — the terminology list and the pair-alignment
record — stay in [`../standard/`](../standard/README.md).

---

[Documentation](../README.md) > Contracts
