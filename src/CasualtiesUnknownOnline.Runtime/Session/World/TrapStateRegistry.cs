using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Kernel-backed trap state-machine projection. Host-side trap edges are
/// mapped through <see cref="TrapStateProfiles"/> and committed as
/// <see cref="RecordTrapStateCommand"/> batches; guests rebuild the same facts
/// from kernel checkpoints/batches.
/// </summary>
public sealed class TrapStateRegistry(
	ISessionControl session,
	ItemKernelAuthority kernelAuthority,
	ITimeSource time)
{
	private readonly ISessionControl _session = session;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ITimeSource _time = time;

	/// <summary>Host only: record a stateful trap edge by committing a kernel command.</summary>
	public void Report(EntityEventKind kind, float x, float y, byte extra)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		var phase = TrapStateProfiles.Map(kind);
		if (phase is null)
		{
			return;
		}

		var command = new RecordTrapStateCommand(
			_kernelAuthority.NextOperationId(),
			new ActorId(_session.LocalSteamId),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.HostOnly,
			EntityPosition.FromWorld(x, y),
			(int)kind,
			phase.Value,
			extra,
			_time.NowMs);
		_kernelAuthority.TryExecuteHostCommand(command, _session.LocalSteamId, "record-trap-state", out _, out _);
	}
}
