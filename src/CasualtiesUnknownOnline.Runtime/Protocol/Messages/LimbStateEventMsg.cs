using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A player's limb latch changed (broke / mended / dislocated / un-dislocated
/// / dismembered — Limb.cs:193-273, 518-522): the dedicated trigger event.
/// The owner applies the change locally (local compute) and reports the
/// post-event terminal state; the host merges it into the saved character and
/// relays to the other members (source excluded — the source already applied
/// locally), whose render clones apply the presentation immediately. The
/// 1 Hz character snapshot stays the fallback/replay channel.
/// <see cref="Limbs"/> carries EVERY limb of the owner, not just the reported
/// one: <c>Dismember</c> also deactivates the lower limbs and mutates the
/// connected limbs in the same operation (Limb.cs:91-145), so the event must
/// rebuild the full limb set exactly (one operation = one message, never a
/// delta). <see cref="Health"/> carries the post-event body state for the
/// same reason (BreakBone writes body.adrenaline/internalBleeding, MendBone
/// writes happiness, Dismember writes traumaAmount, Dislocate writes
/// adrenaline — the body fields changed by the same operation).
/// </summary>
[ProtoContract]
public sealed class LimbStateEventMsg
{
	/// <summary>The limb owner's SteamId (stamped by the reporter; the host stamps its own on broadcast).</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>Every limb's full post-event terminal state (exact rebuild — the whole Body.limbs set, never a delta).</summary>
	[ProtoMember(2)]
	public List<CharacterLimbMsg> Limbs { get; set; } = [];

	/// <summary>The body's full post-event terminal state (the same capture as the 1 Hz character snapshot's Health).</summary>
	[ProtoMember(3)]
	public CharacterHealthMsg? Health { get; set; }
}
