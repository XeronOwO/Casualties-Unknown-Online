using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure conversions between player-interaction kernel result payloads and the
/// Protocol wire DTOs. Kept separate from <see cref="KernelWireMapper"/> so the
/// interaction result mapping can grow without pushing the core mapper over the
/// architecture line gate.
/// </summary>
public static class PlayerInteractionWireMapper
{
	public static WirePlayerInteraction ToWire(PlayerInventoryTransferEvent e) =>
		new()
		{
			FromSteamId = e.FromSteamId,
			ToSteamId = e.ToSteamId,
			ItemIdentity = ToWireIdentity(e.Item.Identity),
			ItemData = ToWireData(e.Item.Data),
			ItemContents = [.. e.Item.Children.Select(ToWireItem)],
		};

	public static WirePlayerInteraction ToWire(PlayerHealResultEvent e) =>
		new()
		{
			FromSteamId = e.HealerSteamId,
			ToSteamId = e.TargetSteamId,
			ItemInstanceId = e.ItemInstanceId,
			ItemDestroyed = e.ItemDestroyed,
			ItemConditionAfter = e.ItemConditionAfter,
			HealedLimbIndex = e.HealedLimbIndex,
			Health = e.Health is null ? null : ToWireHealth(e.Health),
			Limbs = [.. e.Limbs.Select(ToWireLimb)],
		};

	public static WirePlayerInteraction ToWire(PlayerItemUseResultEvent e)
	{
		var wire = new WirePlayerInteraction
		{
			FromSteamId = e.UserSteamId,
			ToSteamId = e.TargetSteamId,
			ItemInstanceId = e.ItemInstanceId,
			ItemDestroyed = e.ItemDestroyed,
			Health = e.Health is null ? null : ToWireHealth(e.Health),
			Limbs = [.. e.Limbs.Select(ToWireLimb)],
			TimedEffects = [.. e.TimedEffects.Select(t => new WirePlayerInteractionTimedLimbEffect
			{
				LimbIndex = t.LimbIndex,
				DurationSeconds = t.DurationSeconds,
				BleedPerSecond = t.BleedPerSecond,
			})],
			TimedBodyEffects = [.. e.TimedBodyEffects.Select(t => new WirePlayerInteractionTimedBodyEffect
			{
				EffectId = t.EffectId,
				DurationSeconds = t.DurationSeconds,
				DoseMl = t.DoseMl,
			})],
		};

		if (e.ItemAfter is { } after)
		{
			wire.ItemAfterIdentity = ToWireIdentity(after.Identity);
			wire.ItemAfterData = ToWireData(after.Data);
			wire.ItemAfterContents = [.. after.Children.Select(ToWireItem)];
		}

		if (e.WornItem is { } worn)
		{
			wire.WornItemIdentity = ToWireIdentity(worn.Identity);
			wire.WornItemData = ToWireData(worn.Data);
			wire.WornItemContents = [.. worn.Children.Select(ToWireItem)];
		}

		return wire;
	}

	public static PlayerInventoryTransferEvent FromWireInventoryTransfer(WirePlayerInteraction p) =>
		new(
			p.FromSteamId,
			p.ToSteamId,
			FromWireItem(p.ItemIdentity, p.ItemData, p.ItemContents) ?? throw new System.InvalidOperationException("inventory transfer event lacks item payload"));

	public static PlayerHealResultEvent FromWireHealResult(WirePlayerInteraction p) =>
		new(
			p.FromSteamId,
			p.ToSteamId,
			p.ItemInstanceId,
			p.ItemDestroyed,
			p.ItemConditionAfter,
			p.HealedLimbIndex,
			FromWireHealth(p.Health),
			[.. p.Limbs.Select(FromWireLimb)]);

