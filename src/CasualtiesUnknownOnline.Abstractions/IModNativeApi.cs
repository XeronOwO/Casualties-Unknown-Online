namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The permission-gated native/game-private operation surface (Phase 4 Mod API
/// remainder). This is NOT arbitrary reflection or unrestricted game-assembly
/// access: the Game Adapter — the only layer allowed to know game-private
/// types — registers a curated set of named operations, and a mod can only
/// invoke those operation ids. Arguments and results are restricted to the
/// framework-safe value surface (<c>null</c>, strings, numeric primitives,
/// <c>byte[]</c>, primitive arrays, and framework DTO types such as
/// <see cref="IModNativeLocalPlayerState"/>); Unity/game-assembly objects never
/// cross the boundary.
///
/// Invoking requires <see cref="ModPermission.AccessNativeApi"/>: nothing is
/// implicit, and every call also checks and logs the permission before acting.
/// The first slice is deliberately read-only (the local player body state);
/// write/native-mutation operations are not exposed until a concrete consumer
/// exists and its sync boundary is designed.
/// </summary>
public interface IModNativeApi
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.AccessNativeApi"/>.
	/// Every invoke method also checks and logs this before acting.
	/// </summary>
	bool CanAccess { get; }

	/// <summary>
	/// True when the named operation is registered by the Game Adapter and this
	/// mod copy has <see cref="ModPermission.AccessNativeApi"/>.
	/// </summary>
	bool CanInvoke(string operation);

	/// <summary>
	/// Try to invoke a Game Adapter–registered native operation. Returns false
	/// (with a framework log) when the mod lacks <see cref="ModPermission.AccessNativeApi"/>,
	/// the operation id is malformed, the arguments are outside the safe value
	/// surface, the provider does not know the operation, or the provider's
	/// result is outside the safe value surface.
	/// </summary>
	bool TryInvoke(string operation, object?[] arguments, out object? result);

	/// <summary>
	/// Convenience projection of the registered <see cref="ModNativeApiOperations.LocalPlayerState"/>
	/// operation. Returns false (with a framework log) when the mod lacks
	/// <see cref="ModPermission.AccessNativeApi"/> or the local body is not
	/// available in the world.
	/// </summary>
	bool TryGetLocalPlayerState(out IModNativeLocalPlayerState state);
}
