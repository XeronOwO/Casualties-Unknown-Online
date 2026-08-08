using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Host → guest: everyone finished loading — start playing (or, for a late joiner, enter directly).</summary>
[ProtoContract]
public sealed class WorldReadyMsg
{
}
