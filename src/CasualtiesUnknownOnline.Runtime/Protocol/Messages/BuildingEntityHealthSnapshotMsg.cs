using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → member: every damaged building entity so far (world entry / the
/// 60 s resend, sent alongside the block-state, trap-state and
/// opened-entities snapshots). Position-keyed — the receiver finds its own
/// deterministically-generated copy at each position and writes the host's
/// current health, the same semantic as the live BuildingEntityDamaged relay.
/// </summary>
[ProtoContract]
public sealed class BuildingEntityHealthSnapshotMsg
{
	[ProtoMember(1)]
	public List<BuildingEntityHealthEntryMsg> Entries { get; set; } = [];
}
