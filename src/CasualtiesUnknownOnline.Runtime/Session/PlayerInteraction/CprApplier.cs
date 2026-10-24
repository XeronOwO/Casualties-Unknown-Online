using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The CPR family's patient-side physiology (ticket `remote-medical-cpr.md`): compressions that build perfusion and
/// restart the heart, ventilation that carries oxygen, and the rib an over-press breaks. It is its own object because
/// the two channels are one rule set that the applier only projects onto the wire — the applier keeps the snapshots and
/// the state messages, this keeps what a compression and a breath do to a body.
///
/// <para>
/// The split from `OtherMedicalOperationApplier` is the architecture-debt row's own instruction: it was recorded when
/// the rib half pushed the applier past 600 lines, with stage 2 (the oxygen channel) named as the moment to do it.
/// </para>
/// </summary>
internal sealed class CprApplier(PlayerCharacterAccess access)
{
	/// <summary>The limb index the game's own heart checks treat as the chest (`ApplyDefibrillation` tests `limb.Index == 1`).</summary>
	internal const int ChestLimbIndex = 1;

	/// <summary>The pain an over-press's broken rib adds, on the game's own 0-100 limb pain scale.</summary>
	internal const float OverPressRibPain = 25f;

	private readonly PlayerCharacterAccess _access = access;

	/// <summary>
	/// One chest compression: the permission (alive, in arrest), the perfusion the rhythm earns inside the patient's own
	/// ceiling, the restart when the progress crosses it, and the rib an over-press breaks. Returns the target snapshot the
	/// caller commits; <paramref name="restarted"/> tells it the heart came back in THIS compression.
	/// </summary>
	internal bool TryCompress(
		OtherMedicalOperationSession session,
		float intervalSeconds,
		out CharacterDataMsg? newTarget,
		out bool restarted)
	{
		newTarget = null;
		restarted = false;
		var targetData = _access.GetCharacterData(session.Target);
		if (targetData?.Health is not { } health || !health.Alive)
		{
			return false;
		}

		if (health.HeartRate >= 20f)
		{
			// `Body.inCardiacArrest` is heartRate < 20: above it the heart is beating and compressions are not the care.
			// The heart restarting ENDS this CPR's accumulation, whichever procedure caused it (an AED clearing the quiver,
			// or this very rhythm in an earlier compression), so the banked progress is dropped here: a later re-arrest
			// must earn its restart again instead of spending perfusion nobody built.
			session.Progress = 0f;
			return false;
		}

		var ceiling = CprRhythmPolicy.CeilingFor(
			CprRhythmPolicy.BloodVolumeFraction(health.BloodVolume),
			health.Hemothorax);
		var gain = CprRhythmPolicy.PressureGain(intervalSeconds, session.Progress, ceiling);
		var target = PlayerCharacterAccess.CloneCharacter(targetData);
		var newHealth = target.Health!;

		// The operator's cost is NOT charged here: a member's own vitals are authoritative on their own client (read on the
		// machine, batch 20261024-o: five presses charged by the host left the member's own stamina at 100), so the press is
		// paid for on the operator's screen and the host only applies the patient's effect. `CprRhythmPolicy` keeps the
		// numbers both sides read.

		// The compression builds the session's own PERFUSION PROGRESS, not the simulated pressure: in arrest the game's
		// model has no driver for pressure (its whole heart-rate block is skipped while `inCardiacArrest`), so a pressure
		// written into the snapshot is overwritten before the next frame shows it — read on the machine, batch
		// 20261024-j: three presses left the host at bloodPressure 0.374. What the game DOES read is the heart's state, so
		// reaching the threshold restarts it (heartRate above the arrest line, fibrillation cleared) and the model
		// restores the pressure itself from there. The host applies this, which is the ruling's "first to the host wins"
		// arbitration for the shared restart trigger.
		session.Progress = Math.Min(ceiling, session.Progress + gain);
		if (CprRhythmPolicy.RestartsTheHeart(session.Progress, ceiling))
		{
			newHealth.HeartRate = CprRhythmPolicy.PerfusingHeartRate;
			newHealth.FibrillationProgress = 0f;
			newHealth.FibrillationForced = false;
			session.Progress = 0f;
			restarted = true;
		}

		BreakARibIfThePressWasAShove(target, intervalSeconds);
		newTarget = target;
		return true;
	}

	/// <summary>
	/// One ventilation: the breath carries oxygen into a body whose own breathing has stopped. `Body.HandleCirculation`
	/// ADDS to `bloodOxygen` every frame (`respiratoryRate * 0.01 * breathing - 0.5`), so unlike the pressure this value
	/// persists — the game then clamps it at its own ceiling (`100 - hemothorax * 0.3`) and at the volume/viscosity caps.
	/// </summary>
	internal bool TryVentilate(OtherMedicalOperationSession session, float intervalSeconds, out CharacterDataMsg? newTarget)
	{
		newTarget = null;
		var targetData = _access.GetCharacterData(session.Target);
		if (targetData?.Health is not { } health || !health.Alive || health.HeartRate >= 20f)
		{
			return false;
		}

		var ceiling = CprRhythmPolicy.OxygenCeilingFor(health.Hemothorax);
		var gain = CprRhythmPolicy.OxygenGain(intervalSeconds, health.BloodOxygen, ceiling);
		var target = PlayerCharacterAccess.CloneCharacter(targetData);
		target.Health!.BloodOxygen = Math.Min(ceiling, health.BloodOxygen + gain);
		newTarget = target;
		return true;
	}

	private static void BreakARibIfThePressWasAShove(CharacterDataMsg target, float intervalSeconds)
	{
		// A frantic press is a shove: the chest pays for it. The game's own fracture state is the cost, so the patient's
		// chart shows the rib the compression broke rather than a number only this rule knows about.
		if (!CprRhythmPolicy.IsAnOverPress(intervalSeconds))
		{
			return;
		}

		foreach (var limb in target.Limbs)
		{
			if (limb.Index != ChestLimbIndex || limb.Broken)
			{
				continue;
			}

			limb.Broken = true;
			limb.Pain = Math.Min(100f, limb.Pain + OverPressRibPain);
			return;
		}
	}
}
