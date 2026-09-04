using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Read-only detailed medical view of one remote player, projected from the
/// full character-data stream. The compact <see cref="RemoteVitalsSnapshot"/>
/// remains the nameplate projection; this snapshot feeds the CUO medical panel
/// and deliberately copies every physiological field plus limb facts so the
/// panel never shares mutable wire objects with the protocol layer.
/// </summary>
public sealed class RemoteMedicalSnapshot
{
	private RemoteMedicalSnapshot(
		float brainHealth,
		float consciousness,
		bool alive,
		bool conscious,
		float hunger,
		float thirst,
		float stamina,
		float energy,
		float happiness,
		float temperature,
		float bloodVolume,
		float bloodOxygen,
		float heartRate,
		float respiratoryRate,
		float bloodPressure,
		float bloodVesselSize,
		float fibrillationProgress,
		bool fibrillationForced,
		float adrenaline,
		float curAdrenaline,
		float shock,
		float sicknessAmount,
		float desensitizedMult,
		int corpsesSeen,
		float septicShock,
		bool disfigured,
		bool eyeGone,
		bool bothEyesGone,
		float radiationSickness,
		float caffeinated,
		float hearingLoss,
		float internalBleeding,
		float hemothorax,
		float painShock,
		float traumaAmount,
		float wetness,
		float badSleepAmount,
		float goodSleepTime,
		float snowAmount,
		float immunity,
		float dirtyness,
		float brainGrowSickness,
		bool usedNeuralBooster,
		float clawHealth,
		float clawRegrowTime,
		bool hasPulmonaryEmbolism,
		float strokeAmount,
		float bloodPressureChangeFromMedicine,
		float venomTotal,
		float venomCurrent,
		float maxSpeed,
		float jumpSpeed,
		float temporarySlowdown,
		float moveForce,
		float slowdownAmount,
		float weightOffset,
		float opiateAmount,
		float opiateTolerance,
		float opiateReception,
		float antagonistAmount,
		float actualOpiateReception,
		float sleepingPillsAmount,
		float antidepressantsAmount,
		float antidepressantsCurrentAmount,
		bool mindwipeScriptPresent,
		bool mindwipeScriptActive,
		bool triedRollingLastStand,
		bool succesfullyRolledLastStand,
		float lastStandTime,
		float horrifiedLevel,
		float focusedLevel,
		float eyePanicTime,
		int disfiguredIndex,
		float disfiguredTimeFullSkin,
		float eyeTimeHealed,
		IReadOnlyList<RemoteLimbSnapshot> limbs)
	{
		BrainHealth = brainHealth;
		Consciousness = consciousness;
		Alive = alive;
		Conscious = conscious;
		Hunger = hunger;
		Thirst = thirst;
		Stamina = stamina;
		Energy = energy;
		Happiness = happiness;
		Temperature = temperature;
		BloodVolume = bloodVolume;
		BloodOxygen = bloodOxygen;
		HeartRate = heartRate;
		RespiratoryRate = respiratoryRate;
		BloodPressure = bloodPressure;
		BloodVesselSize = bloodVesselSize;
		FibrillationProgress = fibrillationProgress;
		FibrillationForced = fibrillationForced;
		Adrenaline = adrenaline;
		CurAdrenaline = curAdrenaline;
		Shock = shock;
		SicknessAmount = sicknessAmount;
		DesensitizedMult = desensitizedMult;
		CorpsesSeen = corpsesSeen;
		SepticShock = septicShock;
		Disfigured = disfigured;
		EyeGone = eyeGone;
		BothEyesGone = bothEyesGone;
		RadiationSickness = radiationSickness;
		Caffeinated = caffeinated;
		HearingLoss = hearingLoss;
		InternalBleeding = internalBleeding;
		Hemothorax = hemothorax;
		PainShock = painShock;
		TraumaAmount = traumaAmount;
		Wetness = wetness;
		BadSleepAmount = badSleepAmount;
		GoodSleepTime = goodSleepTime;
		SnowAmount = snowAmount;
		Immunity = immunity;
		Dirtyness = dirtyness;
		BrainGrowSickness = brainGrowSickness;
		UsedNeuralBooster = usedNeuralBooster;
		ClawHealth = clawHealth;
		ClawRegrowTime = clawRegrowTime;
		HasPulmonaryEmbolism = hasPulmonaryEmbolism;
		StrokeAmount = strokeAmount;
		BloodPressureChangeFromMedicine = bloodPressureChangeFromMedicine;
		VenomTotal = venomTotal;
		VenomCurrent = venomCurrent;
		MaxSpeed = maxSpeed;
		JumpSpeed = jumpSpeed;
		TemporarySlowdown = temporarySlowdown;
		MoveForce = moveForce;
		SlowdownAmount = slowdownAmount;
		WeightOffset = weightOffset;
		OpiateAmount = opiateAmount;
		OpiateTolerance = opiateTolerance;
		OpiateReception = opiateReception;
		AntagonistAmount = antagonistAmount;
		ActualOpiateReception = actualOpiateReception;
		SleepingPillsAmount = sleepingPillsAmount;
		AntidepressantsAmount = antidepressantsAmount;
		AntidepressantsCurrentAmount = antidepressantsCurrentAmount;
		MindwipeScriptPresent = mindwipeScriptPresent;
		MindwipeScriptActive = mindwipeScriptActive;
		TriedRollingLastStand = triedRollingLastStand;
		SuccesfullyRolledLastStand = succesfullyRolledLastStand;
		LastStandTime = lastStandTime;
		HorrifiedLevel = horrifiedLevel;
		FocusedLevel = focusedLevel;
		EyePanicTime = eyePanicTime;
		DisfiguredIndex = disfiguredIndex;
		DisfiguredTimeFullSkin = disfiguredTimeFullSkin;
		EyeTimeHealed = eyeTimeHealed;
		Limbs = limbs;
	}

