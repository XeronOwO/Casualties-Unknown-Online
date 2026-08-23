using IGameAdapter = CasualtiesUnknownOnline.Runtime.GameAdapter.IGameAdapter;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The GameAdapter boundary for the trader-recruit co-op revive: the Online UI
/// asks the adapter, the adapter forwards to the Unity-facing
/// <c>TraderRecruitCoordinator</c>. The coordinator owns the host-authoritative
/// validation and the target's local revive application.
/// </summary>
public sealed partial class GameAdapter
{
	bool IGameAdapter.TryRequestTraderRecruit(ulong targetSteamId) =>
		_traderRecruit.TryRequest(targetSteamId);
}
