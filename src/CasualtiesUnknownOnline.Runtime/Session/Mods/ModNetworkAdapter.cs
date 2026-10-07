using System;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod anonymous-tunnel send surface — every call routes through the
/// channel with the mod's own id. SendNetworkMessage is checked here
/// (undeclared messages are refused). The declared-packet surface beside it is
/// <see cref="ModPacketsAdapter"/>; this one is the single-opaque-payload form.
/// </summary>
internal sealed class ModNetworkAdapter(ModChannel channel, ModManifest manifest, ILogger log) : IModNetwork
{
	public void SendToHost(byte[] payload)
	{
		if (CanSend()) { channel.SendToHost(manifest.Id, payload); }
	}

	public void SendToPeer(ulong steamId, byte[] payload)
	{
		if (CanSend()) { channel.SendToPeer(manifest.Id, steamId, payload); }
	}

	public void Broadcast(byte[] payload)
	{
		if (CanSend()) { channel.SendToAll(manifest.Id, payload); }
	}

	public event Action<ulong, byte[]>? MessageReceived;

	public void FireMessageReceived(ulong sender, byte[] payload) => MessageReceived?.Invoke(sender, payload);

	private bool CanSend()
	{
		if (ModPermissionGate.HasPermission(manifest, ModPermission.SendNetworkMessage))
		{
			return true;
		}

		log.LogWarning("[Mods] {ModId} does not declare {Permission} — the call is refused.", manifest.Id, "SendNetworkMessage");
		return false;
	}
}
