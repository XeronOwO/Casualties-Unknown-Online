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

	/// <summary>
	/// Indices of the limbs directly connected to this limb in the body graph.
	/// Captured from the live body for host-side tourniquet/amputation effects;
	/// the host does not instantiate Unity bodies, so wire-carried topology is
	/// the only source for connected-limb semantics.
	/// </summary>
	[ProtoMember(23)]
	public List<int> ConnectedLimbIndices { get; set; } = [];

	/// <summary>
	/// Native <c>distanceToHeart</c> for this limb. Tourniquet lower-limb
	/// traversal uses it the same way the game's <c>GetLowerLimbs</c> does.
	/// </summary>
	[ProtoMember(24)]
	public int DistanceToHeart { get; set; }

	/// <summary>
	/// The native <c>Limb.ProstheticInfo.hasProsthetic</c> of the owner's limb
	/// (Item.cs:749-830 installs it through <c>Limb.AttachProsthetic</c>). The
	/// host records it with the limb state; a remote client reattaches it through
	/// the game's own <c>AttachProsthetic(info, isLoad: true)</c>.
	/// </summary>
	[ProtoMember(25)]
	public bool HasProsthetic { get; set; }

	/// <summary>
	/// The prosthetic's native item id (<c>"steelprosthesis"</c>, <c>"peo"</c>, …).
	/// Null when the limb carries none, so an ordinary limb pays nothing on the wire.
	/// </summary>
	[ProtoMember(26)]
	public string? ProstheticItemId { get; set; }

	/// <summary>The prosthetic's <c>forceChange</c> (native <c>ProstheticInfo</c>).</summary>
	[ProtoMember(27)]
	public float ProstheticForceChange { get; set; }

	/// <summary>The prosthetic's <c>armorChange</c> (native <c>ProstheticInfo</c>).</summary>
	[ProtoMember(28)]
	public float ProstheticArmorChange { get; set; }

	/// <summary>The prosthetic's red colour channel (native <c>ProstheticInfo.r</c>).</summary>
	[ProtoMember(29)]
	public float ProstheticR { get; set; }

	/// <summary>The prosthetic's green colour channel (native <c>ProstheticInfo.g</c>).</summary>
	[ProtoMember(30)]
	public float ProstheticG { get; set; }

	/// <summary>The prosthetic's blue colour channel (native <c>ProstheticInfo.b</c>).</summary>
	[ProtoMember(31)]
	public float ProstheticB { get; set; }
}
