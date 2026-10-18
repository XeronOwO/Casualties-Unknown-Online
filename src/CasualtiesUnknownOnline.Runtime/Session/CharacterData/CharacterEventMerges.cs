using System;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The event-merge seams over the host's saved character table: each takes an event the host
/// arbitrated and applies its terminal body state to the snapshot the table holds for the body's
/// owner, so a disconnect before the next 1 Hz report still restores it. The TABLE stays in
/// <see cref="CharacterDataStore"/> (save/restore, the report filter and the authority save); this
/// class owns how each event shape is interpreted — its finite guard, the per-limb clamp and the
/// authority direction of the three continuous values.
///
/// <para>
/// Extracted from the store when the heal authority event pushed that file over the
/// aggregate-line ceiling: the store now keeps one forwarding call per event, so a handler sees the
/// same surface it always did, and the merge rules have one home that can be read on its own.
/// </para>
/// </summary>
internal sealed class CharacterEventMerges
{
	private readonly Func<ulong, CharacterDataMsg?> _saved;
	private readonly PlayerKernelLimbProjection _playerLimbKernel;
	private readonly ILogger _log;

	internal CharacterEventMerges(
		Func<ulong, CharacterDataMsg?> saved,
		PlayerKernelLimbProjection playerLimbKernel,
		ILogger log)
	{
		_saved = saved;
		_playerLimbKernel = playerLimbKernel;
		_log = log;
	}

	/// <summary>
	/// Host side: merge an enemy bite's post-bite terminal state into the victim's saved snapshot
	/// immediately — a disconnect before the next report must still restore the bite (the dedicated
	/// event is the trigger; the snapshot is only the fallback).
	/// </summary>
	internal void ApplyEnemyBite(EnemyBiteMsg msg)
	{
		if (RefuseEnemyEvent(msg.VictimSteamId, msg, msg.Limb, "bite event"))
		{
			return;
		}

		if (_saved(msg.VictimSteamId) is { } data)
		{
			EnemyTerminalStateApplier.ApplyBite(data, msg);
			HeadMouthRule.Refresh(data);
			data.LocalRevision++;
		}
	}

	/// <summary>The <see cref="ApplyEnemyBite"/> mirror for a crystal-lunge terminal state.</summary>
	internal void ApplyEnemyLunge(EnemyLungeMsg msg)
	{
		if (RefuseEnemyEvent(msg.VictimSteamId, msg, msg.Limb, "lunge event"))
		{
			return;
		}

		if (_saved(msg.VictimSteamId) is { } data)
		{
			EnemyTerminalStateApplier.ApplyLunge(data, msg);
			HeadMouthRule.Refresh(data);
			data.LocalRevision++;
		}
	}

	/// <summary>The <see cref="ApplyEnemyBite"/> mirror for an enemy-proximity side effect.</summary>
	internal void ApplyEnemyEffect(EnemyEffectMsg msg)
	{
		if (RefuseEnemyEvent(msg.VictimSteamId, msg, limb: null, "effect event"))
		{
			return;
		}

		if (_saved(msg.VictimSteamId) is { } data)
		{
			EnemyTerminalStateApplier.ApplyEffect(data, msg);
			HeadMouthRule.Refresh(data);
			data.LocalRevision++;
		}
	}

	/// <summary>
	/// A limb-latch/injury event's full terminal state: merge it into the owner's saved snapshot and
	/// project the limb/body facts into the kernel. False when the event was refused (non-finite), so
	/// the caller skips the adapter event and the relay — the fact table shares the stored object
	/// reference and would otherwise apply and relay the refused values.
	/// </summary>
	internal bool ApplyLimbStateEvent(LimbStateEventMsg msg)
	{
		var eventData = new CharacterDataMsg { Health = msg.Health, Limbs = msg.Limbs };
		if (RefuseNonFinite(msg.OwnerSteamId, eventData, "owner limb/injury event"))
		{
			return false;
		}

		if (_saved(msg.OwnerSteamId) is { } data)
		{
			EnemyTerminalStateApplier.ApplyLimbState(data, msg);
			HeadMouthRule.Refresh(data);
			data.LocalRevision++;
		}

		_playerLimbKernel.SyncFromLimbEvent(msg);
		return true;
	}

