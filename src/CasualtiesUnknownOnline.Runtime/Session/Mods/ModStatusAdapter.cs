using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod <see cref="IModStatusRuntime"/> adapter. Role/scope gates, the
/// mod-id scoping, and defensive copies live here; the primitive table and
/// projection read seam live in <see cref="ModStatusStore"/>. This is a
/// separate top-level type so the store stays under the architecture gate while
/// keeping the adapter in the same mod-status domain.
/// </summary>
internal sealed class ModStatusAdapter(
	ModStatusStore store,
	SessionService session,
	ModManifest manifest,
	ILogger log) : IModStatusRuntime
{
	public bool TryDeclare(
		string statusId,
		ModStatusScope scope,
		ModDataScope runtimeScope,
		int schemaVersion,
		ModStatusProjectionKind projectionKind)
	{
		if (!ModStatusPolicy.IsValidRuntimeScopeFor(manifest, runtimeScope))
		{
			log.LogWarning("[Mods] {ModId} tried to declare runtime status {StatusId} with runtime scope {RuntimeScope} that is invalid for network mode {Mode} — refused.",
				manifest.Id, statusId, runtimeScope, manifest.NetworkMode);
			return false;
		}

		return store.TryDeclare(manifest.Id, statusId, scope, runtimeScope, schemaVersion, projectionKind);
	}

	public bool TryGetBodyStatus(string statusId, ulong playerSteamId, out byte[]? value)
	{
		value = null;
		if (!CanReadStatus(statusId))
		{
			return false;
		}

		return store.TryGetBodyValue(manifest.Id, statusId, playerSteamId, out value);
	}

	public bool TryGetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, out byte[]? value)
	{
		value = null;
		if (!CanReadStatus(statusId))
		{
			return false;
		}

		return store.TryGetLimbValue(manifest.Id, statusId, playerSteamId, limbSlot, out value);
	}

	public bool TrySetBodyStatus(string statusId, ulong playerSteamId, byte[] value)
	{
		if (!TryWriteGuard(statusId))
		{
			return false;
		}

		return store.TrySetBodyValue(manifest.Id, statusId, playerSteamId, value);
	}

	public bool TrySetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value)
	{
		if (!TryWriteGuard(statusId))
		{
			return false;
		}

		return store.TrySetLimbValue(manifest.Id, statusId, playerSteamId, limbSlot, value);
	}

	public bool TryApplyBodyStatus(string statusId, ulong playerSteamId, byte[] value, ulong senderSteamId)
	{
		if (!TryApplyGuard(statusId, senderSteamId))
		{
			return false;
		}

		return store.TrySetBodyValue(manifest.Id, statusId, playerSteamId, value);
	}

	public bool TryApplyLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value, ulong senderSteamId)
	{
		if (!TryApplyGuard(statusId, senderSteamId))
		{
			return false;
		}

		return store.TrySetLimbValue(manifest.Id, statusId, playerSteamId, limbSlot, value);
	}

	public bool TryApplyRemoveBodyStatus(string statusId, ulong playerSteamId, ulong senderSteamId)
	{
		if (!TryApplyGuard(statusId, senderSteamId))
		{
			return false;
		}

		return store.TryRemoveBodyValue(manifest.Id, statusId, playerSteamId);
	}

	public bool TryApplyRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot, ulong senderSteamId)
	{
		if (!TryApplyGuard(statusId, senderSteamId))
		{
			return false;
		}

		return store.TryRemoveLimbValue(manifest.Id, statusId, playerSteamId, limbSlot);
	}

	public bool TryRemoveBodyStatus(string statusId, ulong playerSteamId)
	{
		if (!TryRemoveGuard(statusId))
		{
			return false;
		}

		return store.TryRemoveBodyValue(manifest.Id, statusId, playerSteamId);
	}

	public bool TryRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot)
	{
		if (!TryRemoveGuard(statusId))
		{
			return false;
		}

		return store.TryRemoveLimbValue(manifest.Id, statusId, playerSteamId, limbSlot);
	}

	public bool TryGetScope(string statusId, out ModStatusScope scope)
	{
		scope = default;
		return CanReadStatus(statusId) && store.TryGetScope(manifest.Id, statusId, out scope);
	}

	public bool TryGetRuntimeScope(string statusId, out ModDataScope runtimeScope)
	{
		runtimeScope = default;
		return CanReadStatus(statusId) && store.TryGetRuntimeScope(manifest.Id, statusId, out runtimeScope);
	}

	public bool TryGetSchemaVersion(string statusId, out int schemaVersion)
	{
		schemaVersion = 0;
		return CanReadStatus(statusId) && store.TryGetSchemaVersion(manifest.Id, statusId, out schemaVersion);
	}

	public IReadOnlyCollection<string> StatusIds => store.GetStatusIds(manifest.Id, IsVisible);

	public int StatusCount => store.GetStatusCount(manifest.Id, IsVisible);

	private bool CanReadStatus(string statusId)
	{
		if (!store.TryGetRuntimeScope(manifest.Id, statusId, out var runtimeScope))
		{
			return false;
		}

		if (runtimeScope == ModDataScope.HostAuthoritative && session.Role != SessionRole.Host)
		{
			log.LogWarning("[Mods] {ModId} tried to read host-authoritative runtime status {StatusId} on a guest copy — refused.",
				manifest.Id, statusId);
			return false;
		}

		return true;
	}

	private bool TryWriteGuard(string statusId)
	{
		if (!store.TryGetRuntimeScope(manifest.Id, statusId, out var runtimeScope))
		{
			log.LogWarning("[Mods] {ModId} tried to write undeclared runtime status {StatusId} — refused.",
				manifest.Id, statusId);
			return false;
		}

		if (runtimeScope != ModDataScope.LocalOnly && session.Role != SessionRole.Host)
		{
			log.LogWarning("[Mods] {ModId} tried to write {RuntimeScope} runtime status {StatusId} from a guest — refused.",
				manifest.Id, runtimeScope, statusId);
			return false;
		}

		return true;
	}

	private bool TryApplyGuard(string statusId, ulong senderSteamId)
	{
		if (!store.TryGetRuntimeScope(manifest.Id, statusId, out var runtimeScope))
		{
			log.LogWarning("[Mods] {ModId} tried to apply undeclared runtime status {StatusId} — refused.",
				manifest.Id, statusId);
			return false;
		}

		if (runtimeScope != ModDataScope.Shared)
		{
			log.LogWarning("[Mods] {ModId} tried to apply runtime status {StatusId} as shared but its runtime scope is {RuntimeScope} — refused.",
				manifest.Id, statusId, runtimeScope);
			return false;
		}

		if (session.Role == SessionRole.Host)
		{
			log.LogWarning("[Mods] {ModId} tried to apply runtime status {StatusId} on the host — refused; the host writes with TrySet, not TryApply.",
				manifest.Id, statusId);
			return false;
		}

		if (senderSteamId != session.HostSteamId)
		{
			log.LogWarning("[Mods] {ModId} tried to apply runtime status {StatusId} from non-host sender {Sender} — refused.",
				manifest.Id, statusId, senderSteamId);
			return false;
		}

		return true;
	}

	private bool TryRemoveGuard(string statusId)
	{
		if (!store.TryGetRuntimeScope(manifest.Id, statusId, out var runtimeScope))
		{
			log.LogWarning("[Mods] {ModId} tried to remove undeclared runtime status {StatusId} — refused.",
				manifest.Id, statusId);
			return false;
		}

		if (runtimeScope != ModDataScope.LocalOnly && session.Role != SessionRole.Host)
		{
			log.LogWarning("[Mods] {ModId} tried to remove {RuntimeScope} runtime status {StatusId} from a guest — refused.",
				manifest.Id, runtimeScope, statusId);
			return false;
		}

		return true;
	}

	private bool IsVisible(ModStatusStore.ModStatusEntry entry) =>
		entry.RuntimeScope != ModDataScope.HostAuthoritative || session.Role == SessionRole.Host;
}
