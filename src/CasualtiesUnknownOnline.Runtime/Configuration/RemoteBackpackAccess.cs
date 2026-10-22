namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// Who may open and operate another player's backpack. It replaces the boolean
/// <c>AllowRemoteInventoryTake</c> (pre-release: the old key and its meaning are
/// replaced, not kept beside it), because one flag was answering two different
/// questions — "may anyone touch another player's pack at all" and "may a
/// CONSCIOUS player's pack be touched" — for the whole family: the remote
/// backpack viewer, every native intent captured from it, the cross-player take,
/// and the push direction that gives an item away.
///
/// <para>
/// The middle value is the landed take rule (<c>!Conscious || !Alive</c>): only an
/// unconscious or dead body may be operated on. A conscious owner's backpack still
/// opens — the player may LOOK at it, and the game's own red slot look says it is not
/// operable (the look the native game gives an unconscious player viewing their own
/// pack; this design permits the pickup and refuses the release) — but every operation
/// is refused.
/// </para>
/// </summary>
public enum RemoteBackpackAccess
{
	/// <summary>Any visible player's backpack may be opened and operated. Today's <c>true</c>, and the default: the co-op rule the mod has always had.</summary>
	Allow,

	/// <summary>Only an unconscious or dead player's backpack may be OPERATED on. A conscious owner's backpack opens view-only: items are visible, every operation (take, move, drop, use, give) is refused, and the game's red slot look marks it.</summary>
	UnconsciousOrDeadOnly,

	/// <summary>No player may open or operate another player's backpack. Today's <c>false</c>.</summary>
	Deny,
}
