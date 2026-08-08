namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler support type for the C# `init` accessor, missing on net48 — the
/// standard polyfill that makes `init` compile (the compiler emits a reference
/// to this type; it is never invoked at runtime).
/// </summary>
internal static class IsExternalInit
{
}
