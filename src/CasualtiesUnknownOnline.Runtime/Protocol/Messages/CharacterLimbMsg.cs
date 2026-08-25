using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

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

	/// <summary>
	/// The limb's dynamic <c>[Saveable]</c> component states (SplintLimb,
	/// TourniquetScript, ChilledLimb). Same wire shape as item component state;
	/// the Game Adapter owns the game-type capture/apply side.
	/// </summary>
	[ProtoMember(20)]
	public List<ComponentStateMsg> Components { get; set; } = [];

	/// <summary>
	/// Whether this limb is the body's head. Not part of the vanilla save set,
	/// but needed by host-authoritative cross-player limb tools to mirror the
	/// native "not on head" eligibility checks.
	/// </summary>
	[ProtoMember(21)]
	public bool IsHead { get; set; }

	/// <summary>
	/// Whether this limb is vital (torso/central). Not part of the vanilla save
	/// set, but needed by host-authoritative cross-player limb tools to mirror
	/// the native "not on vital" eligibility checks.
	/// </summary>
	[ProtoMember(22)]
	public bool IsVital { get; set; }
}
