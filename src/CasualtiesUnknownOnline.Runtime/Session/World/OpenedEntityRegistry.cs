using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Kernel-backed projection for the WorldEntities opened-entity facts.
/// Host writes commit through the kernel; guests rebuild from checkpoints via
/// <see cref="WorldEntityKernelProjection"/>.
/// </summary>
public sealed class OpenedEntityRegistry(
	ISessionControl session,
	ItemKernelAuthority kernelAuthority)
{
	private readonly ISessionControl _session = session;
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
}
