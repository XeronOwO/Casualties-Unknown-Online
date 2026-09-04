namespace CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;

/// <summary>Immutable diagnostic view of one projection domain tracked by the health coordinator.</summary>
public sealed record ProjectionHealthInfo(
	string Domain,
	ulong LastSuccessfulRevision,
	ulong LastFailedRevision,
	bool Dirty,
	bool Degraded,
	int ConsecutiveFailures,
	int TotalFailures,
	string? LastError);
