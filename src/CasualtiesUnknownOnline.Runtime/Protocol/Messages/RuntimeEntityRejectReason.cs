namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Why the host rejected a runtime-creation report (decision 161: accept-first
/// never covers state the host cannot own). The first cause is a prefab the
/// host's content set cannot produce, and a vanilla id missing from the game
/// and a mod template missing from the host's mod set are indistinguishable on
/// this side, so both are the same reason. The second is a report of ANOTHER
/// world/layer generation (protocol 30): the host cannot own state of a world
/// it no longer simulates either, and the answer ends the reporter's pending
/// report exactly as the prefab refusal does.
/// </summary>
public enum RuntimeEntityRejectReason : byte
{
	/// <summary>The host has no prefab/template for the reported id, so it can never own a copy of the creation.</summary>
	PrefabUnavailable = 1,

	/// <summary>The report belongs to another world/layer generation (the host is at a different kernel run baseline), so materializing it would create the previous layer's entity in this one.</summary>
	StaleGeneration = 2,
}
