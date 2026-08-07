using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Full character snapshot for session-scoped save/restore (character-data-plan):
/// the guest reports it periodically (1-2 Hz), the host keeps the latest per
/// SteamID and hands it back when the same player reconnects, so the guest can
/// rebuild its character after the game spawned a fresh default one.
/// One message serves both directions (report and restore).
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
	public int HandSlot { get; set; } = -1; // -1 = don't touch (default on report-less restores)
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

[ProtoContract]
public sealed class CharacterHealthMsg
{
	[ProtoMember(1)]
	public float BloodVolume { get; set; }

	[ProtoMember(2)]
	public float Hunger { get; set; }

	[ProtoMember(3)]
	public float Thirst { get; set; }

	[ProtoMember(4)]
	public float BrainHealth { get; set; }

	[ProtoMember(5)]
	public float Consciousness { get; set; }

	[ProtoMember(6)]
	public float Temperature { get; set; }

	[ProtoMember(7)]
	public bool Alive { get; set; }

	[ProtoMember(8)]
	public bool Conscious { get; set; }
}

/// <summary>
/// One limb's persistent state. A limb has no single HP field — health is
/// skinHealth + muscleHealth (Limb.cs:657/661); bones/infection/bleeding are
/// the rest of what a restore must re-apply (SaveSystem's [JsonProperty] set).
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
