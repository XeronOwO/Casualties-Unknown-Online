namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>What this side's answer to a member's re-entry ask is — the verdict of <see cref="ReentryPolicy"/>.</summary>
internal enum ReentryDecision
{
	/// <summary>This host's world is up and a run baseline exists — send the targeted entry instruction.</summary>
	Invite,

	/// <summary>The ask reached a side that is not an active host (a guest, solo, or a dead session) — drop it.</summary>
	NotThisSide,

	/// <summary>This host has no live world right now (it is entering one, regenerating, or in a menu) — the member's bounded window asks again.</summary>
	NoLiveWorld,

	/// <summary>The world is up but no kernel run baseline is committed — an entry now is a member waiting for params that will never come, so it is refused by name.</summary>
	NoRunBaseline,
}
