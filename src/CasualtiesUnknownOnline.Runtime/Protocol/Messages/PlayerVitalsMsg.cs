using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → client: the host-computed continuous bleeding row for one player (the 2026-10-18
/// ruling — the host owns the continuous value, the affected client judges the events and
/// applies the row).
///
/// <para>
/// What it carries is ONLY the two values the host owns: the body's blood volume and each
/// limb's bleed amount, plus the body-level bleeding speed the panel reads. Everything that
/// is random or terminal — infection, internal-bleeding rolls, death, unconsciousness, a
/// broken limb — never rides this message; those stay with the affected client's events.
/// </para>
///
/// <para>
/// The receiver applies the row to its own live body (the only body it may write); a row naming
/// another player is dropped there, because that player's own client is the one applying it. It
/// is UNRELIABLE on purpose: the next row is 100 ms away and repairs a lost one, which is
/// decision 2's rule for a self-healing fact.
/// </para>
/// </summary>
[ProtoContract]
public sealed class PlayerVitalsMsg
{
	/// <summary>The player this row belongs to (the host's own SteamId names the host's body).</summary>
	[ProtoMember(1)]
	public ulong SteamId { get; set; }

	/// <summary>The host-computed blood volume (<c>Body.bloodVolume</c>).</summary>
	[ProtoMember(2)]
	public float BloodVolume { get; set; }

	/// <summary>The host-computed body bleeding speed (<c>Body.totalBleedSpeed</c>), for the panel's l/m line.</summary>
	[ProtoMember(3)]
	public float TotalBleedSpeed { get; set; }

	/// <summary>The per-limb bleed amounts the host owns, by native limb index.</summary>
	[ProtoMember(4)]
	public List<PlayerLimbBleedMsg> Limbs { get; set; } = [];
}
