using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → the reporting guest: the runtime creation this guest reported is
/// REJECTED — the host cannot materialize it, so it is neither recorded nor
/// relayed and the reporter is told why (decision 161). This message is the
/// report's answer: the reporter drops its pending re-report (the 60 s fallback
/// stops) and destroys its local copy, so no peer keeps an entity the host can
/// never own, back up or retract — the unowned accept the corrected accept-first
/// precondition forbids.
/// <para>
/// The key is the CREATION identity, never a position: the copy may have been
/// pushed or fallen out of its creation cell before this arrives.
/// </para>
/// </summary>
[ProtoContract]
public sealed class RuntimeEntityRejectedMsg
{
	/// <summary>The rejected creation's identity (prefab id + creation cell + creation-instance token).</summary>
	[ProtoMember(1)]
	public RuntimeEntityKeyMsg Key { get; set; } = new();

	/// <summary>Why the host rejected it.</summary>
	[ProtoMember(2)]
	public RuntimeEntityRejectReason Reason { get; set; } = RuntimeEntityRejectReason.PrefabUnavailable;
}
