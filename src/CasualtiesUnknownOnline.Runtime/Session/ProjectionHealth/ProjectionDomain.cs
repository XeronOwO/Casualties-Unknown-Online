using System;

namespace CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;

/// <summary>
/// Adapter that turns the existing delegate-based projection registration into
/// the typed <see cref="IProjectionDomain"/> contract. It lets current callers
/// migrate incrementally while still exposing every domain through the same
/// registry/health surface.
/// </summary>
public sealed class ProjectionDomain(
	string domain,
	Action rebuild,
	Func<ulong> currentRevision) : IProjectionDomain
{
	public string Domain { get; } = domain ?? throw new ArgumentNullException(nameof(domain));

	public ulong CurrentRevision => _currentRevision();

	public void Rebuild() => _rebuild();

	private readonly Action _rebuild = rebuild ?? throw new ArgumentNullException(nameof(rebuild));
	private readonly Func<ulong> _currentRevision = currentRevision ?? throw new ArgumentNullException(nameof(currentRevision));
}
