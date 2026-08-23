namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The default <see cref="IModNativeApiProvider"/> in the Runtime-only
/// composition: tests and non-game hosts do not have a Game Adapter seam wired,
/// so every native operation is refused instead of touching a missing adapter
/// implementation. The production plugin replaces this registration with the
/// Game Adapter's real provider through <c>extraRegistrations</c>.
/// </summary>
internal sealed class DisabledModNativeApiProvider : IModNativeApiProvider
{
	public bool IsRegistered(string operation) => false;

	public bool TryInvoke(string operation, object?[] arguments, out object? result)
	{
		result = null;
		return false;
	}
}
