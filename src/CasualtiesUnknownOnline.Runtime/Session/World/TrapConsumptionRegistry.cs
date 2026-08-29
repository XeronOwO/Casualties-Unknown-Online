using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Projection over the kernel's WorldEntities trap-consumption facts. Host
/// writes now commit through the kernel; this type only builds the legacy
/// world-entry snapshot payload from the authoritative query.
/// </summary>
public sealed class TrapConsumptionRegistry(
	ISessionControl session,
	PacketSender sender,
	ITimeSource time,
	ItemKernelAuthority kernelAuthority)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ITimeSource _time = time;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;

	/// <summary>Host only: record a one-shot consumption by committing a kernel command.</summary>
	public void Report(EntityEventKind kind, float x, float y, byte extra)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		_kernelAuthority.TryRecordTrapConsumed(
			_session.LocalSteamId,
			EntityPosition.FromWorld(x, y),
			(int)kind,
			extra,
			_time.NowMs,
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

	/// <summary>Host only: send the consumptions to one member (on its world entry).</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || targetSteamId == 0)
		{
			return;
		}

		var state = _kernelAuthority.QueryWorldEntities();
		if (state is null || state.Consumptions.Count == 0)
		{
			return;
		}

		var now = _time.NowMs;
		_sender.Send(targetSteamId, NetMsg.TrapStateSnapshot, new TrapStateSnapshotMsg
		{
			Consumed = [.. state.Consumptions.Select(c => new EntityEventMsg
			{
				Kind = (EntityEventKind)c.Kind,
				Extra = c.Extra,
				Position = new NetVector2Msg(c.Position.CenterX, c.Position.CenterY),
				ElapsedSeconds = (now - c.TriggeredAtMs) / 1000f,
			})],
		});
	}
}
