using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: one enemy aggregate left the authoritative host set. Unlike
/// the 20 Hz state batch — an update-only convergence stream — this is the
/// explicit lifecycle fact for an enemy despawn/destruction. Reliable: a lost
/// removal must not leave a stale frozen copy on the guest.
/// </summary>
[ProtoContract]
public sealed class EnemyRemovedMsg
{
	[ProtoMember(1)]
	public NetworkEntityIdMsg Id { get; set; } = new();
}