	public static PlayerItemUseResultEvent FromWireItemUseResult(WirePlayerInteraction p) =>
		new(
			p.FromSteamId,
			p.ToSteamId,
			p.ItemInstanceId,
			p.ItemDestroyed,
			FromWireItem(p.ItemAfterIdentity, p.ItemAfterData, p.ItemAfterContents),
			FromWireItem(p.WornItemIdentity, p.WornItemData, p.WornItemContents),
			FromWireHealth(p.Health),
			[.. p.Limbs.Select(FromWireLimb)],
			[.. p.TimedEffects.Select(t => new PlayerInteractionTimedLimbEffect(t.LimbIndex, t.DurationSeconds, t.BleedPerSecond))],
			[.. p.TimedBodyEffects.Select(t => new PlayerInteractionTimedBodyEffect(t.EffectId, t.DurationSeconds, t.DoseMl))]);

	private static WireItemIdentity ToWireIdentity(ItemIdentity identity) =>
		new()
		{
			InstanceId = identity.InstanceId,
			DefinitionId = identity.DefinitionId,
		};

	private static WireItemData ToWireData(ItemData data) =>
		new()
		{
			Condition = data.Condition,
			Favourited = data.Favourited,
			SlotIndex = data.SlotIndex,
			Liquids = [.. data.Liquids.Select(l => new WireLiquidStack { LiquidId = l.LiquidId, Amount = l.Amount })],
			Components = [.. data.Components.Select(ToWireComponent)],
		};

	private static PlayerInteractionItem? FromWireItem(
		WireItemIdentity? identity,
		WireItemData? data,
		IReadOnlyList<WirePlayerInteractionItem>? contents)
	{
		if (identity is null || data is null)
		{
			return null;
		}

		return new PlayerInteractionItem(
			new ItemIdentity(identity.InstanceId, identity.DefinitionId),
			FromWireData(data),
			contents is null ? null : [.. contents.Select(FromWireItem)]);
	}

	private static WirePlayerInteractionItem ToWireItem(PlayerInteractionItem item) =>
		new()
		{
			Identity = ToWireIdentity(item.Identity),
			Data = ToWireData(item.Data),
			Contents = [.. item.Children.Select(ToWireItem)],
		};

	private static PlayerInteractionItem FromWireItem(WirePlayerInteractionItem item) =>
		new(
			new ItemIdentity(item.Identity.InstanceId, item.Identity.DefinitionId),
			FromWireData(item.Data),
			[.. item.Contents.Select(FromWireItem)]);

	private static WireComponentState ToWireComponent(ItemComponentState component) =>
		new()
		{
			TypeName = component.TypeName,
			Fields = [.. component.Fields.Select(f => new WireComponentField
			{
				Name = f.Name,
				Kind = (int)f.Kind,
				FloatValue = f.FloatValue,
				IntValue = f.IntValue,
				BoolValue = f.BoolValue,
				StringValue = f.StringValue,
				StringList = [.. f.StringList],
			})],
		};

	private static ItemComponentState FromWireComponent(WireComponentState component) =>
		new(
			component.TypeName,
			[.. component.Fields.Select(f => new ItemComponentField(
				f.Name,
				(ItemComponentFieldKind)f.Kind,
				f.FloatValue,
				f.IntValue,
				f.BoolValue,
				f.StringValue,
				f.StringList))]);

	private static ItemData FromWireData(WireItemData data) =>
		new(
			data.Condition,
			data.Favourited,
			data.SlotIndex,
			[.. data.Liquids.Select(l => new ItemLiquidStack(l.LiquidId, l.Amount))],
			[.. data.Components.Select(FromWireComponent)]);

