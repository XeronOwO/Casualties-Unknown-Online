using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The world's keypad codes (airdrop crates, keypad doors — Openable with
/// isKeypad). The game lazy-generates a code on FIRST USE per side
/// (Openable.cs:19) from its own Random stream — two sides would get two
/// codes. The host pre-generates every code at world entry and broadcasts
/// them (position-keyed: world entities are generated deterministically, so
/// both sides have the same object at the same place). Host → guest only
/// (direction-validated by PacketReceiver).
/// </summary>
[ProtoContract]
public sealed class KeypadCodeMsg
{
	[ProtoMember(1)]
	public List<KeypadEntryMsg> Codes { get; set; } = [];
}
