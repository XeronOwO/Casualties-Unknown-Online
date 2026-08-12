using CasualtiesUnknownOnline.Abstractions;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One member's declared mod, carried in the handshake (HandshakeMsg.Mods —
/// Phase 4 Mod API consistency check). The host validates the members' lists
/// against its own: missing/mismatched mods are rejected per the NetworkMode
/// policy (RequiresAllPlayers/Synchronized/Authoritative missing or version-
/// unequal → reject; ClientOnly/Cosmetic differences and host-only mods → pass).
/// The enum serializes as its underlying int; Unspecified (0) is an invalid
/// wire value — the host's shape check rejects it.
/// </summary>
[ProtoContract]
public sealed class ModInfoMsg
{
	[ProtoMember(1)]
	public string Id { get; set; } = string.Empty;

	/// <summary>Exact string version — the handshake compares by equality.</summary>
	[ProtoMember(2)]
	public string Version { get; set; } = string.Empty;

	[ProtoMember(3)]
	public NetworkMode NetworkMode { get; set; }
}
