using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Projects authoritative kernel player terminal facts into a
/// <see cref="CharacterDataMsg"/> before it is handed back as a
/// reconnect/re-entry restore. The kernel is the single authority for
/// alive/conscious, discrete limb latches, body-level terminal latches, durable
/// skill facts, and the carry relation; the legacy character snapshot stays the
/// authority for continuous physiological fields, items, and position. Carry is
/// not a character-snapshot field and is restored separately through
/// PlayerKernelCarryProjection from checkpoints and committed batches.
/// </summary>
public sealed class PlayerKernelRestoreProjection(
	ItemKernelAuthority kernelAuthority,
	ILogger<PlayerKernelRestoreProjection> log)
{
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ILogger<PlayerKernelRestoreProjection> _log = log;

	public void Apply(ulong steamId, CharacterDataMsg data)
	{
		var player = _kernelAuthority.QueryPlayers()?.Players.FirstOrDefault(p => p.SteamId == steamId);
		if (player is null)
		{
			_log.LogDebug("[PlayerKernelRestore] no kernel player facts for {Player}; snapshot remains the fallback.", steamId);
			return;
		}

		if (data.Health is { } health)
		{
			health.Alive = player.Alive;
			health.Conscious = player.Conscious;
			if (player.Body is { } body)
			{
				ApplyBody(health, body);
			}
		}
		else
		{
			_log.LogDebug("[PlayerKernelRestore] {Player} has kernel terminal facts but no snapshot health block; only limb latches can be projected.", steamId);
		}

		foreach (var limb in player.LimbFacts)
		{
			var existing = data.Limbs.FirstOrDefault(l => l.Index == limb.Index);
			if (existing is null)
			{
				data.Limbs.Add(ToLimbMessage(limb));
				_log.LogDebug("[PlayerKernelRestore] added kernel limb fact {Limb} to restore of {Player}.", limb.Index, steamId);
				continue;
			}

			ApplyLimb(existing, limb);
		}

		if (player.Skills is { } skills)
		{
			data.Skills ??= new CharacterSkillsMsg();
			ApplySkills(data.Skills, skills);
		}

		_log.LogDebug("[PlayerKernelRestore] projected kernel player facts for {Player}.", steamId);
	}

	private static void ApplyBody(CharacterHealthMsg health, PlayerBodyTerminalState body)
	{
		health.Disfigured = body.Disfigured;
		health.EyeGone = body.EyeGone;
		health.BothEyesGone = body.BothEyesGone;
		health.HasPulmonaryEmbolism = body.HasPulmonaryEmbolism;
		health.TriedRollingLastStand = body.TriedRollingLastStand;
		health.SuccesfullyRolledLastStand = body.SuccesfullyRolledLastStand;
		health.UsedNeuralBooster = body.UsedNeuralBooster;
		health.FibrillationForced = body.FibrillationForced;
		health.MindwipeScriptPresent = body.MindwipeScriptPresent;
		health.MindwipeScriptActive = body.MindwipeScriptActive;
	}

	private static void ApplySkills(CharacterSkillsMsg target, PlayerSkillsState skills)
	{
		target.Strength = skills.Strength;
		target.Resistance = skills.Resistance;
		target.Intelligence = skills.Intelligence;
		target.ExpStrength = skills.ExpStrength;
		target.ExpResistance = skills.ExpResistance;
		target.ExpIntelligence = skills.ExpIntelligence;
	}

	private static CharacterLimbMsg ToLimbMessage(PlayerLimbState limb) => new()
	{
		Index = limb.Index,
		Broken = limb.Broken,
		Dismembered = limb.Dismembered,
		Dislocated = limb.Dislocated,
		Splinted = limb.Splinted,
		Infected = limb.Infected,
		BlockedBleeding = limb.BlockedBleeding,
		IsHead = limb.IsHead,
		IsVital = limb.IsVital,
	};

	private static void ApplyLimb(CharacterLimbMsg target, PlayerLimbState limb)
	{
		target.Broken = limb.Broken;
		target.Dismembered = limb.Dismembered;
		target.Dislocated = limb.Dislocated;
		target.Splinted = limb.Splinted;
		target.Infected = limb.Infected;
		target.BlockedBleeding = limb.BlockedBleeding;
		target.IsHead = limb.IsHead;
		target.IsVital = limb.IsVital;
	}
}
