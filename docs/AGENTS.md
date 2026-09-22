# docs/ — agent rules for this subtree

Agent entry for `docs/`; [README.md](README.md) is the human entry, and
[i18n/README.md](i18n/README.md) owns the layer and pairing contract.

- An index declares its layer: `AGENTS.md` for agents (English only), `README.md` for people,
  `README.md` + `README.zh.md` for the paired guide levels.
- Only `guide/**` and `developer/**` are paired (`foo.md` + `foo.zh.md` + `foo.i18n.yaml`); a
  `.zh.md` or `.i18n.yaml` elsewhere in the repository's file set fails the gate.
- Editing one side means patching the counterpart minimally and re-recording both blob hashes with
  `git hash-object`; renderings come from [i18n/terminology.md](i18n/terminology.md).
