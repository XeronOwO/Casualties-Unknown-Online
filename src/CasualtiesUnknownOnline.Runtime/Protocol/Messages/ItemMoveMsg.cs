using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Moving world-item positions (host → guest, unreliable): the host's physics
/// is the position authority — the guests follow instead of diverging on
/// their own physics ("drops bounce to different spots" — the spawn state is
/// synced, the independent physics after it is not). Drops are harmless to
/// lose (the next tick overwrites), like the 20 Hz state stream.
/// </summary>
[ProtoContract]
public sealed class ItemMoveMsg
{
	[ProtoMember(1)]
	public List<ItemMoveEntryMsg> Items { get; set; } = [];
}
