using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
	private float _lastRttMs = -1f;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibrary(string lpFileName);

	// Loads steam_api64.dll from this plugin's folder so DllImport in
	// Steamworks.NET resolves it (DllImport only searches the exe dir,
	// system dirs and PATH by default). Must run before any Steam call.
	private static void PreloadNativeLibrary()
	{
		try
		{
			var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var path = Path.Combine(dir ?? "", "steam_api64.dll");
			if (LoadLibrary(path) == IntPtr.Zero)
				Logger.LogWarning($"CUO: LoadLibrary failed for {path} (Win32 error {Marshal.GetLastWin32Error()})");
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"CUO: native library preload failed: {ex.Message}");
		}
	}

	private void Awake()
	{
		// Plugin startup logic
		Logger = base.Logger;
		PreloadNativeLibrary();
		LogBridge.Initialize(new CompositeLogger(
			new BepInExLogSink(Logger),
			new FileLogger(Path.Combine(Paths.BepInExRootPath, "CUO.log"))));

		try
		{
			_targetLobbyId = Config.Bind("Session", "TargetLobbyId", "",
				"Lobby ID to join with F9 (printed by the host on F8). Leave empty to host only.");
			_steam = new SteamService();
			_steam.LobbyCreated += lobbyId => LogBridge.Log.Info($"Lobby created: {lobbyId}");
			_steam.LobbyEntered += lobbyId => LogBridge.Log.Info($"Lobby entered: {lobbyId}");

			_transport = new SteamTransport();
			_transport.MessageReceived += OnMessageReceived;

			LogBridge.Log.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
			LogBridge.Log.Info("CUO Phase 0 test keys: F8 = init Steam + create lobby, F9 = join lobby from config, F10 = ping first peer.");
		}
		catch (Exception ex)
		{
			LogBridge.Log.Error($"CUO startup failed: {ex}");
		}
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
				_lastRttMs = (DateTime.UtcNow.Ticks - sentTicks) / 10_000.0f;
				Logger.LogInfo($"CUO: pong from {sender} — RTT {_lastRttMs:F1} ms");
				break;
		}
	}

	// Phase-0 test HUD (IMGUI, temporary): replace with real UI in later phases.
	private void OnGUI()
	{
		var y = 10f;
		Line("CUO Phase 0 — Steam: " + (_steam?.IsInitialized == true ? "initialized" : "not initialized"));
		if (_steam?.IsInitialized == true)
		{
			Line($"SteamID: {_steam.LocalSteamId}");
			Line($"Lobby: {_steam.CurrentLobbyId}  Members: {_steam.GetLobbyMembers().Length}");
		}

		Line(_lastRttMs >= 0f ? $"Last RTT: {_lastRttMs:F1} ms" : "No ping yet");
		Line("F8 create lobby / F9 join from config / F10 ping peer");

		void Line(string text) => GUI.Label(new Rect(10f, y, 900f, 20f), text);
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
