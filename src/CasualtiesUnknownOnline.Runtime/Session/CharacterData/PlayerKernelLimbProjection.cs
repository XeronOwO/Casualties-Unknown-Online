using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Projects discrete limb terminal facts, body-level terminal latches, and
/// durable skill facts from the character-data surface into the kernel Players
/// domain. The legacy character snapshot / limb event remains the live
/// presentation path; this projection makes the durable player facts available
/// to checkpoint/save/replay.
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
		Sync(msg.OwnerSteamId, msg.Limbs, null, null, ToBodyTerminalState(msg.Health), null);

	public void SyncFromCharacterData(ulong steamId, CharacterDataMsg data) =>
		Sync(
			steamId,
			data.Limbs,
			data.Health?.Alive,
			data.Health?.Conscious,
			ToBodyTerminalState(data.Health),
			ToPlayerSkills(data.Skills));

	private void Sync(
		ulong steamId,
		IReadOnlyList<CharacterLimbMsg>? limbs,
		bool? alive,
		bool? conscious,
		PlayerBodyTerminalState? body,
		PlayerSkillsState? skills)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		var table = _kernelAuthority.QueryPlayers();
		var current = table?.Players.FirstOrDefault(p => p.SteamId == steamId);
		if (current is null
			&& limbs is not { Count: > 0 }
			&& alive is null
			&& conscious is null
			&& body is null
			&& skills is null)
		{
			return;
		}

		var facts = limbs is { Count: > 0 }
			? [.. limbs.Select(ToPlayerLimbState)]
			: current?.LimbFacts ?? [];
		var mergedBody = body ?? current?.Body;
		var mergedSkills = skills ?? current?.Skills;
		if (current is not null
			&& current.Alive == (alive ?? current.Alive)
			&& current.Conscious == (conscious ?? current.Conscious)
			&& FactsEqual(current.LimbFacts, facts)
			&& Equals(current.Body, mergedBody)
			&& Equals(current.Skills, mergedSkills))
		{
			return;
		}

		var updated = current is null
			? new PlayerState(
				steamId,
				alive ?? true,
				conscious ?? true,
				Limbs: facts.Count == 0 ? null : facts,
				Body: body,
				Skills: skills)
			: current
				.WithVitals(alive ?? current.Alive, conscious ?? current.Conscious)
				.WithLimbs(facts.Count == 0 ? null : facts);

		if (body is not null)
		{
			updated = updated.WithBody(body);
		}

		if (skills is not null)
		{
			updated = updated.WithSkills(skills);
		}

		if (!_kernelAuthority.TryUpdatePlayerStatus(
			_session.LocalSteamId,
			updated,
			out _,
			out var rejection))
		{
			_log.LogWarning(
				"[PlayerLimbKernel] rejected character projection for {Player}: {Reason} ({Message}).",
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

	private static PlayerBodyTerminalState? ToBodyTerminalState(CharacterHealthMsg? health) =>
		health is null
			? null
			: new PlayerBodyTerminalState(
				health.Disfigured,
				health.EyeGone,
				health.BothEyesGone,
				health.HasPulmonaryEmbolism,
				health.TriedRollingLastStand,
				health.SuccesfullyRolledLastStand,
				health.UsedNeuralBooster,
				health.FibrillationForced,
				health.MindwipeScriptPresent,
				health.MindwipeScriptActive);

	private static PlayerSkillsState? ToPlayerSkills(CharacterSkillsMsg? skills) =>
		skills is null
			? null
			: new PlayerSkillsState(
				skills.Strength,
				skills.Resistance,
				skills.Intelligence,
				skills.ExpStrength,
				skills.ExpResistance,
				skills.ExpIntelligence);

	private static bool FactsEqual(IReadOnlyList<PlayerLimbState> left, IReadOnlyList<PlayerLimbState> right) =>
		left.Count == right.Count
		&& left.OrderBy(l => l.Index).SequenceEqual(right.OrderBy(l => l.Index));
}
