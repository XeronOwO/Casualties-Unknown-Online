using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The pure trader-recruit rules (KrokMP-inspired co-op revive). This is the
/// L0-locked decision surface for the Unity-facing
/// <c>TraderRecruitCoordinator</c>: trader gates, dead-player detection and the
/// post-revive physiological state. The revive intentionally uses the existing
/// character-snapshot shape — no inventory wipe and no position teleport; the
/// target's local body is only healed in place.
/// </summary>
internal static class TraderRecruitPolicy
{
	/// <summary>A trader must be this friendly to offer a recruit (above the
	/// game's own 70-point AskToMove gate, kept as a separate co-op threshold).</summary>
	internal const float MinReputation = 75f;

	/// <summary>The trader's building must be healthy enough to host a recruit
	/// (the same 200-point purchase gate TraderScript.cs uses).</summary>
	internal const float MinBuildHealth = 200f;

	/// <summary>How close a player must be to a trader to request a recruit
	/// (a little wider than the 6-unit conversation trigger, so the button is
	/// usable while standing at a trader).</summary>
	internal const float RecruitRange = 8f;

	/// <summary>Minimum trader-stock items granted on a successful recruit.</summary>
	internal const int MinGiftItems = 1;

	/// <summary>Maximum trader-stock items granted on a successful recruit.</summary>
	internal const int MaxGiftItems = 3;

	/// <summary>The target is revivable only when the host's authoritative
	/// character snapshot says the player is dead (in world still).</summary>
	internal static bool IsDead(CharacterDataMsg? data) =>
		data?.Health is { } health && !health.Alive;

	/// <summary>
	/// The empty backpack/hand slots in a character snapshot. Worn items
	/// (negative SlotIndex) do not occupy a slot; an older snapshot with
	/// SlotCount=0 falls back to the game's known minimum (3), matching the
	/// cross-player interaction service.
	/// </summary>
	internal static IReadOnlyList<int> FindEmptySlots(CharacterDataMsg data)
	{
		var count = data.SlotCount > 0 ? data.SlotCount : 3;
		var occupied = data.Items.Where(i => i.SlotIndex >= 0).Select(i => i.SlotIndex).ToHashSet();
		var empty = new List<int>();
		for (var slot = 0; slot < count; slot++)
		{
			if (!occupied.Contains(slot))
			{
				empty.Add(slot);
			}
		}

		return empty;
	}

	/// <summary>
	/// Choose <paramref name="count"/> distinct trader-stock item ids. The
	/// random source is injected as a function returning an index in
	/// <c>[0, remaining.Count)</c>, so the pure policy stays L0-testable while
	/// the Unity-facing coordinator supplies <c>Random.Range</c>.
	/// </summary>
	internal static IReadOnlyList<string> SelectGiftItemIds(
		TradeStockState stock,
		int count,
		Func<int, int> randomIndex)
	{
		var available = stock.Items.Select(i => i.Id).Distinct(StringComparer.Ordinal).ToList();
		var selected = new List<string>();
		if (count <= 0 || available.Count == 0)
		{
			return selected;
		}

		var remaining = new List<string>(available);
		for (var i = 0; i < count && remaining.Count > 0; i++)
		{
			var index = randomIndex(remaining.Count);
			if (index < 0 || index >= remaining.Count)
			{
				continue;
			}

			selected.Add(remaining[index]);
			remaining.RemoveAt(index);
		}

		return selected;
	}

	/// <summary>Trade gates for one recruit: the trader has not already been
	/// used, is not hostile, is friendly enough and its building is intact.</summary>
	internal static bool CanRecruit(TradeStockState trader, bool used) =>
		!used
		&& trader.Hostility <= 0f
		&& trader.Reputation >= MinReputation
		&& trader.BuildHealth > MinBuildHealth;

