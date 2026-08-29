namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>A cross-player carry relation was cleared in the kernel.</summary>
public sealed record PlayerCarryClearedEvent(
	ulong CarrierSteamId,
	ulong CarriedSteamId) : PlayerEvent;
