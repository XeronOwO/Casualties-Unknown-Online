using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → member: the host's cheat-permission roster, ABSOLUTE — the whole list every
/// time, never a delta. A member that missed one of these is put right by the next
/// one, so the admission edge, the world-entry group, the in-session repair group and
/// a grant/revoke all converge without bookkeeping.
///
/// <para>
/// An EMPTY list is a value, not an absence: it says the host grants the cheat verbs
/// to nobody (the state a fresh session starts in), and a member holding it refuses a
/// cheat verb locally by name. That is why the receiver stores the list it received
/// even when it is empty, and why "no announcement at all" is a different state — one
/// where the local check is skipped and the host's verdict is the only one.
/// </para>
///
/// <para>
/// The host's own standing never travels: it is the online switch
/// (<c>ISessionControl.SessionActive</c>), not a roster entry, so no member can ever
/// read it as a grant somebody made.
/// </para>
/// </summary>
[ProtoContract]
public sealed class CheatPermissionsMsg
{
	/// <summary>The Steam ids this host has granted the cheat verbs to — the whole list, sorted by the sender.</summary>
	[ProtoMember(1)]
	public List<ulong> SteamIds { get; set; } = [];

	/// <summary>The wire form of the roster (the sender's mapping).</summary>
	public static CheatPermissionsMsg From(IReadOnlyList<ulong> steamIds) => new() { SteamIds = [.. steamIds] };

	/// <summary>The list this message carries (the receiver's mapping).</summary>
	public IReadOnlyList<ulong> ToList() => [.. SteamIds];

	/// <summary>
	/// Why this announcement cannot be applied, or null when it is well formed. The one
	/// value no grant can hold is player 0, and it is refused WHOLE rather than filtered
	/// out: a list that names nobody as somebody is not a list this build knows what to
	/// do with, and the receiver keeps the list it holds (or none) instead of half of a
	/// bad one.
	/// </summary>
	public string? Malformed() => SteamIds.Contains(0UL)
		? "the announced roster names player 0, which is nobody"
		: null;
}
