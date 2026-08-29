using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Projects host-authoritative cross-player carry mutations into the kernel
/// Players domain. The legacy wire carry-state broadcast remains the live
/// presentation/mirror path; this projection adds the durable kernel fact for
/// checkpoint/save/replay.
/// </summary>
internal sealed class PlayerKernelCarryProjection(
	ItemKernelAuthority kernelAuthority,
	ISessionControl session,
	ILogger log)
{
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ISessionControl _session = session;
	private readonly ILogger _log = log;

	public void SetCarry(ulong carrierSteamId, ulong carriedSteamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		if (!_kernelAuthority.TrySetPlayerCarry(
			_session.LocalSteamId,
			carrierSteamId,
			carriedSteamId,
			out _,
			out var rejection))
		{
			_log.LogWarning(
				"[CarryKernel] set rejected {Carrier} -> {Carried}: {Reason} ({Message}).",
				carrierSteamId, carriedSteamId, rejection!.Reason, rejection.Message);
			return;
		}

		_log.LogDebug("[CarryKernel] committed {Carrier} -> {Carried}.", carrierSteamId, carriedSteamId);
	}

	public void ClearCarry(ulong carrierSteamId, ulong carriedSteamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		if (!_kernelAuthority.TryClearPlayerCarry(
			_session.LocalSteamId,
			carrierSteamId,
			carriedSteamId,
			out _,
			out var rejection))
		{
			_log.LogWarning(
				"[CarryKernel] clear rejected {Carrier} ({Carried}): {Reason} ({Message}).",
				carrierSteamId, carriedSteamId, rejection!.Reason, rejection.Message);
		}
	}
}