	/// <summary>
	/// Build the post-revive character snapshot from the target's last host-side
	/// save: every item/skill/limb stays, only the lethal physiological fields
	/// are returned to a safe, conscious baseline.
	/// </summary>
	internal static CharacterDataMsg PrepareRevive(CharacterDataMsg source)
	{
		var health = source.Health is { } original
			? new CharacterHealthMsg
			{
				BloodVolume = 100f,
				BloodOxygen = 100f,
				HeartRate = 70f,
				RespiratoryRate = 100f,
				BloodPressure = 120f,
				BloodVesselSize = 1f,
				FibrillationProgress = 0f,
				FibrillationForced = false,
				Adrenaline = original.Adrenaline,
				CurAdrenaline = original.CurAdrenaline,
				Hunger = original.Hunger,
				Thirst = original.Thirst,
				Stamina = 100f,
				Energy = 100f,
				Happiness = original.Happiness,
				WeightOffset = original.WeightOffset,
				BrainHealth = 75f,
				Consciousness = 100f,
				Alive = true,
				Conscious = true,
				Shock = 0f,
				SicknessAmount = original.SicknessAmount,
				DesensitizedMult = original.DesensitizedMult,
				CorpsesSeen = original.CorpsesSeen,
				SepticShock = 0f,
				Disfigured = original.Disfigured,
				EyeGone = original.EyeGone,
				BothEyesGone = original.BothEyesGone,
				RadiationSickness = original.RadiationSickness,
				Caffeinated = original.Caffeinated,
				HearingLoss = original.HearingLoss,
				InternalBleeding = 0f,
				Hemothorax = 0f,
				PainShock = 0f,
				TraumaAmount = original.TraumaAmount,
				Wetness = original.Wetness,
				BadSleepAmount = original.BadSleepAmount,
				GoodSleepTime = original.GoodSleepTime,
				SnowAmount = original.SnowAmount,
				Immunity = original.Immunity,
				AntibioticImmunityTime = original.AntibioticImmunityTime,
				TriedRollingLastStand = original.TriedRollingLastStand,
				SuccesfullyRolledLastStand = original.SuccesfullyRolledLastStand,
				LastStandTime = original.LastStandTime,
				Dirtyness = original.Dirtyness,
				BrainGrowSickness = original.BrainGrowSickness,
				UsedNeuralBooster = original.UsedNeuralBooster,
				ClawHealth = original.ClawHealth,
				ClawRegrowTime = original.ClawRegrowTime,
				HasPulmonaryEmbolism = false,
				StrokeAmount = 0f,
				BloodPressureChangeFromMedicine = original.BloodPressureChangeFromMedicine,
				VenomTotal = original.VenomTotal,
				VenomCurrent = original.VenomCurrent,
				MaxSpeed = original.MaxSpeed,
				JumpSpeed = original.JumpSpeed,
				TemporarySlowdown = original.TemporarySlowdown,
				MoveForce = original.MoveForce,
				SlowdownAmount = original.SlowdownAmount,
				Temperature = 37f,
				HorrifiedLevel = original.HorrifiedLevel,
				FocusedLevel = original.FocusedLevel,
				EyePanicTime = original.EyePanicTime,
				DisfiguredIndex = original.DisfiguredIndex,
				DisfiguredTimeFullSkin = original.DisfiguredTimeFullSkin,
				EyeTimeHealed = original.EyeTimeHealed,
			}
			: new CharacterHealthMsg
			{
				BloodVolume = 100f,
				BloodOxygen = 100f,
				HeartRate = 70f,
				RespiratoryRate = 100f,
				BloodPressure = 120f,
				BloodVesselSize = 1f,
				Hunger = 100f,
				Thirst = 100f,
				Stamina = 100f,
				Energy = 100f,
				BrainHealth = 75f,
				Consciousness = 100f,
				Alive = true,
				Conscious = true,
				Temperature = 37f,
			};

		return new CharacterDataMsg
		{
			Skills = source.Skills,
			Health = health,
			Limbs = [.. source.Limbs.Select(CloneLimb)],
			Items = [.. source.Items],
			HandSlot = source.HandSlot,
			OwnerSteamId = source.OwnerSteamId,
			Position = source.Position,
			SlotCount = source.SlotCount,
		};
	}

	private static CharacterLimbMsg CloneLimb(CharacterLimbMsg limb) => new()
	{
		Index = limb.Index,
		SkinHealth = limb.SkinHealth,
		MuscleHealth = limb.MuscleHealth,
		Broken = limb.Broken,
		Dislocated = limb.Dislocated,
		Splinted = limb.Splinted,
		Infected = limb.Infected,
		InfectionAmount = limb.InfectionAmount,
		BleedAmount = limb.BleedAmount,
		DisinfectionTime = limb.DisinfectionTime,
		Pain = limb.Pain,
		DislocationTimer = limb.DislocationTimer,
		BoneHealTimer = limb.BoneHealTimer,
		BlockedBleeding = limb.BlockedBleeding,
		Shrapnel = limb.Shrapnel,
		FurBloodAmount = limb.FurBloodAmount,
		BandageSlowAmount = limb.BandageSlowAmount,
		SkinHealAmount = limb.SkinHealAmount,
		Dismembered = limb.Dismembered,
		Components = [.. limb.Components],
		IsHead = limb.IsHead,
		IsVital = limb.IsVital,
	};
}
