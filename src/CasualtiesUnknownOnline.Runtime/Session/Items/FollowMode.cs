namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>The per-item follow mode (see <see cref="FollowDecision.Mode"/>):
/// Frozen — no stream tick yet, never pumped; Settled — the copy is at rest and
/// the residual gap eases away; Moving — the local physics runs from the host's
/// velocity.</summary>
internal enum FollowMode
{
	Frozen,
	Settled,
	Moving,
}
