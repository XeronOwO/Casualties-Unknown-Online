namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// What a cut does with ONE class of in-flight state (§4 of the format doc).
/// Every class carries an explicit verdict — the ticket's rule is "no silent
/// loss", so an undecided class is a bug, not a default.
/// </summary>
public enum WorldTransientVerdict
{
	/// <summary>The state is part of the snapshot: the cut carries it and a restore puts it back.</summary>
	Capture,

	/// <summary>
	/// The state resolves itself within a frame or two, and capturing it is
	/// impossible while a live world needs it (a break's drops do not exist yet).
	/// The cut therefore WAITS for the state to resolve before it is taken, and
	/// names it if it outlasts the deadline.
	/// </summary>
	ResolveBeforeSave,

	/// <summary>
	/// The state is not carried. It is a live-world or network artifact whose
	/// world effect is already in the kernel (or which the restored world
	/// re-derives), so a cut names it — count, class and why — and goes on.
	/// </summary>
	DropWithLog,
}
