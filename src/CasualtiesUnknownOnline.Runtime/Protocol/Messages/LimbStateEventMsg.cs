using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The owner's FULL post-event limb and body state. Two triggers share it:
/// a limb latch changed (broke / mended / dislocated / un-dislocated /
/// dismembered — Limb.cs:193-273, 518-522), or a client-judged injury raised a
/// limb's bleed (the plain 1 Hz report may not state the host-owned bleed, so
/// this event is how the host's model learns the wound). The owner applies the
/// change locally (local compute) and reports the post-event state; the host
/// merges the limbs (each limb's bleed clamped upward against its own last
/// written value — only a dismembered limb's zero is a downward write) and the
/// non-owned body fields into the saved character, keeps its own two continuous
/// blood values, and relays to the other members (source excluded — the source
/// already applied locally), whose render clones apply the presentation
/// immediately. The 1 Hz character snapshot stays the fallback/replay channel.
/// <see cref="Limbs"/> carries EVERY limb of the owner, not just the reported
/// one: <c>Dismember</c> also deactivates the lower limbs and mutates the
/// connected limbs in the same operation (Limb.cs:91-145), so the event must
/// rebuild the full limb set exactly (one operation = one message, never a
/// delta). <see cref="Health"/> carries the post-event body state for the
/// same reason (BreakBone writes body.adrenaline/internalBleeding, MendBone
/// writes happiness, Dismember writes traumaAmount, Dislocate writes
/// adrenaline — the body fields changed by the same operation); its
/// <c>BloodVolume</c>/<c>TotalBleedSpeed</c> are overwritten with the host's
/// own when the host merges it.
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
