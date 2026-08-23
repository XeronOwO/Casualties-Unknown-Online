namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The Runtime → Game Adapter boundary for the mod native-API surface. The
/// Runtime owns permission/policy gating and the safe value surface; the Game
/// Adapter owns the actual named operations (it is the only layer allowed to
/// know game-private types). This seam deliberately returns <see cref="object"/>
/// so the operation set can grow, but the Runtime refuses any value outside the
/// policy's safe surface before it reaches a mod.
/// </summary>
public interface IModNativeApiProvider
{
	/// <summary>True when the Game Adapter has a registered implementation for this operation id.</summary>
	bool IsRegistered(string operation);

	/// <summary>
	/// Invoke a registered native operation. The caller (ModService) is
	/// responsible for permission, operation-shape and value-safety gating.
	/// </summary>
	bool TryInvoke(string operation, object?[] arguments, out object? result);
}
