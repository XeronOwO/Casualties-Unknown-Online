namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The stable operation ids exposed through <see cref="IModNativeApi"/>.
/// The ids are part of the public Mod API contract: the Game Adapter registers
/// them and a mod invokes them by the same string.
/// </summary>
public static class ModNativeApiOperations
{
	/// <summary>
	/// The read-only local-player body state projection. Takes no arguments and
	/// returns <see cref="IModNativeLocalPlayerState"/> (or <c>null</c> when the
	/// local body is not available in the world). No wire/protocol change.
	/// </summary>
	public const string LocalPlayerState = "local.player.state";
}
