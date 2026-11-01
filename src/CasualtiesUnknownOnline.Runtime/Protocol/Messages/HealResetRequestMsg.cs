using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: a member typed one of the console cheat verbs this family carries — <c>/heal</c> or
/// <c>/kill</c>, named by <see cref="Operation"/>. The host judges the member's cheat permission
/// against its own switch and roster, then carries the operation to each named target's own client —
/// the body mutation is the target's to apply — and answers the member's console.
/// </summary>
[ProtoContract]
public sealed class HealResetRequestMsg
{
	/// <summary>The players the member's selector resolved, on the member's own view.</summary>
	[ProtoMember(1)]
	public List<ulong> TargetSteamIds { get; set; } = [];

	/// <summary>
	/// Which body operation the member asked for (<see cref="BodyOperation"/>). 0 is never sent by
	/// this build and a request that states it is refused, so an absent field can never be read as a
	/// quiet <c>Heal</c>.
	/// </summary>
	[ProtoMember(2)]
	public BodyOperation Operation { get; set; }
}
