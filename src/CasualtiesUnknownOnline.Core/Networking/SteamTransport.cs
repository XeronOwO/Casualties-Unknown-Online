using System;
using System.Runtime.InteropServices;
using CasualtiesUnknownOnline.Core.Logging;
using Steamworks;

namespace CasualtiesUnknownOnline.Core.Networking;

/// <summary>
/// MVP transport over ISteamNetworkingMessages (reliable + unreliable
/// messages, no connection handles). Single channel (0) for now; the
/// architecture's INetworkTransport abstraction lands when a second
/// transport exists.
/// </summary>
public sealed class SteamTransport
{
	private const int MaxMessagesPerPoll = 32;

	private readonly ILogger _log = LogBridge.Log;
	private readonly IntPtr[] _receiveBuffer = new IntPtr[MaxMessagesPerPoll];

	/// <summary>Raised on the Unity main thread via <see cref="Poll"/>.</summary>
	public event Action<ulong, byte[]>? MessageReceived;

	public bool SendTo(ulong steamId, byte[] data, bool reliable)
	{
		var identity = new SteamNetworkingIdentity();
		identity.SetSteamID64(steamId);

		var flags = reliable
			? Constants.k_nSteamNetworkingSend_Reliable
			: Constants.k_nSteamNetworkingSend_Unreliable;

		unsafe
		{
			fixed (byte* pData = data)
			{
				var result = SteamNetworkingMessages.SendMessageToUser(
					ref identity, (IntPtr)pData, (uint)data.Length, flags, 0);
				if (result != EResult.k_EResultOK)
				{
					_log.Warning($"SendMessageToUser to {steamId} failed: {result}");
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>Drains incoming messages. Must run on the Unity main thread each frame.</summary>
	public void Poll()
	{
		int count;
		while ((count = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, _receiveBuffer, _receiveBuffer.Length)) > 0)
		{
			for (var i = 0; i < count; i++)
			{
				HandleMessage(_receiveBuffer[i]);
				SteamNetworkingMessage_t.Release(_receiveBuffer[i]);
			}
		}
	}

	private void HandleMessage(IntPtr messagePtr)
	{
		var message = SteamNetworkingMessage_t.FromIntPtr(messagePtr);

		var data = new byte[message.m_cbSize];
		Marshal.Copy(message.m_pData, data, 0, message.m_cbSize);

		var sender = message.m_identityPeer.GetSteamID64();
		MessageReceived?.Invoke(sender, data);
	}
}
