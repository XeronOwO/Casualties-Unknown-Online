using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Enables C# init-only accessors on net48, where the BCL does not ship this
/// compiler-support type. This mirrors the Runtime/GameState pattern.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit
{
}
