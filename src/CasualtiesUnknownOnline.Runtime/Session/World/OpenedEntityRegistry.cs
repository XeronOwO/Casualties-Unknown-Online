using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Projection over the kernel's WorldEntities opened-entity facts. Host writes
/// commit through the kernel; this type only builds the legacy world-entry
/// snapshot payload from the authoritative query.
/// </summary>
public sealed class OpenedEntityRegistry(
	ISessionControl session,
	PacketSender sender,
	ItemKernelAuthority kernelAuthority)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;

	/// <summary>Host only: record an opened entity by committing a kernel command.</summary>
	public void Report(float x, float y)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		_kernelAuthority.TryRecordOpenedEntity(
			_session.LocalSteamId,
			EntityPosition.FromWorld(x, y),
			out _,
			out _);
	}

	/// <summary>Host only: a new world layer is generating — the kernel table starts empty again.</summary>
	public void Reset()
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		_kernelAuthority.TryResetWorldEntities(_session.LocalSteamId, out _, out _);
	}

	/// <summary>Host only: send the opened positions to one member (on its world entry).</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || targetSteamId == 0)
		{
			return;
		}

		var state = _kernelAuthority.QueryWorldEntities();
		if (state is null || state.OpenedEntities.Count == 0)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.OpenedEntitiesSnapshot, new OpenedEntitiesSnapshotMsg
		{
			Positions = [.. state.OpenedEntities.Select(o => new NetVector2Msg(o.Position.CenterX, o.Position.CenterY))],
		});
	}
}
