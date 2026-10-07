namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>What this client's side of the end-of-layer choice is — the verdict of <see cref="LayerAdvancePolicy"/>.</summary>
internal enum LayerAdvanceDecision
{
	/// <summary>This host's world can take the advance — drive the native end-of-layer entry.</summary>
	Drive,

	/// <summary>The session's advance is not this client's to take (a guest's choice is a request; a client with no live session has no session to advance).</summary>
	NotThisSides,

	/// <summary>No generated layer exists here to advance (a host that has not entered a world yet).</summary>
	NoWorld,

	/// <summary>An advance is already running — the native entry's own re-entrancy clauses.</summary>
	AlreadyAdvancing,
}
