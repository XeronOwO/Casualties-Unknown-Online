using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A lockable building entity was opened (instant-open, lockpick or keypad
/// success — the three paths all write health = 0 directly, Openable.cs:12 /
/// LockpingMinigame.cs:129 / KeypadMinigame.cs:138): guest → host as a report
/// (the host applies it to its copy — which rolls the host-side drops — and
/// relays), host → guest as a broadcast relay (the source excluded). The
/// entity is identified by its world position.
/// </summary>
[ProtoContract]
public sealed class BuildingEntityOpenedMsg
{
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new(); // the entity's world position
}
