namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// What one <see cref="ILocalBodyReset.Reset"/> did, in the facts the console answer and the
/// target's own notice name: whether the body was reset, why not, whether the target was dead
/// before it (a reset revives), how many severed limbs came back, and whether the hollow debuff
/// was cleared.
/// </summary>
public readonly record struct LocalBodyResetOutcome(
	bool Applied,
	string Refusal,
	bool WasDead,
	int RestoredLimbs,
	bool HollowCleared)
{
	/// <summary>The client could not reset a body, with the reason the operator reads.</summary>
	public static LocalBodyResetOutcome Refused(string reason) => new(false, reason, false, 0, false);

	/// <summary>One applied reset, with the facts the answer names.</summary>
	public static LocalBodyResetOutcome AppliedNow(bool wasDead, int restoredLimbs, bool hollowCleared) =>
		new(true, "", wasDead, restoredLimbs, hollowCleared);
}
