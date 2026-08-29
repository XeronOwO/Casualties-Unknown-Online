namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel-shaped post-interaction body health snapshot. This is not a terminal
/// kernel fact and is not reduced into the player table; it rides a
/// cross-player interaction result event so the receiving Game Adapter can
/// apply the exact host-authoritative post-effect body state immediately.
/// </summary>
public sealed record PlayerInteractionHealth
{
	public float BloodVolume { get; init; }

	public float BloodOxygen { get; init; }

	public float HeartRate { get; init; }

	public float RespiratoryRate { get; init; }

	public float BloodPressure { get; init; }

	public float BloodVesselSize { get; init; }

	public float FibrillationProgress { get; init; }

	public bool FibrillationForced { get; init; }

	public float BloodViscosity { get; init; }

	public float Adrenaline { get; init; }

	public float CurAdrenaline { get; init; }

	public float Hunger { get; init; }

	public float Thirst { get; init; }

	public float Stamina { get; init; }

	public float Energy { get; init; }

	public float Happiness { get; init; }

	public float WeightOffset { get; init; }

	public float BrainHealth { get; init; }

	public float Consciousness { get; init; }

	public bool Alive { get; init; }

	public bool Conscious { get; init; }

	public float Shock { get; init; }

	public float SicknessAmount { get; init; }

	public float DesensitizedMult { get; init; }

	public int CorpsesSeen { get; init; }

	public float SepticShock { get; init; }

	public bool Disfigured { get; init; }

	public bool EyeGone { get; init; }

	public bool BothEyesGone { get; init; }

	public float RadiationSickness { get; init; }

	public float Caffeinated { get; init; }

	public float HearingLoss { get; init; }

	public float InternalBleeding { get; init; }

	public float Hemothorax { get; init; }

	public float PainShock { get; init; }

	public float TraumaAmount { get; init; }

	public float Wetness { get; init; }

	public float BadSleepAmount { get; init; }

	public float GoodSleepTime { get; init; }

	public float SnowAmount { get; init; }

	public float Immunity { get; init; }

	public float AntibioticImmunityTime { get; init; }

	public bool TriedRollingLastStand { get; init; }

	public bool SuccesfullyRolledLastStand { get; init; }

	public float LastStandTime { get; init; }

	public float Dirtyness { get; init; }

	public float BrainGrowSickness { get; init; }

	public bool UsedNeuralBooster { get; init; }

	public float ClawHealth { get; init; }

	public float ClawRegrowTime { get; init; }

	public bool HasPulmonaryEmbolism { get; init; }

	public float StrokeAmount { get; init; }

	public float BloodPressureChangeFromMedicine { get; init; }

	public float VenomTotal { get; init; }

	public float VenomCurrent { get; init; }

	public float MaxSpeed { get; init; }

	public float JumpSpeed { get; init; }

	public float TemporarySlowdown { get; init; }

	public float MoveForce { get; init; }

	public float SlowdownAmount { get; init; }

	public float Temperature { get; init; }

	public float HorrifiedLevel { get; init; }

	public float FocusedLevel { get; init; }

	public float EyePanicTime { get; init; }

	public int DisfiguredIndex { get; init; }

	public float DisfiguredTimeFullSkin { get; init; }

	public float EyeTimeHealed { get; init; }

	public float OpiateAmount { get; init; }

	public float OpiateTolerance { get; init; }

	public float OpiateReception { get; init; }

	public float AntagonistAmount { get; init; }

	public float ActualOpiateReception { get; init; }

	public float SleepingPillsAmount { get; init; }

	public float AntidepressantsAmount { get; init; }

	public float AntidepressantsCurrentAmount { get; init; }

	public bool MindwipeScriptPresent { get; init; }

	public bool MindwipeScriptActive { get; init; }
}
