using System;

namespace CasualtiesUnknownOnline.Runtime.Logging;

/// <summary>
/// Shared no-op scope. Structured scopes (SessionId/SteamId/ModId — architecture.md
/// §5.5) land in a later round when sessions exist to scope.
/// </summary>
internal sealed class NullScope : IDisposable
{
	public static readonly NullScope Instance = new();

	private NullScope()
	{
	}

	public void Dispose()
	{
	}
}