	/// <summary>
	/// The reset result a member's own client applied, merged into that member's saved snapshot
	/// through the AUTHORITY path — unlike a plain report, the reset's blood volume, total bleeding
	/// speed and per-limb bleed are kept as written, which is the whole point of the event. The event
	/// is bounded by facts this host holds: the transport sender's own body and the instruction id
	/// (checked by the caller), and the reset TABLE's healthy values for the three owned fields —
	/// the event may only state the reset the host ordered, never an arbitrary blood volume or
	/// bleed. The finite guard and the table comparison both refuse the event WHOLE by name; then the
	/// body half is replaced and the in-place revision bump tells the host clone to map the result on
	/// its next fixed step.
	/// </summary>
	internal bool ApplyHealResetEvent(HealResetAppliedMsg msg)
	{
		if (msg.Health is null)
		{
			_log.LogError("[Character] refusing a heal reset event for {Peer}: it carries no health record — the previous snapshot stays.", msg.OwnerSteamId);
			return false;
		}

		if (msg.Limbs.Count == 0)
		{
			_log.LogError("[Character] refusing a heal reset event for {Peer}: it carries no limbs, so it cannot be the reset's full body state — the previous snapshot stays.", msg.OwnerSteamId);
			return false;
		}

		var eventData = new CharacterDataMsg { Health = msg.Health, Limbs = [.. msg.Limbs] };
		if (RefuseNonFinite(msg.OwnerSteamId, eventData, "heal reset event"))
		{
			return false;
		}

		if (!MatchesResetTable(msg))
		{
			return false;
		}

		if (_saved(msg.OwnerSteamId) is not { } data)
		{
			// The result cannot be merged into a record this host does not hold. Nothing durable is
			// written, and the plain report that follows a fresh member's first snapshot is what
			// establishes the healed baseline (there is no previous snapshot for Preserve to keep).
			_log.LogWarning("[Character] a heal reset event for {Peer} has no saved snapshot to merge into — refused; the next report establishes the baseline.",
				msg.OwnerSteamId);
			return false;
		}

		data.Health = msg.Health;
		data.Limbs = [.. msg.Limbs];
		data.LocalRevision++;
		_playerLimbKernel.SyncFromCharacterData(msg.OwnerSteamId, data);
		_log.LogInformation("[HealReset] stored {Peer}'s reset result through the authority path (instruction {InstructionId}, blood {Blood:F1}, total bleed {Total:F1}, {Limbs} limbs).",
			msg.OwnerSteamId, msg.InstructionId, data.Health.BloodVolume, data.Health.TotalBleedSpeed, data.Limbs.Count);
		return true;
	}

	/// <summary>
	/// The table anchor of the event's three host-owned values. The host issued this instruction from
	/// <see cref="HealResetState"/>; a target that applied that reset can only capture the table's own
	/// healthy values, so any other blood volume, total bleeding speed or per-limb bleed is a claim the
	/// host can disprove with data it already holds (not a second simulation of the body). A mismatch
	/// is refused whole by name and the previous snapshot stays.
	/// </summary>
	private bool MatchesResetTable(HealResetAppliedMsg msg)
	{
		var health = msg.Health!; // the caller refused a missing health record
		if (!Near(health.BloodVolume, HealResetState.BloodVolume))
		{
			return RefuseMismatch(msg.OwnerSteamId, "health.BloodVolume", health.BloodVolume, HealResetState.BloodVolume);
		}

		if (!Near(health.TotalBleedSpeed, HealResetState.TotalBleedSpeed))
		{
			return RefuseMismatch(msg.OwnerSteamId, "health.TotalBleedSpeed", health.TotalBleedSpeed, HealResetState.TotalBleedSpeed);
		}

		foreach (var limb in msg.Limbs)
		{
			if (!Near(limb.BleedAmount, HealResetState.LimbBleedAmount))
			{
				return RefuseMismatch(msg.OwnerSteamId, $"limb {limb.Index}.BleedAmount", limb.BleedAmount, HealResetState.LimbBleedAmount);
			}
		}

		return true;
	}

	private bool RefuseMismatch(ulong peer, string field, float claimed, float expected)
	{
		_log.LogError("[Character] refusing a heal reset event for {Peer}: {Field} is {Claimed:F3}, but the reset table writes {Expected:F3} — the previous snapshot stays.",
			peer, field, claimed, expected);
		return false;
	}

	private static bool Near(float value, float expected) =>
		Math.Abs(value - expected) <= ContinuousVitals.AuthorityEpsilon;

	/// <summary>
	/// The in-place enemy merges bypass SaveCharacterData, so they get the finiteness gate here: a
	/// non-finite limb or body float would poison the host clone (and the row) from an authority
	/// path that no report can repair.
	/// </summary>
	private bool RefuseEnemyEvent(ulong victim, object message, CharacterLimbMsg? limb, string kind)
	{
		var field = CharacterDataFinite.FirstNonFiniteFields(message)
			?? (limb is null ? null : CharacterDataFinite.FirstNonFiniteLimb(limb));
		if (field is null)
		{
			return false;
		}

		_log.LogError("[Character] refusing a non-finite {Kind} for {Peer}: {Field}.", kind, victim, field);
		return true;
	}

	private bool RefuseNonFinite(ulong steamId, CharacterDataMsg data, string path) =>
		CharacterDataFinite.Refuse(_log, steamId, data, path);
}
