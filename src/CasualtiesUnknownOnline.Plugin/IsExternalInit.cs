namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler support type for the C# `init` accessor, missing on net48 — the
/// standard polyfill that makes `init` compile (the compiler emits a reference
/// to this type; it is never invoked at runtime). The Runtime, GameState and
/// Application projects carry the same file; this copy is what lets the plugin's
/// own small value types (the Online UI's member action) be records too.
/// </summary>
internal static class IsExternalInit
{
}
