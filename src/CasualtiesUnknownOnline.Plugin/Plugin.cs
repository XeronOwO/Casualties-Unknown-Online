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

	private const float AutoPingIntervalSeconds = 5f;

	private ConfigEntry<string> _targetLobbyId = null!;
	private SteamService? _steam;
	private SteamTransport? _transport;
	private float _lastRttMs = -1f;
	private float _nextAutoPingTime;

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
			// Multiplayer games must keep running when the window loses focus.
			Application.runInBackground = true;

			_targetLobbyId = Config.Bind("Session", "TargetLobbyId", "",
				"Lobby ID to join with F9 (printed by the host on F8). Leave empty to host only.");
			_steam = new SteamService();
			_steam.LobbyCreated += lobbyId => LogBridge.Log.Info($"Lobby created: {lobbyId}");
			_steam.LobbyEntered += lobbyId => LogBridge.Log.Info($"Lobby entered: {lobbyId}");

			_transport = new SteamTransport();
			_transport.MessageReceived += OnMessageReceived;

			// Forward Unity log messages into CUO's own log so runtime errors
			// (which BepInEx's DiskLogListener may not capture) are visible.
			Application.logMessageReceived += OnUnityLogMessage;

			LogBridge.Log.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

			// Initialize Steam at load time (same as KrokMP's CheckSteam on
			// startup): the lobby UI later keys off IsInitialized.
			if (_steam.Initialize())
			{
				_transport.IsSteamInitialized = true;
				LogBridge.Log.Info("CUO Phase 0 test keys: F8 = create lobby, F9 = join lobby from config, F10 = ping first peer.");
			}
			else
			{
				LogBridge.Log.Warning("CUO: Steam not initialized — lobby features unavailable. F8 can retry.");
			}
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

		// Phase-0 auto-ping: while in a lobby with a peer, ping every few
		// seconds without requiring key input (window focus breaks Input
		// during dual-instance testing). Keeps connection diagnostics flowing.
		if (_steam?.IsInitialized == true && _steam.CurrentLobbyId != 0
			&& Time.unscaledTime >= _nextAutoPingTime)
		{
			_nextAutoPingTime = Time.unscaledTime + AutoPingIntervalSeconds;
			if (_steam.GetLobbyMembers().Length > 1)
				SendPing();
		}

		if (_steam is { } steam)
		{
			if (Input.GetKeyDown(KeyCode.F8))
			{
				// Retry path: if load-time init failed (Steam not running yet),
				// F8 re-attempts initialization, then creates the lobby.
				if (EnsureSteamReady(steam))
					steam.CreateLobby();
			}
			else if (Input.GetKeyDown(KeyCode.F9))
			{
				if (EnsureSteamReady(steam) && ulong.TryParse(_targetLobbyId.Value, out var lobbyId))
					steam.JoinLobby(lobbyId);
			}
			else if (Input.GetKeyDown(KeyCode.F10))
			{
				SendPing();
			}
		}
	}

	private bool EnsureSteamReady(SteamService steam)
	{
		if (!steam.Initialize())
			return false;

		_transport!.IsSteamInitialized = true;
		return true;
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

		void Line(string text)
		{
			GUI.Label(new Rect(10f, y, 900f, 20f), text);
			y += 20f;
		}
	}

	private void OnUnityLogMessage(string message, string stackTrace, LogType type)
	{
		switch (type)
		{
			case LogType.Error:
			case LogType.Exception:
			case LogType.Assert:
				LogBridge.Log.Error($"[Unity:{type}] {message}\n{stackTrace}");
				break;
			case LogType.Warning:
				LogBridge.Log.Warning($"[Unity] {message}");
				break;
		}
	}

	// SteamManager guidance: never do Steamworks work in OnDestroy (execution
	// order is not guaranteed); OnDisable is the safe teardown point.
	private void OnDisable()
	{
		Application.logMessageReceived -= OnUnityLogMessage;
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
