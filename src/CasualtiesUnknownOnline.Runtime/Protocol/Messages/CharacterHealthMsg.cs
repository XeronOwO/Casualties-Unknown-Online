using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Full Body physiological/status state (SaveSystem's [JsonProperty] set,
/// Body.cs:3779+) plus the enemy-proximity presentation fields CUO syncs
/// outside the vanilla save set (horrified/focused/eye panic). Alive/Conscious
/// are derived properties (Body.cs:203/213) — reported for diagnostics, never
/// restored.
/// </summary>
[ProtoContract]
public sealed class CharacterHealthMsg
{
	// Circulation / respiration (Body.cs:3867-3916)
	[ProtoMember(1)]
	public float BloodVolume { get; set; }

	[ProtoMember(11)]
	public float BloodOxygen { get; set; }

	[ProtoMember(12)]
	public float HeartRate { get; set; }

	[ProtoMember(13)]
	public float RespiratoryRate { get; set; }

	[ProtoMember(14)]
	public float BloodPressure { get; set; }

	[ProtoMember(15)]
	public float BloodVesselSize { get; set; }

	[ProtoMember(16)]
	public float FibrillationProgress { get; set; }

	[ProtoMember(17)]
	public bool FibrillationForced { get; set; }

	[ProtoMember(18)]
	public float BloodViscosity { get; set; }

	[ProtoMember(19)]
	public float Adrenaline { get; set; }

	[ProtoMember(20)]
	public float CurAdrenaline { get; set; }

	// Nourishment / vitals (Body.cs:3919-3946)
	[ProtoMember(2)]
	public float Hunger { get; set; }

	[ProtoMember(3)]
	public float Thirst { get; set; }

	[ProtoMember(9)]
	public float Stamina { get; set; }

	[ProtoMember(10)]
	public float Energy { get; set; }

	[ProtoMember(21)]
	public float Happiness { get; set; }

	[ProtoMember(22)]
	public float WeightOffset { get; set; }

	// Brain / consciousness / sickness (Body.cs:3949-4168)
	[ProtoMember(4)]
	public float BrainHealth { get; set; }

	[ProtoMember(5)]
	public float Consciousness { get; set; }

	[ProtoMember(7)]
	public bool Alive { get; set; } // derived (brainHealth > 0) — report only

	[ProtoMember(8)]
	public bool Conscious { get; set; } // derived — report only

	[ProtoMember(23)]
	public float Shock { get; set; }

	[ProtoMember(24)]
	public float SicknessAmount { get; set; }

	[ProtoMember(25)]
	public float DesensitizedMult { get; set; }

	[ProtoMember(26)]
	public int CorpsesSeen { get; set; }

	[ProtoMember(27)]
	public float SepticShock { get; set; }

	[ProtoMember(28)]
	public bool Disfigured { get; set; }

	[ProtoMember(29)]
	public bool EyeGone { get; set; }

	[ProtoMember(30)]
	public bool BothEyesGone { get; set; }

	[ProtoMember(31)]
	public float RadiationSickness { get; set; }

	[ProtoMember(32)]
	public float Caffeinated { get; set; }

	[ProtoMember(33)]
	public float HearingLoss { get; set; }

	[ProtoMember(34)]
	public float InternalBleeding { get; set; }

	[ProtoMember(35)]
	public float Hemothorax { get; set; }

	[ProtoMember(36)]
	public float PainShock { get; set; }

	[ProtoMember(37)]
	public float TraumaAmount { get; set; }

	// Environment / recovery / misc (Body.cs:4208-4397)
	[ProtoMember(38)]
	public float Wetness { get; set; }

	[ProtoMember(39)]
	public float BadSleepAmount { get; set; }

	[ProtoMember(40)]
	public float GoodSleepTime { get; set; }

	[ProtoMember(41)]
	public float SnowAmount { get; set; }

	[ProtoMember(42)]
	public float Immunity { get; set; }

	[ProtoMember(43)]
	public float AntibioticImmunityTime { get; set; }

	[ProtoMember(44)]
	public bool TriedRollingLastStand { get; set; }

	[ProtoMember(45)]
	public bool SuccesfullyRolledLastStand { get; set; }

	[ProtoMember(46)]
	public float LastStandTime { get; set; }

	[ProtoMember(47)]
	public float Dirtyness { get; set; }

	[ProtoMember(48)]
	public float BrainGrowSickness { get; set; }

	[ProtoMember(49)]
	public bool UsedNeuralBooster { get; set; }

	[ProtoMember(50)]
	public float ClawHealth { get; set; }

	[ProtoMember(51)]
	public float ClawRegrowTime { get; set; }

	[ProtoMember(52)]
	public bool HasPulmonaryEmbolism { get; set; }

	[ProtoMember(53)]
	public float StrokeAmount { get; set; }

	[ProtoMember(54)]
	public float BloodPressureChangeFromMedicine { get; set; }

	[ProtoMember(55)]
	public float VenomTotal { get; set; }

	[ProtoMember(56)]
	public float VenomCurrent { get; set; }

	// Movement parameters (SaveSystem saves them; drugs/diseases alter them)
	[ProtoMember(57)]
	public float MaxSpeed { get; set; }

	[ProtoMember(58)]
	public float JumpSpeed { get; set; }

	[ProtoMember(59)]
	public float TemporarySlowdown { get; set; }

	[ProtoMember(60)]
	public float MoveForce { get; set; }

	[ProtoMember(61)]
	public float SlowdownAmount { get; set; }

	[ProtoMember(6)]
	public float Temperature { get; set; }

	// Enemy-proximity presentation fields (not in the vanilla SaveSystem set —
	// synced by EnemyEffectMsg and the 1 Hz snapshot fallback).
	[ProtoMember(62)]
	public float HorrifiedLevel { get; set; }

	[ProtoMember(63)]
	public float FocusedLevel { get; set; }

	[ProtoMember(64)]
	public float EyePanicTime { get; set; }

	// Remote-clone FacialExpression presentation latches (not part of the
	// vanilla SaveSystem set — the owner-side face state must reach the peer's
	// render clone, including the random disfigurement head index and the
	// long-run heal presentation timers).
	[ProtoMember(65)]
	public int DisfiguredIndex { get; set; }

	[ProtoMember(66)]
	public float DisfiguredTimeFullSkin { get; set; }

	[ProtoMember(67)]
	public float EyeTimeHealed { get; set; }

	// Painkiller component state (Painkillers.cs, [Saveable] — the component
	// drives limb pain reduction, opiate happiness, withdrawal and overdose
	// presentation on the owner's simulated body, so it must ride the character
	// snapshot for cross-player opiate use and reconnect restore).
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

	// Drinkable-medicine component state (SleepingPills/Antidepressants/
	// MindwipeScript are [Saveable] components on the simulated Body — Mapster
	// cannot see them, so the character snapshot carries the same fields the
	// cross-player drinkable-medicine slice needs to restore/evolve them).
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

	/// <summary>
	/// The owner's actual head/mouth sprite state (closed, half-open, or open)
	/// as chosen by the owner's live <c>FacialExpression.Update</c>. The remote
	/// clone replays this exact state rather than deriving its head sprite from
	/// clone-local slot contents / limb latches / eat-time, which can disagree
	/// with the owner's own view after falls or other pose transitions.
	/// </summary>
	[ProtoMember(78)]
	public HeadMouthState HeadMouth { get; set; }

	/// <summary>
	/// The owner's live eating/drinking timer. It is the one mouth trigger that
	/// is not otherwise represented in the character snapshot, and it lets the
	/// receiving side recompute <see cref="HeadMouth"/> after slot/limb events
	/// without waiting for the next full snapshot.
	/// </summary>
	[ProtoMember(79)]
	public float EatTime { get; set; }

	/// <summary>
	/// The owner's computed leg-speed multiplier (Body.legSpeedMult, 0-1).
	/// HandleVisuals uses it as the weakness/slouch input for the CrouchAmount
	/// animator parameter; the remote render clone cannot recompute it because
	/// its limbs are frozen, so the 1 Hz snapshot carries the owner's actual
	/// value. Severe sleepiness, low consciousness/stamina, hunger, and other
	/// movement-debility facts all flow through this one pose input.
	/// </summary>
	[ProtoMember(80)]
	public float LegSpeedMult { get; set; }
}