	private static WirePlayerInteractionHealth ToWireHealth(PlayerInteractionHealth health) =>
		new()
		{
			BloodVolume = health.BloodVolume,
			BloodOxygen = health.BloodOxygen,
			HeartRate = health.HeartRate,
			RespiratoryRate = health.RespiratoryRate,
			BloodPressure = health.BloodPressure,
			BloodVesselSize = health.BloodVesselSize,
			FibrillationProgress = health.FibrillationProgress,
			FibrillationForced = health.FibrillationForced,
			BloodViscosity = health.BloodViscosity,
			Adrenaline = health.Adrenaline,
			CurAdrenaline = health.CurAdrenaline,
			Hunger = health.Hunger,
			Thirst = health.Thirst,
			Stamina = health.Stamina,
			Energy = health.Energy,
			Happiness = health.Happiness,
			WeightOffset = health.WeightOffset,
			BrainHealth = health.BrainHealth,
			Consciousness = health.Consciousness,
			Alive = health.Alive,
			Conscious = health.Conscious,
			Shock = health.Shock,
			SicknessAmount = health.SicknessAmount,
			DesensitizedMult = health.DesensitizedMult,
			CorpsesSeen = health.CorpsesSeen,
			SepticShock = health.SepticShock,
			Disfigured = health.Disfigured,
			EyeGone = health.EyeGone,
			BothEyesGone = health.BothEyesGone,
			RadiationSickness = health.RadiationSickness,
			Caffeinated = health.Caffeinated,
			HearingLoss = health.HearingLoss,
			InternalBleeding = health.InternalBleeding,
			Hemothorax = health.Hemothorax,
			PainShock = health.PainShock,
			TraumaAmount = health.TraumaAmount,
			Wetness = health.Wetness,
			BadSleepAmount = health.BadSleepAmount,
			GoodSleepTime = health.GoodSleepTime,
			SnowAmount = health.SnowAmount,
			Immunity = health.Immunity,
			AntibioticImmunityTime = health.AntibioticImmunityTime,
			TriedRollingLastStand = health.TriedRollingLastStand,
			SuccesfullyRolledLastStand = health.SuccesfullyRolledLastStand,
			LastStandTime = health.LastStandTime,
			Dirtyness = health.Dirtyness,
			BrainGrowSickness = health.BrainGrowSickness,
			UsedNeuralBooster = health.UsedNeuralBooster,
			ClawHealth = health.ClawHealth,
			ClawRegrowTime = health.ClawRegrowTime,
			HasPulmonaryEmbolism = health.HasPulmonaryEmbolism,
			StrokeAmount = health.StrokeAmount,
			BloodPressureChangeFromMedicine = health.BloodPressureChangeFromMedicine,
			VenomTotal = health.VenomTotal,
			VenomCurrent = health.VenomCurrent,
			MaxSpeed = health.MaxSpeed,
			JumpSpeed = health.JumpSpeed,
			TemporarySlowdown = health.TemporarySlowdown,
			MoveForce = health.MoveForce,
			SlowdownAmount = health.SlowdownAmount,
			Temperature = health.Temperature,
			HorrifiedLevel = health.HorrifiedLevel,
			FocusedLevel = health.FocusedLevel,
			EyePanicTime = health.EyePanicTime,
			DisfiguredIndex = health.DisfiguredIndex,
			DisfiguredTimeFullSkin = health.DisfiguredTimeFullSkin,
			EyeTimeHealed = health.EyeTimeHealed,
			OpiateAmount = health.OpiateAmount,
			OpiateTolerance = health.OpiateTolerance,
			OpiateReception = health.OpiateReception,
			AntagonistAmount = health.AntagonistAmount,
			ActualOpiateReception = health.ActualOpiateReception,
			SleepingPillsAmount = health.SleepingPillsAmount,
			AntidepressantsAmount = health.AntidepressantsAmount,
			AntidepressantsCurrentAmount = health.AntidepressantsCurrentAmount,
			MindwipeScriptPresent = health.MindwipeScriptPresent,
			MindwipeScriptActive = health.MindwipeScriptActive,
		};

