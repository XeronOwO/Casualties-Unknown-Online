using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → one guest: the authoritative state of an item whose evidence the
/// guest's last action report diverged from (wrong contents, condition,
/// liquids, components or slot). This is a data-sync tool for "action valid,
/// the guest's stored data is wrong" — the action itself was accepted and
/// executed by the host from its own table entry. Carries the full item state
/// (digest and full form are the same wire shape; the recipient applies it via
/// its restore machinery) and deliberately NO location fields — dynamic
/// position is the 10 Hz move stream's job. Extra items the guest has that the
/// host does not are removed with a one-shot ItemDestroy instead.
/// </summary>
[ProtoContract]
public sealed class ItemCorrectionMsg
{
	[ProtoMember(1)]
	public CharacterItemMsg Item { get; set; } = new();
}
