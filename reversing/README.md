# reversing/

Reverse-engineering workspace for the game's assemblies.

Decompiled game sources and analysis artifacts live here. They are
**copyrighted and large** — the whole folder except this file is gitignored,
so keep anything sensitive/derived inside and never stage it.

## What goes here

- dnSpy decompile output of game assemblies (`Assembly-CSharp.dll`, …)
- structure dumps, method-signature listings, call-graph notes
- temporary analysis scripts and experiments

## Decompiling the game assembly

With dnSpy.Console (path in `CLAUDE.local.md` on the dev machine):

```bash
dnSpy.Console.exe -o reversing/Assembly-CSharp --project references/Assembly-CSharp.dll
```

## What does NOT go here

Notes worth keeping for the project (capability findings, adapter
knowledge, version-specific quirks) belong in `docs/` and get committed —
the self-learning rule. This folder is for raw material and scratch work.
