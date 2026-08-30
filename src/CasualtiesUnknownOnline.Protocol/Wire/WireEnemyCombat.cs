using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire payload for the three typed enemy-combat result commands/events (bite,
/// lunge, proximity effect). One composite keeps the event/command DTO from
/// adding many parallel scalar members to <see cref="WireEvent"/> or
/// <see cref="WireCommand"/>; the kind discriminator selects which fields are
/// meaningful.
/// </summary>
[ProtoContract]
public sealed class WireEnemyCombat
{
	[ProtoMember(1)]
	public ulong VictimSteamId { get; set; }

	/// <summary>The proximity effect kind (1-based; only meaningful for enemy effect commands/events).</summary>
	[ProtoMember(2)]
	public int EffectKind { get; set; }

	/// <summary>The bitten/lunged limb snapshot (only meaningful for bite/lunge).</summary>
	[ProtoMember(3)]
	public WirePlayerInteractionLimb? Limb { get; set; }

	[ProtoMember(4)]
	public float VenomTotal { get; set; }

	[ProtoMember(5)]
	public float Adrenaline { get; set; }

	[ProtoMember(6)]
	public float Happiness { get; set; }

	[ProtoMember(7)]
	public float Stamina { get; set; }

	[ProtoMember(8)]
	public float HorrifiedLevel { get; set; }

	[ProtoMember(9)]
	public float FocusedLevel { get; set; }

	[ProtoMember(10)]
	public float Energy { get; set; }

	[ProtoMember(11)]
	public float Caffeinated { get; set; }

	[ProtoMember(12)]
	public float SepticShock { get; set; }

	[ProtoMember(13)]
	public float Shock { get; set; }

	[ProtoMember(14)]
	public float EyePanicTime { get; set; }
}
