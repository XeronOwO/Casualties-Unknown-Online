namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler support type for the C# `init` accessor, missing on net48 — the
/// standard polyfill that makes `init` compile (the compiler emits a reference
/// to this type; it is never invoked at runtime). Each assembly that declares a
/// record needs its own copy, and this tool deliberately references no src/
/// project, so it cannot borrow one.
/// </summary>
internal static class IsExternalInit
{
}
