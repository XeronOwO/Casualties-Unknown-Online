using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The runtime read model for the local mod-status projection domain. It
/// projects the local player's <see cref="ModStatusStore"/> values into a
/// stable, immutable snapshot list and registers the domain through
/// <see cref="IProjectionDomain"/> so the global
/// <see cref="ProjectionHealthCoordinator"/> can observe its revision and
/// rebuild it from the store on failure.
///
/// The GameAdapter's vanilla body/limb and moodle surfaces consume this read
/// seam (body/limb formula snapshots) plus the store-owned resolver/payload
/// accessors; the store itself remains the runtime state source and is never
/// mutated by this projection.
/// </summary>
public sealed class ModStatusProjectionReadModel : ICuoService
{
	private readonly ModStatusStore _statusStore;
	private readonly ISessionControl _session;
	private readonly ProjectionHealthCoordinator _projectionHealth;
	private readonly ILogger<ModStatusProjectionReadModel> _log;
	private IReadOnlyList<ModStatusProjectionSnapshot> _snapshots = [];
	private IReadOnlyList<ModStatusStore.StatusPresence> _presences = [];
	private ulong _lastLocalSteamId;
	private SessionRole _lastRole;

	public ModStatusProjectionReadModel(
		ModStatusStore statusStore,
		ISessionControl session,
		ProjectionHealthCoordinator projectionHealth,
		ILogger<ModStatusProjectionReadModel> log)
	{
		_statusStore = statusStore;
		_session = session;
		_projectionHealth = projectionHealth;
		_log = log;
		_projectionHealth.Register(new ProjectionDomain(
			"mod-status",
			Rebuild,
			() => _statusStore.CurrentRevision));
		_statusStore.StatusChanged += OnStatusChanged;
		Rebuild();
		_lastLocalSteamId = _session.LocalSteamId;
		_lastRole = _session.Role;
	}

	/// <summary>Raised after the read model is rebuilt from the store, including identity-change refreshes and failure-recovery pumps.</summary>
	internal event Action? Changed;

	/// <summary>Visible projection snapshots for the local player (host-authoritative statuses are hidden on a guest).</summary>
	internal IReadOnlyList<ModStatusProjectionSnapshot> ProjectionSnapshots => _snapshots;

	/// <summary>Visible status presences for the local player, including opaque/presentation-only slots; host-authoritative presences are hidden on a guest.</summary>
	internal IReadOnlyList<ModStatusStore.StatusPresence> StatusPresences => _presences;

	internal ulong CurrentRevision => _statusStore.CurrentRevision;

	private void OnStatusChanged() =>
		_projectionHealth.Run("mod-status", _statusStore.CurrentRevision, Rebuild);

	internal void Rebuild()
	{
		var local = _session.LocalSteamId;
		_snapshots = [.. _statusStore.GetProjectionSnapshots(local).Where(IsVisible)];
		_presences = [.. _statusStore.GetStatusPresences(local).Where(IsVisiblePresence)];
		_log.LogDebug(
			"[ModStatusProjection] rebuilt local read model at revision {Revision}: {Snapshots} projection snapshot(s), {Presences} presence(s).",
			_statusStore.CurrentRevision,
			_snapshots.Count,
			_presences.Count);
		Changed?.Invoke();
	}

	private bool IsVisible(ModStatusProjectionSnapshot snapshot) =>
		snapshot.RuntimeScope != ModDataScope.HostAuthoritative || _session.Role == SessionRole.Host;

	private bool IsVisiblePresence(ModStatusStore.StatusPresence presence) =>
		_statusStore.TryGetRuntimeScope(presence.ModId, presence.StatusId, out var runtimeScope)
		&& (runtimeScope != ModDataScope.HostAuthoritative || _session.Role == SessionRole.Host);

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update() => RefreshForIdentityChanges();

	void ICuoService.Stop()
	{
	}

	private void RefreshForIdentityChanges()
	{
		var local = _session.LocalSteamId;
		var role = _session.Role;
		if (_lastLocalSteamId == local && _lastRole == role)
		{
			return;
		}

		var previousLocal = _lastLocalSteamId;
		var previousRole = _lastRole;
		_lastLocalSteamId = local;
		_lastRole = role;
		_log.LogInformation(
			"[ModStatusProjection] local identity changed (steam {PreviousSteamId} -> {SteamId}, role {PreviousRole} -> {Role}); rebuilding the local read model.",
			previousLocal,
			local,
			previousRole,
			role);
		_projectionHealth.Run("mod-status", _statusStore.CurrentRevision, Rebuild);
	}

	public void Dispose() => _statusStore.StatusChanged -= OnStatusChanged;
}
