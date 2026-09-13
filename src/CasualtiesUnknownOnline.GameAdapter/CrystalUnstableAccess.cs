namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Reflection access to the internal CrystalUnstable effect (CrystalUnstable.cs): the
/// effect list lives in the private CrystalBehaviour.effects field
/// (CrystalBehaviour.cs:83-107) and the 5 s pre-explosion ticking latch is the private
/// bool timerStarted (CrystalUnstable.cs:70). The lookup and the typed bool read are
/// <see cref="CrystalEffectAccess"/>'s (one place for the untyped list scan); this type
/// keeps the unstable effect's type name and its own reasoning. The GameFieldContractTests
/// rows lock the member against a game update.
/// </summary>
internal static class CrystalUnstableAccess
{
	private const string UnstableTypeName = "CrystalUnstable";

	private const string TimerStartedFieldName = "timerStarted";

	/// <summary>The CrystalUnstable effect on this crystal, or null when the crystal has none (a non-unstable effect set — the position-keyed replay then reports the mismatch).</summary>
	internal static object? Find(CrystalBehaviour crystal) => CrystalEffectAccess.Find(crystal, UnstableTypeName);

	/// <summary>The unstable crystal's timerStarted latch (false when the crystal
	/// carries no unstable effect): true means THIS side's copy already started
	/// its own 5 s natural countdown (its local player touched/hit it) — the
	/// ticking visual is already running natively, a replay must not double it.</summary>
	internal static bool IsTimerStarted(CrystalBehaviour crystal)
	{
		var unstable = Find(crystal);
		return unstable is not null
			&& CrystalEffectAccess.TryReadBool(unstable, TimerStartedFieldName, out var started)
			&& started;
	}
}
