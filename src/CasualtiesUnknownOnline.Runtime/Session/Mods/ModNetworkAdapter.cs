using System;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod anonymous-tunnel send surface — every call encodes the mod's
/// value and routes it through the channel with the mod's own id.
/// SendNetworkMessage is checked here (undeclared messages are refused), and so
/// is the value itself: the framework's encoder is the one validator, and a
/// refusal names the path inside the model. The declared-packet surface beside
/// it is <see cref="ModPacketsAdapter"/>.
/// </summary>
internal sealed class ModNetworkAdapter(ModChannel channel, ModManifest manifest, ILogger log) : IModNetwork
{
	public void SendToHost(ModValue value)
	{
		if (CanSend() && TryEncode("SendToHost", value, out var encoded))
		{
			channel.SendToHost(manifest.Id, encoded);
		}
	}

	public void SendToPeer(ulong steamId, ModValue value)
	{
		if (CanSend() && TryEncode("SendToPeer", value, out var encoded))
		{
			channel.SendToPeer(manifest.Id, steamId, encoded);
		}
	}

	public void Broadcast(ModValue value)
	{
		if (CanSend() && TryEncode("Broadcast", value, out var encoded))
		{
			channel.SendToAll(manifest.Id, encoded);
		}
	}

	public event Action<ulong, ModValue>? MessageReceived;

	public void FireMessageReceived(ulong sender, ModValue value) => MessageReceived?.Invoke(sender, value);

	private bool CanSend()
	{
		if (ModPermissionGate.HasPermission(manifest, ModPermission.SendNetworkMessage))
		{
			return true;
		}

		log.LogWarning("[Mods] {ModId} does not declare {Permission} — the call is refused.", manifest.Id, "SendNetworkMessage");
		return false;
	}

	/// <summary>Encode one value for the wire; a refusal is this surface's one log line, naming the path inside the model.</summary>
	private bool TryEncode(string call, ModValue value, out byte[] encoded)
	{
		if (ModValueCodec.TryEncode(value, ModChannel.MaxPayloadBytes, out encoded, out var refusal))
		{
			return true;
		}

		log.LogWarning("[Mods] {ModId} {Call} refused the value — {Reason}", manifest.Id, call, refusal);
		return false;
	}
}
