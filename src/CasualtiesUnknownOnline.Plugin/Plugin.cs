using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Core.Logging;
using CasualtiesUnknownOnline.Core.Networking;
using CasualtiesUnknownOnline.Core.Steam;
using UnityEngine;

namespace CasualtiesUnknownOnline;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("CasualtiesUnknown.exe")]
public class Plugin : BaseUnityPlugin
{
	private const byte MsgPing = 0;
	private const byte MsgPong = 1;

	internal static new ManualLogSource Logger = null!;

	private ConfigEntry<string> _targetLobbyId = null!;
	private SteamService? _steam;
	private SteamTransport? _transport;

	private void Awake()
	{
		// Plugin startup logic
		Logger = base.Logger;
		LogBridge.Initialize(new BepInExLogSink(Logger));

		_targetLobbyId = Config.Bind("Session", "TargetLobbyId", "",
			"Lobby ID to join with F9 (printed by the host on F8). Leave empty to host only.");
		_steam = new SteamService();
		_steam.LobbyCreated += lobbyId => Logger.LogInfo($"CUO: lobby created: {lobbyId}");
		_steam.LobbyEntered += lobbyId => Logger.LogInfo($"CUO: lobby entered: {lobbyId}");

		_transport = new SteamTransport();
		_transport.MessageReceived += OnMessageReceived;

		Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
		Logger.LogInfo("CUO Phase 0 test keys: F8 = init Steam + create lobby, F9 = join lobby from config, F10 = ping first peer.");
	}

	private void Update()
	{
		_steam?.RunCallbacks();
		_transport?.Poll();

		if (Input.GetKeyDown(KeyCode.F8))
		{
			if (_steam is { } steam && steam.Initialize())
				steam.CreateLobby();
		}
		else if (Input.GetKeyDown(KeyCode.F9))
		{
			if (_steam is { } steam && steam.Initialize() && ulong.TryParse(_targetLobbyId.Value, out var lobbyId))
				steam.JoinLobby(lobbyId);
		}
		else if (Input.GetKeyDown(KeyCode.F10))
		{
			SendPing();
		}
	}

	private void SendPing()
	{
		if (_steam is not { IsInitialized: true } steam)
		{
			Logger.LogWarning("CUO: Steam not initialized — press F8 first.");
			return;
		}

		var target = steam.GetLobbyMembers().FirstOrDefault(m => m != steam.LocalSteamId);
		if (target == 0)
		{
			Logger.LogWarning("CUO: no peer in the lobby to ping.");
			return;
		}

		var payload = new byte[9];
		payload[0] = MsgPing;
		BitConverter.GetBytes(DateTime.UtcNow.Ticks).CopyTo(payload, 1);
		var sent = _transport!.SendTo(target, payload, reliable: true);
		Logger.LogInfo(sent ? $"CUO: ping -> {target}" : $"CUO: ping to {target} FAILED");
	}

	private void OnMessageReceived(ulong sender, byte[] data)
	{
		if (data.Length == 0)
			return;

		switch (data[0])
		{
			case MsgPing:
				_transport!.SendTo(sender, new[] { MsgPong }, reliable: true);
				Logger.LogInfo($"CUO: ping from {sender} — pong sent.");
				break;

			case MsgPong when data.Length >= 9:
				var sentTicks = BitConverter.ToInt64(data, 1);
				var rttMs = (DateTime.UtcNow.Ticks - sentTicks) / 10_000.0;
				Logger.LogInfo($"CUO: pong from {sender} — RTT {rttMs:F1} ms");
				break;
		}
	}

	// SteamManager guidance: never do Steamworks work in OnDestroy (execution
	// order is not guaranteed); OnDisable is the safe teardown point.
	private void OnDisable()
	{
		_steam?.Dispose();
	}

	/// <summary>Bridges CUO Core's ILogger to BepInEx logging.</summary>
	private sealed class BepInExLogSink : Core.Logging.ILogger
	{
		private readonly ManualLogSource _source;

		public BepInExLogSink(ManualLogSource source) => _source = source;

		public void Info(string message) => _source.LogInfo(message);

		public void Warning(string message) => _source.LogWarning(message);

		public void Error(string message) => _source.LogError(message);
	}
}
