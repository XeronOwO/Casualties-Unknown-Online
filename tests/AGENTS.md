# tests/ — the behavioural suite and the repository gates

- The normative gates are `CasualtiesUnknownOnline.NormativeGates.Tests`: they encode the rules in
  [`../AGENTS.md`](../AGENTS.md) and the documentation standard in [`../docs/AGENTS.md`](../docs/AGENTS.md).
- A gate's declaration must equal what it can reach: derive the scan surface from the existing source of
  truth, keep a census floor, and pin the matcher with positive and negative samples.
- Run the focused gate first (`--filter "FullyQualifiedName~<Gate>"`), the full suite before a commit.
