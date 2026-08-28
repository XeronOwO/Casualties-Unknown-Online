using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// One authoritative child item inside a container sync command. Container
/// contents are separate kernel item facts; this wire shape carries the facts
/// a guest reports for one container subtree so the host can reconcile them.
/// </summary>
[ProtoContract]
public sealed class WireContainerChild
{
	[ProtoMember(1)]
	public WireItemIdentity Identity { get; set; } = new();

	[ProtoMember(2)]
	public ulong ParentItemId { get; set; }

	[ProtoMember(3)]
	public WireItemData Data { get; set; } = new();
}
