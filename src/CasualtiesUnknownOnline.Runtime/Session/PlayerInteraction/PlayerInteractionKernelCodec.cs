using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure conversions between kernel-shaped player-interaction result payloads and
/// the Runtime presentation messages used by the Game Adapter. The kernel never
/// references these Runtime DTOs; this codec is the projection boundary.
/// </summary>
public static class PlayerInteractionKernelCodec
{
	public static PlayerInteractionItem FromCharacterItem(CharacterItemMsg item) =>
		new(
			new ItemIdentity(item.InstanceId, item.ItemId),
			ItemKernelCodec.ToKernelData(item));

	public static CharacterItemMsg ToCharacterItem(PlayerInteractionItem item) =>
		ToCharacterItem(item.Identity, item.Data);

	private static CharacterItemMsg ToCharacterItem(ItemIdentity identity, ItemData data) =>
		new()
		{
			InstanceId = identity.InstanceId,
			ItemId = identity.DefinitionId,
			Condition = data.Condition,
			Favourited = data.Favourited,
			SlotIndex = data.SlotIndex,
			Liquids = [.. data.Liquids.Select(l => new LiquidStackMsg { LiquidId = l.LiquidId, Amount = l.Amount })],
			Components = [.. data.Components.Select(ToComponentMessage)],
		};

	public static PlayerInteractionHealth FromCharacterHealth(CharacterHealthMsg health) =>
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

	public static CharacterHealthMsg ToCharacterHealth(PlayerInteractionHealth health) =>
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

	public static PlayerInteractionLimb FromCharacterLimb(CharacterLimbMsg limb) =>
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
			Components = [.. limb.Components.Select(ToKernelComponent)],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};

	public static CharacterLimbMsg ToCharacterLimb(PlayerInteractionLimb limb) =>
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
			Components = [.. limb.Components.Select(ToComponentMessage)],
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};

	public static PlayerInteractionTimedLimbEffect FromTimedLimbEffect(TimedLimbEffectMsg effect) =>
		new(effect.LimbIndex, effect.DurationSeconds, effect.BleedPerSecond);

	public static TimedLimbEffectMsg ToTimedLimbEffect(PlayerInteractionTimedLimbEffect effect) =>
		new()
		{
			LimbIndex = effect.LimbIndex,
			DurationSeconds = effect.DurationSeconds,
			BleedPerSecond = effect.BleedPerSecond,
		};

	public static PlayerInteractionTimedBodyEffect FromTimedBodyEffect(TimedBodyEffectMsg effect) =>
		new(effect.EffectId, effect.DurationSeconds, effect.DoseMl);

	public static TimedBodyEffectMsg ToTimedBodyEffect(PlayerInteractionTimedBodyEffect effect) =>
		new()
		{
			EffectId = effect.EffectId,
			DurationSeconds = effect.DurationSeconds,
			DoseMl = effect.DoseMl,
		};

	public static PlayerInventoryTransferMsg ToTransferMessage(PlayerInventoryTransferEvent e) =>
		new()
		{
			FromSteamId = e.FromSteamId,
			ToSteamId = e.ToSteamId,
			Item = ToCharacterItem(e.Item),
		};

	public static PlayerHealResultMsg ToHealMessage(PlayerHealResultEvent e) =>
		new()
		{
			HealerSteamId = e.HealerSteamId,
			TargetSteamId = e.TargetSteamId,
			ItemInstanceId = e.ItemInstanceId,
			ItemDestroyed = e.ItemDestroyed,
			ItemConditionAfter = e.ItemConditionAfter,
			HealedLimbIndex = e.HealedLimbIndex,
			Health = e.Health is null ? null : ToCharacterHealth(e.Health),
			Limbs = [.. e.Limbs.Select(ToCharacterLimb)],
		};

	public static PlayerItemUseResultMsg ToUseMessage(PlayerItemUseResultEvent e) =>
		new()
		{
			UserSteamId = e.UserSteamId,
			TargetSteamId = e.TargetSteamId,
			ItemInstanceId = e.ItemInstanceId,
			ItemDestroyed = e.ItemDestroyed,
			ItemAfter = e.ItemAfter is null ? null : ToCharacterItem(e.ItemAfter),
			WornItem = e.WornItem is null ? null : ToCharacterItem(e.WornItem),
			Health = e.Health is null ? null : ToCharacterHealth(e.Health),
			Limbs = [.. e.Limbs.Select(ToCharacterLimb)],
			TimedEffects = [.. e.TimedEffects.Select(ToTimedLimbEffect)],
			TimedBodyEffects = [.. e.TimedBodyEffects.Select(ToTimedBodyEffect)],
		};

	private static ComponentStateMsg ToComponentMessage(ItemComponentState component) =>
		new()
		{
			TypeName = component.TypeName,
			Fields = [.. component.Fields.Select(f => new ComponentFieldMsg
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

	private static ItemComponentState ToKernelComponent(ComponentStateMsg component) =>
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
}
