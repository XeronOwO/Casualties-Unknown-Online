using CasualtiesUnknownOnline.Runtime.Configuration;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The one reading of <see cref="RemoteBackpackAccess"/>: what the rule allows for
/// ONE other player, given that player's authoritative state. The host enforces it
/// at its two seams (the native-intent request handler and the custody-transfer
/// core), and a client reads the same answers to decide whether a view opens, is
/// view-only, or is refused at all — so the two sides cannot disagree about what
/// the announced rule means.
///
/// <para>
/// "Unconscious or dead" is the landed take rule (<c>!Conscious || !Alive</c>), not a
/// new one. An owner whose health is not known yet fails CLOSED for the middle value:
/// a client that cannot establish the owner is down must not be the reason the host
/// permits a rummage, and the host itself refuses the same way when its own snapshot
/// has no health block. <see cref="RemoteBackpackAccess.Allow"/> needs no state and
/// <see cref="RemoteBackpackAccess.Deny"/> refuses regardless.
/// </para>
/// </summary>
public static class RemoteBackpackAccessPolicy
{
	/// <summary>Whether another player's backpack may be opened at all (the middle value opens it view-only).</summary>
	public static bool MayOpen(RemoteBackpackAccess access) => access != RemoteBackpackAccess.Deny;

	/// <summary>
	/// Whether the other player's inventory may be OPERATED on: taken from, moved,
	/// dropped, used, or given into. <paramref name="healthKnown"/> is false when the
	/// authoritative snapshot carries no health block.
	/// </summary>
	public static bool MayOperate(RemoteBackpackAccess access, bool healthKnown, bool conscious, bool alive) => access switch
	{
		RemoteBackpackAccess.Allow => true,
		RemoteBackpackAccess.UnconsciousOrDeadOnly => healthKnown && (!conscious || !alive),
		_ => false,
	};

	/// <summary>Whether an opened view is view-only: it may open, but every operation is refused.</summary>
	public static bool IsViewOnly(RemoteBackpackAccess access, bool healthKnown, bool conscious, bool alive) =>
		MayOpen(access) && !MayOperate(access, healthKnown, conscious, alive);

	/// <summary>The other player's state in words, for a refusal line that names what the rule saw.</summary>
	public static string DescribeState(bool healthKnown, bool conscious, bool alive) =>
		!healthKnown ? "has no health snapshot yet"
		: !alive ? "is dead"
		: conscious ? "is conscious/alive"
		: "is unconscious";
}