	public float BrainHealth { get; }

	public float Consciousness { get; }

	public bool Alive { get; }

	public bool Conscious { get; }

	public float Hunger { get; }

	public float Thirst { get; }

	public float Stamina { get; }

	public float Energy { get; }

	public float Happiness { get; }

	public float Temperature { get; }

	public float BloodVolume { get; }

	public float BloodOxygen { get; }

	public float HeartRate { get; }

	public float RespiratoryRate { get; }

	public float BloodPressure { get; }

	public float BloodVesselSize { get; }

	public float FibrillationProgress { get; }

	public bool FibrillationForced { get; }

	public float Adrenaline { get; }

	public float CurAdrenaline { get; }

	public float Shock { get; }

	public float SicknessAmount { get; }

	public float DesensitizedMult { get; }

	public int CorpsesSeen { get; }

	public float SepticShock { get; }

	public bool Disfigured { get; }

	public bool EyeGone { get; }

	public bool BothEyesGone { get; }

	public float RadiationSickness { get; }

	public float Caffeinated { get; }

	public float HearingLoss { get; }

	public float InternalBleeding { get; }

	public float Hemothorax { get; }

	public float PainShock { get; }

	public float TraumaAmount { get; }

	public float Wetness { get; }

	public float BadSleepAmount { get; }

	public float GoodSleepTime { get; }

	public float SnowAmount { get; }

	public float Immunity { get; }

	public float Dirtyness { get; }

	public float BrainGrowSickness { get; }

	public bool UsedNeuralBooster { get; }

	public float ClawHealth { get; }

	public float ClawRegrowTime { get; }

	public bool HasPulmonaryEmbolism { get; }

	public float StrokeAmount { get; }

	public float BloodPressureChangeFromMedicine { get; }

	public float VenomTotal { get; }

	public float VenomCurrent { get; }

	public float MaxSpeed { get; }

	public float JumpSpeed { get; }

