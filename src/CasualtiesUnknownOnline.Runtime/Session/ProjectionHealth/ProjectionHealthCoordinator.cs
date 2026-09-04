using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;

/// <summary>
/// Lightweight per-domain projection health coordinator.
///
/// Projection code runs outside the kernel and must never be allowed to make a
/// committed batch look rejected. This coordinator wraps a projection apply,
/// records the last successfully applied revision, marks the domain dirty when
/// the projection throws, and pumps a per-domain rebuild from the kernel read
/// model on the Unity main thread. Repeated failures escalate the domain to a
/// degraded/diagnostic state so operators can see the projection is not
/// converging without stopping the authoritative flow.
/// </summary>
public sealed class ProjectionHealthCoordinator(ILogger<ProjectionHealthCoordinator> log) : ICuoService
{
	public const int DegradeThreshold = 3;

	private readonly object _sync = new();
	private readonly Dictionary<string, DomainState> _domains = [];
	private readonly ILogger<ProjectionHealthCoordinator> _log = log;

	/// <summary>Register a rebuildable projection domain.</summary>
	public void Register(string domain, Action rebuild, Func<ulong> currentRevision)
	{
		if (string.IsNullOrWhiteSpace(domain))
		{
			throw new ArgumentException("Projection domain must not be empty.", nameof(domain));
		}

		if (rebuild is null)
		{
			throw new ArgumentNullException(nameof(rebuild));
		}

		if (currentRevision is null)
		{
			throw new ArgumentNullException(nameof(currentRevision));
		}

		lock (_sync)
		{
			if (_domains.ContainsKey(domain))
			{
				throw new InvalidOperationException($"Projection domain '{domain}' is already registered.");
			}

			_domains[domain] = new DomainState(domain, rebuild, currentRevision);
		}
	}

	/// <summary>
	/// Run one projection apply under health tracking. A thrown projection is
	/// captured, the domain is marked dirty, and the rebuild is deferred to
	/// <see cref="Pump"/> (Unity main thread).
	/// </summary>
	public void Run(string domain, ulong revision, Action projection)
	{
		if (projection is null)
		{
			throw new ArgumentNullException(nameof(projection));
		}

		DomainState state;
		lock (_sync)
		{
			if (!_domains.TryGetValue(domain, out state!))
			{
				_log.LogWarning("Projection '{Domain}' is not registered; skipping health tracking for revision {Revision}.", domain, revision);
				return;
			}
		}

		try
		{
			projection();
			MarkSuccess(state, revision);
		}
		catch (Exception ex)
		{
			MarkFailure(state, revision, ex);
		}
	}

	/// <summary>
	/// Rebuild every dirty domain from its registered kernel read-model callback.
	/// Call this on the Unity main thread (the ICuoService Update path).
	/// </summary>
	public void Pump()
	{
		List<DomainState>? dirty = null;
		lock (_sync)
		{
			foreach (var state in _domains.Values)
			{
				if (state.Dirty)
				{
					(dirty ??= []).Add(state);
				}
			}
		}

		if (dirty is null)
		{
			return;
		}

		foreach (var state in dirty)
		{
			Rebuild(state);
		}
	}

	/// <summary>Read-only diagnostic snapshot of all tracked projection domains.</summary>
	public IReadOnlyList<ProjectionHealthInfo> Snapshot()
	{
		lock (_sync)
		{
			return [.. _domains.Values.Select(static s => s.ToInfo())];
		}
	}

	public bool IsDirty(string domain)
	{
		lock (_sync)
		{
			return _domains.TryGetValue(domain, out var state) && state.Dirty;
		}
	}

	public bool IsDegraded(string domain)
	{
		lock (_sync)
		{
			return _domains.TryGetValue(domain, out var state) && state.Degraded;
		}
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update() => Pump();

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}

	private void MarkSuccess(DomainState state, ulong revision)
	{
		lock (_sync)
		{
			state.LastSuccessfulRevision = revision;
			state.Dirty = false;
			state.ConsecutiveFailures = 0;
			state.Degraded = false;
			state.LastError = null;
		}
	}

	private void MarkFailure(DomainState state, ulong revision, Exception ex)
	{
		lock (_sync)
		{
			state.Dirty = true;
			state.ConsecutiveFailures++;
			state.TotalFailures++;
			state.LastFailedRevision = revision;
			state.LastError = ex.Message;
			if (state.ConsecutiveFailures >= DegradeThreshold)
			{
				state.Degraded = true;
			}
		}

		_log.LogError(ex,
			"Projection '{Domain}' failed at revision {Revision}; marked dirty for per-domain rebuild.",
			state.Domain, revision);
	}

	private void Rebuild(DomainState state)
	{
		try
		{
			state.Rebuild();
			var revision = state.CurrentRevision();
			lock (_sync)
			{
				state.LastSuccessfulRevision = revision;
				state.Dirty = false;
				state.ConsecutiveFailures = 0;
				state.Degraded = false;
				state.LastError = null;
			}

			_log.LogWarning("Projection '{Domain}' rebuilt from kernel read model at revision {Revision}.",
				state.Domain, revision);
		}
		catch (Exception ex)
		{
			lock (_sync)
			{
				state.ConsecutiveFailures++;
				state.TotalFailures++;
				state.LastError = ex.Message;
				if (state.ConsecutiveFailures >= DegradeThreshold)
				{
					state.Degraded = true;
				}
			}

			_log.LogError(ex,
				"Projection '{Domain}' rebuild failed; domain remains dirty and will retry on the next pump.",
				state.Domain);
		}
	}

	/// <summary>Per-domain mutable tracking state (guarded by the coordinator lock when accessed across threads).</summary>
	private sealed class DomainState(
		string domain,
		Action rebuild,
		Func<ulong> currentRevision)
	{
		internal string Domain { get; } = domain;
		internal Action Rebuild { get; } = rebuild;
		internal Func<ulong> CurrentRevision { get; } = currentRevision;
		internal ulong LastSuccessfulRevision { get; set; }
		internal ulong LastFailedRevision { get; set; }
		internal bool Dirty { get; set; }
		internal bool Degraded { get; set; }
		internal int ConsecutiveFailures { get; set; }
		internal int TotalFailures { get; set; }
		internal string? LastError { get; set; }

		internal ProjectionHealthInfo ToInfo() =>
			new(
				Domain,
				LastSuccessfulRevision,
				LastFailedRevision,
				Dirty,
				Degraded,
				ConsecutiveFailures,
				TotalFailures,
				LastError);
	}
}
