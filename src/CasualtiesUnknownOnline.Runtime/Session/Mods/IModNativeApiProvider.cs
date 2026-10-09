using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The Runtime → Game Adapter boundary for the mod native-API surface. The
/// Runtime owns permission and operation-id gating; the Game Adapter owns the
/// actual operations (it is the only layer allowed to know game-private types)
/// and declares one typed entry per operation — the same shape
/// <see cref="IModItemSpawner"/> gives the mod content API. Nothing crosses this
/// seam as <see cref="object"/>: an operation's result type is part of the
/// seam, so a value the policy would have had to refuse after the fact cannot
/// be produced in the first place, and the registered operation set is what
/// <see cref="IsRegistered"/> reports. That probe and the projections are two
/// INDEPENDENT answers, so an implementation keeps them consistent: a provider
/// that registers an operation and then refuses its projection — or the reverse
/// — makes the mod-visible availability probe lie.
/// </summary>
public interface IModNativeApiProvider
{
	/// <summary>True when the Game Adapter has a registered implementation for this operation id.</summary>
	bool IsRegistered(string operation);

	/// <summary>
	/// The registered <see cref="ModNativeApiOperations.LocalPlayerState"/>
	/// operation: the local body's position, vitals and derived flags. False when
	/// there is no local body to project (a menu scene, a scene swap in flight).
	/// </summary>
	bool TryGetLocalPlayerState(out IModNativeLocalPlayerState state);
}
