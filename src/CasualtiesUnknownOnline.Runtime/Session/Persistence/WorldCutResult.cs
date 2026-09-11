namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// What one attempt at an armed cut resolved to. <see cref="Deferred"/> is not a
/// failure: the cut is waiting for an in-flight state that resolves itself
/// within a frame or two (see <see cref="WorldTransientPolicy"/>), and the seam
/// retries on the next frame with the request still armed.
/// </summary>
public enum WorldCutResult
{
	/// <summary>Armed, but in-flight state the policy resolves first is still pending; the request stays armed.</summary>
	Deferred,

	/// <summary>A snapshot was written.</summary>
	Captured,

	/// <summary>The cut cannot be taken (guest, no world, no run baseline, an unreadable native table, a write failure).</summary>
	Refused,
}
