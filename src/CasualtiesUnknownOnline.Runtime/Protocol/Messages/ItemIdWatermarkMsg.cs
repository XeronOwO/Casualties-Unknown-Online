using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The item-instance-id counter high-water mark of one side: guest → host as a
/// report (every allocation advances it), host → guest as a grant on join or
/// reconnect (the guest resumes from watermark + 1 — a crashed-and-rejoined
/// guest's counter restarts from zero and would otherwise reuse ids the host's
/// tables still hold). Ids are (counter &lt;&lt; 32) | SteamId — the space is
/// per-SteamId, so a per-guest watermark is all the coordination needed.
/// </summary>
[ProtoContract]
public sealed class ItemIdWatermarkMsg
{
	/// <summary>The highest counter value this side has allocated (0 = never allocated).</summary>
	[ProtoMember(1)]
	public ulong Counter { get; set; }
}
