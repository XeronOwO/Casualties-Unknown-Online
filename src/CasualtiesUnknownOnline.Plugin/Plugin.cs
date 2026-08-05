using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Core.Logging;
using CasualtiesUnknownOnline.Core.Steam;
using UnityEngine;

namespace CasualtiesUnknownOnline;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("CasualtiesUnknown.exe")]
public class Plugin : BaseUnityPlugin
{
	internal static new ManualLogSource Logger = null!;

	private ConfigEntry<string> _targetLobbyId = null!;
	private SteamService? _steam;

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

		Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
		Logger.LogInfo("CUO Phase 0 test keys: F8 = init Steam + create lobby, F9 = join lobby from config.");
	}

	private void Update()
	{
		_steam?.RunCallbacks();

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
	}

	private void OnDestroy()
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
