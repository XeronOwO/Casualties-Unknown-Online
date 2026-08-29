using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Projects discrete limb terminal facts from the character-data surface into
/// the kernel Players domain. The legacy character snapshot / limb event
/// remains the live presentation path; this projection makes the durable limb
/// latches available to checkpoint/save/replay.
/// </summary>
public sealed class PlayerKernelLimbProjection(
	ItemKernelAuthority kernelAuthority,
	ISessionControl session,
	ILogger<PlayerKernelLimbProjection> log)
{
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ISessionControl _session = session;
	private readonly ILogger<PlayerKernelLimbProjection> _log = log;

	public void SyncFromLimbEvent(LimbStateEventMsg msg) =>
		Sync(msg.OwnerSteamId, msg.Limbs, null, null);

	public void SyncFromCharacterData(ulong steamId, CharacterDataMsg data) =>
		Sync(steamId, data.Limbs, data.Health?.Alive, data.Health?.Conscious);

	private void Sync(ulong steamId, IReadOnlyList<CharacterLimbMsg>? limbs, bool? alive, bool? conscious)
	{
		if (_session.Role != SessionRole.Host || limbs is null || limbs.Count == 0)
		{
			return;
		}

		PlayerLimbState[] facts = [.. limbs.Select(ToPlayerLimbState)];
		var table = _kernelAuthority.QueryPlayers();
		var current = table?.Players.FirstOrDefault(p => p.SteamId == steamId);
		if (current is not null && FactsEqual(current.LimbFacts, facts))
		{
			return;
		}

		var updated = current is null
			? new PlayerState(steamId, alive ?? true, conscious ?? true, Limbs: facts)
			: current.WithLimbs(facts);

		if (!_kernelAuthority.TryUpdatePlayerStatus(
			_session.LocalSteamId,
			updated,
			out _,
			out var rejection))
		{
			_log.LogWarning(
				"[PlayerLimbKernel] rejected limb projection for {Player}: {Reason} ({Message}).",
				steamId, rejection!.Reason, rejection.Message);
			return;
		}

	}

	private static PlayerLimbState ToPlayerLimbState(CharacterLimbMsg limb) =>
		new(
			limb.Index,
			limb.Broken,
			limb.Dismembered,
			limb.Dislocated,
			limb.Splinted,
			limb.Infected,
			limb.BlockedBleeding,
			limb.IsHead,
			limb.IsVital);

	private static bool FactsEqual(IReadOnlyList<PlayerLimbState> left, IReadOnlyList<PlayerLimbState> right) =>
		left.Count == right.Count
		&& left.OrderBy(l => l.Index).SequenceEqual(right.OrderBy(l => l.Index));
}
