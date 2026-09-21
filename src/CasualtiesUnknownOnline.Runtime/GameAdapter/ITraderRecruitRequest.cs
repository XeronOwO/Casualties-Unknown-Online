namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The Online UI's trader-recruit entry: the local player asks a friendly trader
/// to revive a dead in-world teammate. A request, not a decision — the host
/// remains the authority for the trade gates and the revive result.
/// </summary>
public interface ITraderRecruitRequest
{
	/// <summary>
	/// Online UI entry: the local player requests a trader recruit of a dead
	/// in-world teammate. Returns false when there is no session/world or no
	/// trader within range; the host remains the authority for the actual
	/// trade gates and the revive result.
	/// </summary>
	bool TryRequestTraderRecruit(ulong targetSteamId);
}
