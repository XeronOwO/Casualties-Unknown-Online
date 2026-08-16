using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: invoke a registered mod command (NetMsg.ModCommandRequest —
/// Phase 4b host commands). The host validates the shape caps, the sender's
/// membership, the mod/command/permission state, then executes the handler on
/// its own copy of the mod and answers with a directed
/// <see cref="ModCommandResultMsg"/>. The guest NEVER executes the command
/// locally — host-authoritative command execution, star topology.
/// </summary>
[ProtoContract]
public sealed class ModCommandRequestMsg
{
	/// <summary>The requester-assigned correlation id (echoed in the result).</summary>
	[ProtoMember(1)]
	public uint RequestId { get; set; }

	/// <summary>The command-owning mod's declared id.</summary>
	[ProtoMember(2)]
	public string ModId { get; set; } = string.Empty;

	/// <summary>The registered command name.</summary>
	[ProtoMember(3)]
	public string Name { get; set; } = string.Empty;

	/// <summary>The command arguments (framework shape caps: ≤16 entries, ≤256 chars each).</summary>
	[ProtoMember(4)]
	public List<string> Arguments { get; set; } = [];
}
