using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One spectator notice: a watcher reports that they started or stopped watching a specific member, and the host
/// relays the same payload to that member only (a targeted relay, unlike chat's broadcast). The ruling is that the
/// watched player is TOLD; the payload names both ends explicitly, and the pair is validated by data the receiver
/// already holds (both are members of this session, and on the host's receipt the watcher must be the sender).
/// </summary>
[ProtoContract]
public sealed class SpectatorNoticeMsg
{
	/// <summary>The member doing the watching (the report's author; on the host's relay the transport sender is
	/// the host, which is why this is carried explicitly).</summary>
	[ProtoMember(1)]
	public ulong WatcherSteamId { get; set; }

	/// <summary>The member being watched — the only peer the host relays to.</summary>
	[ProtoMember(2)]
	public ulong TargetSteamId { get; set; }

	/// <summary>True when watching started, false when it stopped.</summary>
	[ProtoMember(3)]
	public bool Watching { get; set; }
}
