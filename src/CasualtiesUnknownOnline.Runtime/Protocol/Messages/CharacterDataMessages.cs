using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Full character snapshot for session-scoped save/restore (character-data-plan):
/// the guest reports it periodically (1-2 Hz), the host keeps the latest per
/// SteamID and hands it back when the same player reconnects, so the guest can
/// rebuild its character after the game spawned a fresh default one.
/// One message serves both directions (report and restore).
/// The field set mirrors the game's own save system (SaveSystem's [JsonProperty]
/// reflection over Body and Limb, Body.cs:3779+ / Limb.cs:656+) so a restore is
/// complete — deliberately no piecemeal additions later.
/// </summary>
[ProtoContract]
public sealed class CharacterDataMsg
{
	[ProtoMember(1)]
	public CharacterSkillsMsg? Skills { get; set; }

	[ProtoMember(2)]
	public CharacterHealthMsg? Health { get; set; }

	[ProtoMember(3)]
	public List<CharacterLimbMsg> Limbs { get; set; } = [];

	[ProtoMember(4)]
	public List<CharacterItemMsg> Items { get; set; } = [];

	[ProtoMember(5)]
	public int HandSlot { get; set; } = -1; // -1 = don't touch
}

[ProtoContract]
public sealed class CharacterSkillsMsg
{
	[ProtoMember(1)]
	public int Strength { get; set; }

	[ProtoMember(2)]
	public int Resistance { get; set; }

	[ProtoMember(3)]
	public int Intelligence { get; set; }

	[ProtoMember(4)]
	public float ExpStrength { get; set; }

	[ProtoMember(5)]
	public float ExpResistance { get; set; }

	[ProtoMember(6)]
	public float ExpIntelligence { get; set; }
}

/// <summary>
/// Full Body physiological/status state (SaveSystem's [JsonProperty] set,
/// Body.cs:3779+). Alive/Conscious are derived properties (Body.cs:203/213) —
/// reported for diagnostics, never restored.
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
}

/// <summary>
/// One limb's persistent state (SaveSystem's [JsonProperty] set, Limb.cs:656-800).
/// </summary>
[ProtoContract]
public sealed class CharacterLimbMsg
{
	[ProtoMember(1)]
	public int Index { get; set; } // index into Body.limbs

	[ProtoMember(2)]
	public float SkinHealth { get; set; }

	[ProtoMember(3)]
	public float MuscleHealth { get; set; }

	[ProtoMember(4)]
	public bool Broken { get; set; }

	[ProtoMember(5)]
	public bool Dislocated { get; set; }

	[ProtoMember(6)]
	public bool Splinted { get; set; }

	[ProtoMember(7)]
	public bool Infected { get; set; }

	[ProtoMember(8)]
	public float InfectionAmount { get; set; }

	[ProtoMember(9)]
	public float BleedAmount { get; set; }

	[ProtoMember(10)]
	public float DisinfectionTime { get; set; }

	[ProtoMember(11)]
	public float Pain { get; set; }

	[ProtoMember(12)]
	public float DislocationTimer { get; set; }

	[ProtoMember(13)]
	public float BoneHealTimer { get; set; }

	[ProtoMember(14)]
	public bool BlockedBleeding { get; set; }

	[ProtoMember(15)]
	public int Shrapnel { get; set; }

	[ProtoMember(16)]
	public float FurBloodAmount { get; set; }

	[ProtoMember(17)]
	public float BandageSlowAmount { get; set; }

	[ProtoMember(18)]
	public float SkinHealAmount { get; set; }

	[ProtoMember(19)]
	public bool Dismembered { get; set; }
}

[ProtoContract]
public sealed class CharacterItemMsg
{
	[ProtoMember(1)]
	public string ItemId { get; set; } = ""; // definition id (ItemInfo.GlobalItems key)

	[ProtoMember(2)]
	public float Condition { get; set; }

	[ProtoMember(3)]
	public int SlotIndex { get; set; } // index into Body.slots
}
