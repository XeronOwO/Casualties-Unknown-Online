using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: a member typed the console cheat verb <c>/heal &lt;selector&gt;</c>. The host
/// judges the member's cheat permission against its own switch and roster, then carries the reset
/// to each named target's own client — the body mutation is the target's to apply — and answers
/// the member's console.
/// </summary>
[ProtoContract]
public sealed class HealResetRequestMsg
{
	/// <summary>The players the member's selector resolved, on the member's own view.</summary>
	[ProtoMember(1)]
	public List<ulong> TargetSteamIds { get; set; } = [];
}