	public float TemporarySlowdown { get; }

	public float MoveForce { get; }

	public float SlowdownAmount { get; }

	public float WeightOffset { get; }

	public float OpiateAmount { get; }

	public float OpiateTolerance { get; }

	public float OpiateReception { get; }

	public float AntagonistAmount { get; }

	public float ActualOpiateReception { get; }

	public float SleepingPillsAmount { get; }

	public float AntidepressantsAmount { get; }

	public float AntidepressantsCurrentAmount { get; }

	public bool MindwipeScriptPresent { get; }

	public bool MindwipeScriptActive { get; }

	public bool TriedRollingLastStand { get; }

	public bool SuccesfullyRolledLastStand { get; }

	public float LastStandTime { get; }

	public float HorrifiedLevel { get; }

	public float FocusedLevel { get; }

	public float EyePanicTime { get; }

	public int DisfiguredIndex { get; }

	public float DisfiguredTimeFullSkin { get; }

	public float EyeTimeHealed { get; }

	public IReadOnlyList<RemoteLimbSnapshot> Limbs { get; }

	/// <summary>
	/// Project a full character snapshot into the medical view. A null health
	/// block means there is no medical data yet; callers should treat that as
	/// "unknown" rather than showing a zeroed body.
	/// </summary>
	public static RemoteMedicalSnapshot? From(CharacterDataMsg? data)
	{
		var health = data?.Health;
		if (health is null)
		{
			return null;
		}

		return new RemoteMedicalSnapshot(
			health.BrainHealth,
			health.Consciousness,
			health.Alive,
			health.Conscious,
			health.Hunger,
			health.Thirst,
			health.Stamina,
			health.Energy,
			health.Happiness,
			health.Temperature,
			health.BloodVolume,
			health.BloodOxygen,
			health.HeartRate,
			health.RespiratoryRate,
			health.BloodPressure,
			health.BloodVesselSize,
			health.FibrillationProgress,
			health.FibrillationForced,
			health.Adrenaline,
			health.CurAdrenaline,
			health.Shock,
			health.SicknessAmount,
			health.DesensitizedMult,
			health.CorpsesSeen,
			health.SepticShock,
			health.Disfigured,
			health.EyeGone,
			health.BothEyesGone,
			health.RadiationSickness,
			health.Caffeinated,
			health.HearingLoss,
			health.InternalBleeding,
			health.Hemothorax,
			health.PainShock,
			health.TraumaAmount,
			health.Wetness,
			health.BadSleepAmount,
			health.GoodSleepTime,
			health.SnowAmount,
			health.Immunity,
			health.Dirtyness,
			health.BrainGrowSickness,
			health.UsedNeuralBooster,
			health.ClawHealth,
			health.ClawRegrowTime,
			health.HasPulmonaryEmbolism,
			health.StrokeAmount,
			health.BloodPressureChangeFromMedicine,
			health.VenomTotal,
			health.VenomCurrent,
			health.MaxSpeed,
			health.JumpSpeed,
			health.TemporarySlowdown,
			health.MoveForce,
			health.SlowdownAmount,
			health.WeightOffset,
			health.OpiateAmount,
			health.OpiateTolerance,
			health.OpiateReception,
			health.AntagonistAmount,
			health.ActualOpiateReception,
			health.SleepingPillsAmount,
			health.AntidepressantsAmount,
			health.AntidepressantsCurrentAmount,
			health.MindwipeScriptPresent,
			health.MindwipeScriptActive,
			health.TriedRollingLastStand,
			health.SuccesfullyRolledLastStand,
			health.LastStandTime,
			health.HorrifiedLevel,
			health.FocusedLevel,
			health.EyePanicTime,
			health.DisfiguredIndex,
			health.DisfiguredTimeFullSkin,
			health.EyeTimeHealed,
			[.. data!.Limbs.Select(RemoteLimbSnapshot.From)]);
	}
}
