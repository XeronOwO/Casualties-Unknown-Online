namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The three answers a lost link can have.</summary>
internal enum PeerLossAction
{
	/// <summary>Nothing to do: the edge belongs to a peer this side does not track.</summary>
	None,

	/// <summary>The host drops the member it still knows.</summary>
	DropMember,

	/// <summary>The guest's link to its host ended: nothing it reports can arrive any more.</summary>
	EndSession,
}

