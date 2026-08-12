using System;
using System.Runtime.InteropServices;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// MVP transport over ISteamNetworkingMessages (reliable + unreliable
/// messages, no connection handles). Single channel (0) for now; the
/// INetworkTransport surface it implements is shared with the test suite's
/// FakeTransport (the second transport the abstraction was waiting for).
/// </summary>
public sealed class SteamTransport(ISteamService steam, ILogger<SteamTransport> log) : ICuoService, INetworkTransport
{
	private const int MaxMessagesPerPoll = 32;

	private readonly ISteamService _steam = steam;
	private readonly ILogger<SteamTransport> _log = log;
	private readonly IntPtr[] _receiveBuffer = new IntPtr[MaxMessagesPerPoll];

	/// <summary>Raised on the Unity main thread via <see cref="Poll"/>.</summary>
	public event Action<ulong, byte[]>? MessageReceived;

	public bool SendTo(ulong steamId, byte[] data, bool reliable)
	{
		if (!_steam.IsInitialized)
		{
			return false;
		}

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
					LogSendDiagnostics(steamId, ref identity, result);
					return false;
				}
			}
		}

		return true;
	}

	// Diagnose why a P2P send failed: session connection state, end reason and
	// debug string, plus the Steam Datagram Relay (SDR) availability.
	private void LogSendDiagnostics(ulong steamId, ref SteamNetworkingIdentity identity, EResult result)
	{
		var state = SteamNetworkingMessages.GetSessionConnectionInfo(
			ref identity, out var info, out _);

		_log.LogWarning(
			$"SendMessageToUser to {steamId} failed: {result}; " +
			$"session state: {state}, end reason: {info.m_eEndReason}, debug: \"{info.m_szEndDebug}\"");

		if (SteamNetworkingUtils.GetRelayNetworkStatus(out var relay) == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current)
		{
			_log.LogWarning($"SDR: any-relay avail: {relay.m_eAvailAnyRelay}, network-config avail: {relay.m_eAvailNetworkConfig}, debug: \"{relay.m_debugMsg}\"");
		}
	}

	/// <summary>Drains incoming messages. Must run on the Unity main thread each frame.</summary>
	public void Poll()
	{
		if (!_steam.IsInitialized)
		{
			return;
		}

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

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update() => Poll();

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
