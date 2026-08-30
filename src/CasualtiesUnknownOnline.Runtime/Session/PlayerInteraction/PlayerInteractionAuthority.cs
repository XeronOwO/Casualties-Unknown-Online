namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Authority policy for one cross-player interaction. The distinction is
/// between interactions the kernel must validate before committing on the host
/// (no local prediction) and pure presentation that never touches kernel state.
/// </summary>
public enum PlayerInteractionAuthority
{
	/// <summary>
	/// The requesting player sends an intent; the host validates against
	/// authoritative state and commits the fact. The requester does not
	/// optimistically mutate the gameplay projection.
	/// </summary>
	HostValidatedNoPrediction,

	/// <summary>
	/// The owning client may simulate locally first and the host later
	/// validates/corrects. Reserved for future local-prediction seams; 4.3
	/// cross-player interactions deliberately do not use this.
	/// </summary>
	OwnerPredictedHostValidated,

	/// <summary>
	/// Transient presentation only; no kernel command or durable fact.
	/// </summary>
	PresentationOnly,
}
