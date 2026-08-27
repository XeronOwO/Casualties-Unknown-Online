namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Default no-op visibility gate: every pair is visible. Used by the base
/// Runtime composition root and by tests that do not host a game world; the
/// production plugin replaces it with the Game Adapter's world-backed
/// implementation.
/// </summary>
public sealed class AllowAllPlayerInteractionVisibility : IPlayerInteractionVisibility
{
	/// <inheritdoc />
	public bool HasLineOfSight(ulong observerSteamId, ulong targetSteamId) => true;
}
