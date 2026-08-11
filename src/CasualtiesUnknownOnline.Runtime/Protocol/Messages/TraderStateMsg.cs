using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A trader's authoritative state — host → guest, reliable, a FULL overwrite
/// of the trader's sync fields + stock (the trader's state is host-computed,
/// the guest's local copy rides the broadcast; the game's own deterministic
/// paths — hostility MoveTowards, LightBroken's flat -40 — run on both sides
/// from the broadcasted base). Sent on every interaction, on world entry
/// (snapshot), and every 5 s as the unreliable fallback (a new layer's
/// traders, a missed broadcast). Position-keyed like the entity events — both
/// sides generated the same trader at the same place (WorldGeneration.cs:
/// 3438-3447). <see cref="RejectedAction"/> is the concurrent-purchase
/// refusal: the acting side already created the item locally (its only spawn),
/// and a refusal means the host's stock was already consumed — the acting side
/// destroys its copy on apply.
/// </summary>
[ProtoContract]
public sealed class TraderStateMsg
{
	/// <summary>The trader's world position (its own transform).</summary>
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(2)]
	public float Reputation { get; set; }

	[ProtoMember(3)]
	public float Hostility { get; set; }

	[ProtoMember(4)]
	public int ValueGiven { get; set; }

	[ProtoMember(5)]
	public int TotalValueGiven { get; set; }

	[ProtoMember(6)]
	public byte FreeAmount { get; set; }

	[ProtoMember(7)]
	public bool FreeDressing { get; set; }

	[ProtoMember(8)]
	public bool DidHug { get; set; }

	[ProtoMember(9)]
	public bool DidMove { get; set; }

	[ProtoMember(10)]
	public bool StartedConvo { get; set; }

	[ProtoMember(11)]
	public float HaggleAmount { get; set; }

	/// <summary>0 = none; otherwise the rejected action kind (Purchase) — the acting side rolls back its local effect.</summary>
	[ProtoMember(12)]
	public byte RejectedAction { get; set; }

	[ProtoMember(13)]
	public TraderItemMsg[] Items { get; set; } = [];
}
