using CasualtiesUnknownOnline.Abstractions;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One member's declared mod, carried in the handshake (HandshakeMsg.Mods —
/// Phase 4 Mod API consistency check). The host validates the members' lists
/// against its own: missing/mismatched mods are rejected per the NetworkMode
/// policy (RequiresAllPlayers/Synchronized/Authoritative missing or version-
/// unequal → reject; ClientOnly/Cosmetic differences and host-only mods → pass).
/// The declared native binding rides the same entry and is judged separately by
/// the host's parity policy — it never joins the NetworkMode contract. The
/// fingerprint of what the mod materialized rides it too, and is REPORTED rather
/// than judged: the host records a difference, names the mod and admits the member
/// (a comparison over addresses cannot be complete enough to gate entry).
/// The enum serializes as its underlying int; Unspecified (0) is an invalid
/// wire value — the host's shape check rejects it.
/// </summary>
[ProtoContract]
public sealed class ModInfoMsg
{
	[ProtoMember(1)]
	public string Id { get; set; } = string.Empty;

	/// <summary>SemVer version — state-bearing modes are compared by precedence (build metadata ignored).</summary>
	[ProtoMember(2)]
	public string Version { get; set; } = string.Empty;

	[ProtoMember(3)]
	public NetworkMode NetworkMode { get; set; }

	/// <summary>
	/// The declared permission flags (Phase 4b). Serialized as their underlying
	/// int; unknown bits are rejected by the host's shape check. State-bearing
	/// modes require the member's flags to equal the host's.
	/// </summary>
	[ProtoMember(4)]
	public ModPermission Permissions { get; set; }

	/// <summary>
	/// The member's declared native binding — the game's own code this mod
	/// patches — or null when it declared none (`[CuoMod] NativeBinding`; a blank
	/// declaration normalizes to null exactly as discovery normalizes it). The
	/// host compares it with its own declaration for a mod both sides list and
	/// applies its <c>NativeBindingParity</c> rule: allow, warn (the default) or
	/// require. Parity is visibility, never proof — an undeclared binding stays
	/// invisible and an identical declaration does not prove identical
	/// behaviour (the "Handshake consistency" section of `docs/en/reference/mod-api.md`).
	/// </summary>
	[ProtoMember(5)]
	public string? NativeBinding { get; set; }

	/// <summary>
	/// The fingerprint of the content THIS mod registered: the address of every entry
	/// it materialized — the owning mod id, the entry's canonical id, its kind and its
	/// schema version — rendered as one canonical text and hashed. Null = the mod
	/// registered NO content, never "unknown", so both peers read a content-less mod
	/// the same way. The host compares it with its own copy for a mod BOTH sides list,
	/// because id and version equality does not say what a declaration produced: a
	/// definition may COMPUTE its members (decision 251), so two copies of one mod
	/// version can materialize different content, and before this field nothing
	/// noticed (decision 254). A difference is REPORTED, never a refusal.
	/// </summary>
	[ProtoMember(6)]
	public string? ContentFingerprint { get; set; }
}