	private static PlayerInteractionHealth? FromWireHealth(WirePlayerInteractionHealth? health)
	{
		if (health is null)
		{
			return null;
		}

		return new PlayerInteractionHealth
		{
			BloodVolume = health.BloodVolume,
			BloodOxygen = health.BloodOxygen,
			HeartRate = health.HeartRate,
			RespiratoryRate = health.RespiratoryRate,
			BloodPressure = health.BloodPressure,
			BloodVesselSize = health.BloodVesselSize,
			FibrillationProgress = health.FibrillationProgress,
			FibrillationForced = health.FibrillationForced,
			BloodViscosity = health.BloodViscosity,
			Adrenaline = health.Adrenaline,
			CurAdrenaline = health.CurAdrenaline,
			Hunger = health.Hunger,
			Thirst = health.Thirst,
			Stamina = health.Stamina,
			Energy = health.Energy,
			Happiness = health.Happiness,
			WeightOffset = health.WeightOffset,
			BrainHealth = health.BrainHealth,
			Consciousness = health.Consciousness,
			Alive = health.Alive,
			Conscious = health.Conscious,
			Shock = health.Shock,
			SicknessAmount = health.SicknessAmount,
			DesensitizedMult = health.DesensitizedMult,
			CorpsesSeen = health.CorpsesSeen,
			SepticShock = health.SepticShock,
			Disfigured = health.Disfigured,
			EyeGone = health.EyeGone,
			BothEyesGone = health.BothEyesGone,
			RadiationSickness = health.RadiationSickness,
			Caffeinated = health.Caffeinated,
			HearingLoss = health.HearingLoss,
			InternalBleeding = health.InternalBleeding,
			Hemothorax = health.Hemothorax,
			PainShock = health.PainShock,
			TraumaAmount = health.TraumaAmount,
			Wetness = health.Wetness,
			BadSleepAmount = health.BadSleepAmount,
			GoodSleepTime = health.GoodSleepTime,
			SnowAmount = health.SnowAmount,
			Immunity = health.Immunity,
			AntibioticImmunityTime = health.AntibioticImmunityTime,
			TriedRollingLastStand = health.TriedRollingLastStand,
			SuccesfullyRolledLastStand = health.SuccesfullyRolledLastStand,
			LastStandTime = health.LastStandTime,
			Dirtyness = health.Dirtyness,
			BrainGrowSickness = health.BrainGrowSickness,
			UsedNeuralBooster = health.UsedNeuralBooster,
			ClawHealth = health.ClawHealth,
			ClawRegrowTime = health.ClawRegrowTime,
			HasPulmonaryEmbolism = health.HasPulmonaryEmbolism,
			StrokeAmount = health.StrokeAmount,
			BloodPressureChangeFromMedicine = health.BloodPressureChangeFromMedicine,
			VenomTotal = health.VenomTotal,
			VenomCurrent = health.VenomCurrent,
			MaxSpeed = health.MaxSpeed,
			JumpSpeed = health.JumpSpeed,
			TemporarySlowdown = health.TemporarySlowdown,
			MoveForce = health.MoveForce,
			SlowdownAmount = health.SlowdownAmount,
			Temperature = health.Temperature,
			HorrifiedLevel = health.HorrifiedLevel,
			FocusedLevel = health.FocusedLevel,
			EyePanicTime = health.EyePanicTime,
			DisfiguredIndex = health.DisfiguredIndex,
			DisfiguredTimeFullSkin = health.DisfiguredTimeFullSkin,
			EyeTimeHealed = health.EyeTimeHealed,
			OpiateAmount = health.OpiateAmount,
			OpiateTolerance = health.OpiateTolerance,
			OpiateReception = health.OpiateReception,
			AntagonistAmount = health.AntagonistAmount,
			ActualOpiateReception = health.ActualOpiateReception,
			SleepingPillsAmount = health.SleepingPillsAmount,
			AntidepressantsAmount = health.AntidepressantsAmount,
			AntidepressantsCurrentAmount = health.AntidepressantsCurrentAmount,
			MindwipeScriptPresent = health.MindwipeScriptPresent,
			MindwipeScriptActive = health.MindwipeScriptActive,
		};
	}

	public static WirePlayerInteractionLimb ToWireLimb(PlayerInteractionLimb limb) =>
		new()
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
			Components = [.. limb.Components.Select(ToWireComponent)],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};

	public static PlayerInteractionLimb FromWireLimb(WirePlayerInteractionLimb limb) =>
		new()
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
			Components = [.. limb.Components.Select(FromWireComponent)],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};
}
