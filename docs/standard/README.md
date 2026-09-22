# standard/ — registries for the document system

Machine-readable records the documentation rules depend on. Writers, reviewers and the gates read
them; they are not a section of the documentation itself.

| File | Holds | Read by |
|---|---|---|
| `terminology.txt` | an English term → the only Chinese rendering, its first-use spelling and the forms never to use | anyone writing a Chinese page; the terminology gate |
| `alignment.txt` | each English/Chinese page pair with the hashes confirmed at the last sync | the drift check; anyone editing one side |

The rules that use these files are [`../AGENTS.md`](../AGENTS.md). Readers meet the words through
[`../en/reference/glossary.md`](../en/reference/glossary.md) and
[`../zh/reference/glossary.md`](../zh/reference/glossary.md).
