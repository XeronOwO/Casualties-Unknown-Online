using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the result of a <see cref="ModCommandRequestMsg"/> (NetMsg.
/// ModCommandResult). Directed to the requester only, reliable, never
/// broadcast. The guest's pending callback is settled by RequestId + ModId;
/// unknown request ids are dropped with a log.
/// </summary>
[ProtoContract]
public sealed class ModCommandResultMsg
{
	/// <summary>The requester-assigned correlation id (from the request).</summary>
	[ProtoMember(1)]
	public uint RequestId { get; set; }

	/// <summary>The command-owning mod's declared id.</summary>
	[ProtoMember(2)]
	public string ModId { get; set; } = string.Empty;

	/// <summary>The command name that executed.</summary>
	[ProtoMember(3)]
	public string Name { get; set; } = string.Empty;

	/// <summary>True when the handler returned normally.</summary>
	[ProtoMember(4)]
	public bool Success { get; set; }

	/// <summary>The handler's returned output (capped by the framework).</summary>
	[ProtoMember(5)]
	public string Output { get; set; } = string.Empty;

	/// <summary>The failure reason when <see cref="Success"/> is false.</summary>
	[ProtoMember(6)]
	public string Error { get; set; } = string.Empty;
}
