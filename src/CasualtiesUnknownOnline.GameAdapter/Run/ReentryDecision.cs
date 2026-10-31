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

	/// <summary>
	/// The world is up but no kernel run baseline is committed — the entry is still sent, because the instruction carries
	/// <c>RunBaselineFollows = false</c> in exactly this case (the flag and the fact come from one read, so a member can never be told
	/// to wait for a baseline that will not follow), and a member joining a world that is already BUILT does not generate: it needs the
	/// run identity, not the RNG that seeds a generation it will not run. Refusing here is what kept a host in a world from ever taking a
	/// member back — measured 2026-11-01: asks every five seconds, each answered "no committed run baseline".
	/// </summary>
	InviteWithoutBaseline,
}
