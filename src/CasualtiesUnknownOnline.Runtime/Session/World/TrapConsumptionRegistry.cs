using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Kernel-backed projection for the WorldEntities trap-consumption facts.
/// Host writes commit through the kernel; guests rebuild from checkpoints via
/// <see cref="WorldEntityKernelProjection"/>.
/// </summary>
public sealed class TrapConsumptionRegistry(
	ISessionControl session,
	ITimeSource time,
	ItemKernelAuthority kernelAuthority)
{
	private readonly ISessionControl _session = session;
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
}
