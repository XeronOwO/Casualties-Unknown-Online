using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Projects terminal player alive/conscious facts from the entity-sync surface
/// into the kernel. Kept separate from <see cref="EntitySyncService"/> so the
/// entity state service stays under the architecture line gate.
/// </summary>
public sealed class PlayerKernelStatusProjection(
	ItemKernelAuthority kernelAuthority,
	ISessionControl session)
{
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ISessionControl _session = session;

	public void Sync(ulong steamId, bool alive, bool conscious)
	{
		var table = _kernelAuthority.QueryPlayers();
		var current = table?.Players.FirstOrDefault(p => p.SteamId == steamId);
		if (current is not null && current.Alive == alive && current.Conscious == conscious)
		{
			return;
		}

		var state = current is null
			? new PlayerState(steamId, alive, conscious)
			: current.WithVitals(alive, conscious);

		_kernelAuthority.TryUpdatePlayerStatus(
			_session.LocalSteamId,
			state,
			out _,
			out _);
	}
}
