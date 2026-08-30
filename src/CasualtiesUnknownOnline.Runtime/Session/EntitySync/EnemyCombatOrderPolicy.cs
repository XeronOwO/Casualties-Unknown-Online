namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure host-side enemy-combat apply-path policy. The Game Adapter's
/// <c>EnemyCombatDirector</c> has the Unity-facing responsibility of turning a
/// selected victim into a native hit, a host-ordered remote attack, or a
/// host-applied item fallback; this class owns the direction of that decision
/// without Unity, so the ordering rule is L0-testable and can later feed a
/// kernel process without re-deriving the rule from the adapter.
/// </summary>
public static class EnemyCombatOrderPolicy
{
	/// <summary>The host-side apply path for one enemy combat decision.</summary>
	public enum ApplyPath
	{
		/// <summary>No host action is needed for this decision.</summary>
		None = 0,

		/// <summary>Host sends this remote victim an <c>EnemyAttack</c> command; the victim applies the game's damage locally.</summary>
		RemoteOrder = 1,

		/// <summary>The native game collision/ray path handles (or just handled) the local victim; no host command.</summary>
		LocalNative = 2,

		/// <summary>The native item branch skipped the host-local proximity, so the host applies the same native effects before reporting.</summary>
		HostItemFallback = 3,
	}

	/// <summary>
	/// The spider-bite apply path. The victim is null when the game's
	/// cooldown/stun gates or bite range closed the decision; otherwise a
	/// remote victim must be ordered and a local victim rides the native
	/// collision path.
	/// </summary>
	public static ApplyPath DecideSpiderBite(EnemyTargetFact? victim, ulong localSteamId)
	{
		if (victim is not { } fact)
		{
			return ApplyPath.None;
		}

		return fact.SteamId == localSteamId ? ApplyPath.LocalNative : ApplyPath.RemoteOrder;
	}

	/// <summary>
	/// The crystal-lunge apply path. The victim is null when no player lies
	/// along the lunge ray before the first ground hit; a remote victim is
	/// ordered because the game's raycast cannot see a collider-less clone,
	/// while a local victim stays on the native raycast and only needs the
	/// pre/post trace for the terminal-state report.
	/// </summary>
	public static ApplyPath DecideCrystalLunge(EnemyTargetFact? victim, ulong localSteamId)
	{
		if (victim is not { } fact)
		{
			return ApplyPath.None;
		}

		return fact.SteamId == localSteamId ? ApplyPath.LocalNative : ApplyPath.RemoteOrder;
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
