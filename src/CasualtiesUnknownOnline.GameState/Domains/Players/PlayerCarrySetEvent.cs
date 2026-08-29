namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>A cross-player carry relation was recorded in the kernel.</summary>
public sealed record PlayerCarrySetEvent(
	ulong CarrierSteamId,
	ulong CarriedSteamId) : PlayerEvent;
