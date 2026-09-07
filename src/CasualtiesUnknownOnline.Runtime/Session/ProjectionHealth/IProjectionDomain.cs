namespace CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;

/// <summary>
/// The global, domain-neutral projection contract. Every projection in CUO is a
/// rebuildable read model derived from an authoritative source; this interface
/// is the shared shape that lets the projection registry observe, health-track
/// and rebuild all domains without each caller hand-rolling its own
/// dirty/degraded logic.
///
/// Implementations must never mutate authority. A projection failure is
/// contained by the registry and the domain must be rebuildable from the
/// current authoritative source.
/// </summary>
public interface IProjectionDomain
{
	/// <summary>Stable projection-domain name (e.g. "items", "fluids", "world-entities", "remote-character-presentation").</summary>
	string Domain { get; }

	/// <summary>The authoritative revision the projection should converge to.</summary>
	ulong CurrentRevision { get; }

	/// <summary>Rebuild the projection from the authoritative source.</summary>
	void Rebuild();
}
