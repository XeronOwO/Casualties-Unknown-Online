using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod typed status transport (mod-status domain phase 2). It wraps the
/// existing <see cref="IModNetwork"/> with the versioned
/// <see cref="ModStatusUpdate"/> wire shape: the host commits a shared status
/// through <see cref="IModStatusRuntime"/> and broadcasts the typed payload;
/// the guest parses the same payload and applies it to the local mirror. This
/// class adds no new NetMsg and no generic snapshot protocol.
///
/// The transport is deliberately a thin adapter: authority/scope checks live in
/// <see cref="IModStatusRuntime"/> and <see cref="IModStatusPolicy"/>; the
/// network permission/role/session checks live in <see cref="ModChannel"/> and
/// the per-mod <see cref="ModContext.ModNetworkAdapter"/>. It only assembles
/// the typed frame and routes the result.
/// </summary>
internal sealed class ModStatusTransport(
	IModStatusRuntime status,
	IModNetwork network,
	ModManifest manifest,
	SessionService session,
	ILogger log) : IModStatusTransport
{
	private readonly IModStatusRuntime _status = status;
	private readonly IModNetwork _network = network;
	private readonly ModManifest _manifest = manifest;
	private readonly SessionService _session = session;
	private readonly ILogger _log = log;

	public bool TryBroadcastBodyStatus(string statusId, ulong playerSteamId, byte[] value)
	{
		if (!CanBroadcast(statusId))
		{
			return false;
		}

		var schemaVersion = ReadSchemaVersion(statusId);
		if (!_status.TrySetBodyStatus(statusId, playerSteamId, value))
		{
			return false;
		}

		var update = ModStatusUpdate.ForBody(statusId, playerSteamId, schemaVersion, value);
		_network.Broadcast(update.ToPayload());
		_log.LogInformation("[Mods] {ModId} broadcast body status {StatusId} for player {Player} ({Length} bytes).",
			_manifest.Id, statusId, playerSteamId, value.Length);
		return true;
	}

	public bool TryBroadcastLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value)
	{
		if (!CanBroadcast(statusId))
		{
			return false;
		}

		var schemaVersion = ReadSchemaVersion(statusId);
		if (!_status.TrySetLimbStatus(statusId, playerSteamId, limbSlot, value))
		{
			return false;
		}

		var update = ModStatusUpdate.ForLimb(statusId, playerSteamId, limbSlot, schemaVersion, value);
		_network.Broadcast(update.ToPayload());
		_log.LogInformation("[Mods] {ModId} broadcast limb status {StatusId} for player {Player} slot {Slot} ({Length} bytes).",
			_manifest.Id, statusId, playerSteamId, limbSlot, value.Length);
		return true;
	}

	public bool TryBroadcastRemoveBodyStatus(string statusId, ulong playerSteamId)
	{
		if (!CanBroadcast(statusId))
		{
			return false;
		}

		var schemaVersion = ReadSchemaVersion(statusId);
		if (!_status.TryRemoveBodyStatus(statusId, playerSteamId))
		{
			return false;
		}

		var update = ModStatusUpdate.RemoveBody(statusId, playerSteamId, schemaVersion);
		_network.Broadcast(update.ToPayload());
		_log.LogInformation("[Mods] {ModId} broadcast body status removal {StatusId} for player {Player}.",
			_manifest.Id, statusId, playerSteamId);
		return true;
	}

	public bool TryBroadcastRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot)
	{
		if (!CanBroadcast(statusId))
		{
			return false;
		}

		var schemaVersion = ReadSchemaVersion(statusId);
		if (!_status.TryRemoveLimbStatus(statusId, playerSteamId, limbSlot))
		{
			return false;
		}

		var update = ModStatusUpdate.RemoveLimb(statusId, playerSteamId, limbSlot, schemaVersion);
		_network.Broadcast(update.ToPayload());
		_log.LogInformation("[Mods] {ModId} broadcast limb status removal {StatusId} for player {Player} slot {Slot}.",
			_manifest.Id, statusId, playerSteamId, limbSlot);
		return true;
	}

	public bool TryHandleStatusPayload(ulong senderSteamId, byte[] payload)
	{
		var update = ModStatusUpdate.FromPayload(payload);
		if (update is null)
		{
			return false;
		}

		if (_session.Role == SessionRole.Host)
		{
			if (senderSteamId == _session.LocalSteamId)
			{
				_log.LogDebug("[Mods] {ModId} consumed its own status broadcast echo ({StatusId}, {Player}).",
					_manifest.Id, update.StatusId, update.PlayerSteamId);
			}
			else
			{
				_log.LogWarning("[Mods] {ModId} ignored a status update sent by guest {Sender} — the host does not apply guest mirrors.",
					_manifest.Id, senderSteamId);
			}

			return true;
		}

		if (update.Scope == ModStatusScope.Body)
		{
			if (update.Remove)
			{
				_status.TryApplyRemoveBodyStatus(update.StatusId, update.PlayerSteamId, senderSteamId);
			}
			else
			{
				_status.TryApplyBodyStatus(update.StatusId, update.PlayerSteamId, update.Value ?? [], senderSteamId);
			}
		}
		else
		{
			if (update.Remove)
			{
				_status.TryApplyRemoveLimbStatus(update.StatusId, update.PlayerSteamId, update.LimbSlot, senderSteamId);
			}
			else
			{
				_status.TryApplyLimbStatus(update.StatusId, update.PlayerSteamId, update.LimbSlot, update.Value ?? [], senderSteamId);
			}
		}

		return true;
	}

	private bool CanBroadcast(string statusId)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			_log.LogWarning("[Mods] {ModId} tried to broadcast runtime status {StatusId} outside a host session — refused.",
				_manifest.Id, statusId);
			return false;
		}

		if (!_status.TryGetRuntimeScope(statusId, out var runtimeScope) || runtimeScope != ModDataScope.Shared)
		{
			_log.LogWarning("[Mods] {ModId} tried to broadcast runtime status {StatusId} that is not a declared shared slot — refused.",
				_manifest.Id, statusId);
			return false;
		}

		return true;
	}

	private int ReadSchemaVersion(string statusId) =>
		_status.TryGetSchemaVersion(statusId, out var schemaVersion) ? schemaVersion : 1;
}
