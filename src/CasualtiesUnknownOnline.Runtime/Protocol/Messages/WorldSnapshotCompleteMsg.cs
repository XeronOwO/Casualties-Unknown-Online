using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the world-entry snapshot group is complete. Sent after every
/// world-entry/reconnect snapshot so the receiver can distinguish a full
/// authoritative world state from a partial best-effort state.
/// </summary>
[ProtoContract]
public sealed class WorldSnapshotCompleteMsg
{
}
