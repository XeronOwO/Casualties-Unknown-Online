namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Why the host rejected a runtime-creation report (decision 161: accept-first
/// never covers state the host cannot own). One code is enough while there is
/// one real cause — the host's content set cannot produce the reported prefab,
/// and a vanilla id missing from the game and a mod template missing from the
/// host's mod set are indistinguishable on this side, so both are the same
/// reason. Extend only when a second cause appears.
/// </summary>
public enum RuntimeEntityRejectReason : byte
{
	/// <summary>The host has no prefab/template for the reported id, so it can never own a copy of the creation.</summary>
	PrefabUnavailable = 1,
}
