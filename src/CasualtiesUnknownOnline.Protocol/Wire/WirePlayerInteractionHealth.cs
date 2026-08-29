using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of the post-interaction body health snapshot carried by a
/// player-interaction result event.
/// </summary>
[ProtoContract]
public sealed class WirePlayerInteractionHealth
{
	[ProtoMember(1)]
	public float BloodVolume { get; set; }

	[ProtoMember(2)]
	public float BloodOxygen { get; set; }

	[ProtoMember(3)]
	public float HeartRate { get; set; }

	[ProtoMember(4)]
	public float RespiratoryRate { get; set; }

	[ProtoMember(5)]
	public float BloodPressure { get; set; }

	[ProtoMember(6)]
	public float BloodVesselSize { get; set; }

	[ProtoMember(7)]
	public float FibrillationProgress { get; set; }

	[ProtoMember(8)]
	public bool FibrillationForced { get; set; }

	[ProtoMember(9)]
	public float BloodViscosity { get; set; }

	[ProtoMember(10)]
	public float Adrenaline { get; set; }

	[ProtoMember(11)]
	public float CurAdrenaline { get; set; }

	[ProtoMember(12)]
	public float Hunger { get; set; }

	[ProtoMember(13)]
	public float Thirst { get; set; }

	[ProtoMember(14)]
	public float Stamina { get; set; }

	[ProtoMember(15)]
	public float Energy { get; set; }

	[ProtoMember(16)]
	public float Happiness { get; set; }

	[ProtoMember(17)]
	public float WeightOffset { get; set; }

	[ProtoMember(18)]
	public float BrainHealth { get; set; }

	[ProtoMember(19)]
	public float Consciousness { get; set; }

	[ProtoMember(20)]
	public bool Alive { get; set; }

	[ProtoMember(21)]
	public bool Conscious { get; set; }

	[ProtoMember(22)]
	public float Shock { get; set; }

	[ProtoMember(23)]
	public float SicknessAmount { get; set; }

	[ProtoMember(24)]
	public float DesensitizedMult { get; set; }

	[ProtoMember(25)]
	public int CorpsesSeen { get; set; }

	[ProtoMember(26)]
	public float SepticShock { get; set; }

	[ProtoMember(27)]
	public bool Disfigured { get; set; }

	[ProtoMember(28)]
	public bool EyeGone { get; set; }

	[ProtoMember(29)]
	public bool BothEyesGone { get; set; }

	[ProtoMember(30)]
	public float RadiationSickness { get; set; }

	[ProtoMember(31)]
	public float Caffeinated { get; set; }

	[ProtoMember(32)]
	public float HearingLoss { get; set; }

	[ProtoMember(33)]
	public float InternalBleeding { get; set; }

	[ProtoMember(34)]
	public float Hemothorax { get; set; }

	[ProtoMember(35)]
	public float PainShock { get; set; }

	[ProtoMember(36)]
	public float TraumaAmount { get; set; }

	[ProtoMember(37)]
	public float Wetness { get; set; }

	[ProtoMember(38)]
	public float BadSleepAmount { get; set; }

	[ProtoMember(39)]
	public float GoodSleepTime { get; set; }

	[ProtoMember(40)]
	public float SnowAmount { get; set; }

	[ProtoMember(41)]
	public float Immunity { get; set; }

	[ProtoMember(42)]
	public float AntibioticImmunityTime { get; set; }

	[ProtoMember(43)]
	public bool TriedRollingLastStand { get; set; }

	[ProtoMember(44)]
	public bool SuccesfullyRolledLastStand { get; set; }

	[ProtoMember(45)]
	public float LastStandTime { get; set; }

	[ProtoMember(46)]
	public float Dirtyness { get; set; }

	[ProtoMember(47)]
	public float BrainGrowSickness { get; set; }

	[ProtoMember(48)]
	public bool UsedNeuralBooster { get; set; }

	[ProtoMember(49)]
	public float ClawHealth { get; set; }

	[ProtoMember(50)]
	public float ClawRegrowTime { get; set; }

	[ProtoMember(51)]
	public bool HasPulmonaryEmbolism { get; set; }

	[ProtoMember(52)]
	public float StrokeAmount { get; set; }

	[ProtoMember(53)]
	public float BloodPressureChangeFromMedicine { get; set; }

	[ProtoMember(54)]
	public float VenomTotal { get; set; }

	[ProtoMember(55)]
	public float VenomCurrent { get; set; }

	[ProtoMember(56)]
	public float MaxSpeed { get; set; }

	[ProtoMember(57)]
	public float JumpSpeed { get; set; }

	[ProtoMember(58)]
	public float TemporarySlowdown { get; set; }

	[ProtoMember(59)]
	public float MoveForce { get; set; }

	[ProtoMember(60)]
	public float SlowdownAmount { get; set; }

	[ProtoMember(61)]
	public float Temperature { get; set; }

	[ProtoMember(62)]
	public float HorrifiedLevel { get; set; }

	[ProtoMember(63)]
	public float FocusedLevel { get; set; }

	[ProtoMember(64)]
	public float EyePanicTime { get; set; }

	[ProtoMember(65)]
	public int DisfiguredIndex { get; set; }

	[ProtoMember(66)]
	public float DisfiguredTimeFullSkin { get; set; }

	[ProtoMember(67)]
	public float EyeTimeHealed { get; set; }

	[ProtoMember(68)]
	public float OpiateAmount { get; set; }

	[ProtoMember(69)]
	public float OpiateTolerance { get; set; }

	[ProtoMember(70)]
	public float OpiateReception { get; set; }

	[ProtoMember(71)]
	public float AntagonistAmount { get; set; }

	[ProtoMember(72)]
	public float ActualOpiateReception { get; set; }

	[ProtoMember(73)]
	public float SleepingPillsAmount { get; set; }

	[ProtoMember(74)]
	public float AntidepressantsAmount { get; set; }

	[ProtoMember(75)]
	public float AntidepressantsCurrentAmount { get; set; }

	[ProtoMember(76)]
	public bool MindwipeScriptPresent { get; set; }

	[ProtoMember(77)]
	public bool MindwipeScriptActive { get; set; }
}
