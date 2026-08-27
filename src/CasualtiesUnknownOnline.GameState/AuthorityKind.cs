namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Declared authority policy of a command. The kernel uses this in later phases
/// for validation and routing; Phase A records it on every accepted batch.
/// </summary>
public enum AuthorityKind
{
	HostOnly,
	OwnerPredictedHostValidated,
	TriggerObservedHostCommitted,
	PresentationOnly,
}
