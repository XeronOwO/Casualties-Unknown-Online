namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The permission-gated native/game-private operation surface (Phase 4 Mod API
/// remainder). This is NOT arbitrary reflection or unrestricted game-assembly
/// access: the Game Adapter — the only layer allowed to know game-private
/// types — registers a curated set of named operations, and a mod reaches each
/// one through its own typed projection. There is no untyped way in: an
/// operation's arguments and its result are declared by the signature of the
/// method that reaches it — a framework DTO such as
/// <see cref="IModNativeLocalPlayerState"/> where the framework owns the shape,
/// <see cref="ModValue"/> (the typed data model, whose binary leaf is where
/// bytes really are the value) where the shape is the operation's own — and
/// never an <c>object</c> the caller has to downcast. Unity/game-assembly
/// objects never cross the boundary.
///
/// Invoking requires <see cref="ModPermission.AccessNativeApi"/>: nothing is
/// implicit, and every call also checks and logs the permission before acting.
/// The first slice is deliberately read-only (the local player body state);
/// write/native-mutation operations are not exposed until a concrete consumer
/// exists and its sync boundary is designed.
/// </summary>
[ApiStability(ApiStabilityLevel.Advanced)]
public interface IModNativeApi
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.AccessNativeApi"/>.
	/// Every projection also checks and logs this before acting.
	/// </summary>
	bool CanAccess { get; }

	/// <summary>
	/// True when the named operation is registered by the Game Adapter and this
	/// mod copy has <see cref="ModPermission.AccessNativeApi"/>. The availability
	/// probe of a game-build-sensitive surface: an adapter built for a different
	/// game build may register a different operation set.
	/// </summary>
	bool CanInvoke(string operation);

	/// <summary>
	/// The registered <see cref="ModNativeApiOperations.LocalPlayerState"/>
	/// operation — the typed way in. Returns false (with a framework log) when
	/// the mod lacks <see cref="ModPermission.AccessNativeApi"/> or the local
	/// body is not available in the world.
	/// </summary>
	bool TryGetLocalPlayerState(out IModNativeLocalPlayerState state);
}
