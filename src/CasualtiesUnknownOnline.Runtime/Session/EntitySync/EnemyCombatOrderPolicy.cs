namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure host-side enemy-combat apply-path policy for the decision the host still
/// owns. The attack paths themselves are no longer host decisions: an enemy
/// attack is announced and the client it may have landed on judges it
/// (<see cref="EnemyAttackJudgment"/>, the 2026-09-18 ruling). What remains here
/// is the item-vs-enemy hit: the Game Adapter observes whether the game's native
/// branch ran, and this class owns the direction of that decision without Unity,
/// so the rule is L0-testable.
/// </summary>
public static class EnemyCombatOrderPolicy
{
	/// <summary>The host-side apply path for one enemy combat decision.</summary>
	public enum ApplyPath
	{
		/// <summary>No host action is needed for this decision.</summary>
		None = 0,

		/// <summary>The native game branch handles (or just handled) the host-local case; no extra host action.</summary>
		LocalNative = 1,

		/// <summary>The native item branch skipped the host-local proximity, so the host applies the same native effects before reporting.</summary>
		HostItemFallback = 2,
	}

	/// <summary>
	/// The item-vs-enemy hit apply path. When the local body was already inside
	/// the native 50-unit radius the native branch ran (or will run on this
	/// collision), so the host only reports; otherwise the host applies the
	/// same native effects only when some in-world player is near enough —
	/// this is the multiplayer generalization of the single-player scoping,
	/// and no player nearby means no item-vs-enemy effect at all.
	/// </summary>
	public static ApplyPath DecideItemHit(bool localBodyWithinNativeRadius, bool anyInWorldPlayerWithinRadius)
	{
		if (localBodyWithinNativeRadius)
		{
			return ApplyPath.LocalNative;
		}

		return anyInWorldPlayerWithinRadius ? ApplyPath.HostItemFallback : ApplyPath.None;
	}
}
