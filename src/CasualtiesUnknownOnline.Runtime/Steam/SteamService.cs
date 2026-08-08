using System;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace CasualtiesUnknownOnline.Runtime.Steam;

/// <summary>
/// Owns the Steam client API lifecycle for CUO. The game itself has no Steam
/// integration (verified by reversing Assembly-CSharp: no SteamAPI usage), so
/// CUO is the sole initializer — no duplicate-init conflicts to worry about.
/// </summary>
/// <remarks>
/// Steam APIs are encapsulated here; consumers only see <see cref="ulong"/>
/// lobby/SteamIDs, never Steamworks types (abstraction rule in architecture.md).
/// </remarks>
public sealed class SteamService(ILogger<SteamService> log) : ICuoService
{
	private readonly ILogger<SteamService> _log = log;
	private Callback<LobbyCreated_t>? _lobbyCreated;
	private Callback<LobbyEnter_t>? _lobbyEntered;
	private Callback<GameLobbyJoinRequested_t>? _joinRequested;

	public bool IsInitialized { get; private set; }

	public ulong LocalSteamId { get; private set; }

	/// <summary>Lobby this client currently hosts or joined, or 0 when none.</summary>
	public ulong CurrentLobbyId { get; private set; }

	/// <summary>SteamIDs of all current lobby members (including self), empty when not in a lobby.</summary>
	public ulong[] GetLobbyMembers()
	{
		if (CurrentLobbyId == 0)
		{
			return [];
		}

		var lobby = new CSteamID(CurrentLobbyId);
		var count = SteamMatchmaking.GetNumLobbyMembers(lobby);
		var members = new ulong[count];
		for (var i = 0; i < count; i++)
		{
			members[i] = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).m_SteamID;
		}

		return members;
	}

	/// <summary>Raised on the Unity main thread via <see cref="RunCallbacks"/>.</summary>
	public event Action<ulong>? LobbyCreated;

	public event Action<ulong>? LobbyEntered;

	/// <summary>Raised when a lobby join attempt fails (lobby gone, full, ...).
	/// The reason is a human-readable description — Steamworks types never
	/// leave this class (abstraction rule in architecture.md).</summary>
	public event Action<ulong, string>? LobbyJoinFailed;

	public event Action<ulong>? JoinRequested;

	public bool Initialize()
	{
		if (IsInitialized)
		{
			return true;
		}

		if (!Packsize.Test())
		{
			_log.LogError("Steamworks Packsize test failed — wrong Steamworks.NET platform build.");
			return false;
		}

		if (!DllCheck.Test())
		{
			_log.LogError("Steamworks DllCheck failed — steam_api64.dll version mismatch.");
			return false;
		}

		if (SteamAPI.InitEx(out var initError) != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
		{
			_log.LogError($"SteamAPI.InitEx failed: {initError}");
			return false;
		}

		IsInitialized = true;
		LocalSteamId = SteamUser.GetSteamID().m_SteamID;
		var appId = SteamUtils.GetAppID().m_AppId;
		_log.LogInformation($"Steam initialized. AppID: {appId}, SteamID: {LocalSteamId} ({SteamFriends.GetPersonaName()})");

		_lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
		_lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
		_joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
		return true;
	}

	/// <summary>Pumps Steam callbacks. Must be called on the Unity main thread every frame.</summary>
	public void RunCallbacks()
	{
		// Steamworks.NET throws "Callback dispatcher is not initialized" when
		// called before SteamAPI.Init — guard so the per-frame pump is safe.
		if (!IsInitialized)
		{
			return;
		}

		SteamAPI.RunCallbacks();
	}

	public void CreateLobby(int maxMembers = 8)
	{
		_log.LogInformation("Requesting lobby creation...");
		SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxMembers);
	}

	public void JoinLobby(ulong lobbyId)
	{
		_log.LogInformation($"Requesting join of lobby {lobbyId}...");
		SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
	}

	private void OnLobbyCreated(LobbyCreated_t callback)
	{
		if (callback.m_eResult == EResult.k_EResultOK)
		{
			var lobbyId = callback.m_ulSteamIDLobby;
			CurrentLobbyId = lobbyId;
			_log.LogInformation($"Lobby created: {lobbyId}");
			LobbyCreated?.Invoke(lobbyId);
		}
		else
		{
			_log.LogError($"Lobby creation failed: {callback.m_eResult}");
		}
	}

	private void OnLobbyEntered(LobbyEnter_t callback)
	{
		var lobbyId = callback.m_ulSteamIDLobby;
		// Steamworks.NET ships this field as uint (not the enum) — cast once.
		var response = (EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse;
		if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
		{
			// Failed joins still arrive as LobbyEnter_t — the response code
			// carries the reason. Never fake an entered lobby on failure
			// (SessionService would start a handshake against a dead lobby).
			var reason = DescribeJoinFailure(response);
			_log.LogError($"Failed to join lobby {lobbyId}: {reason}");
			LobbyJoinFailed?.Invoke(lobbyId, reason);
			return;
		}

		CurrentLobbyId = lobbyId;
		_log.LogInformation($"Entered lobby: {lobbyId}");
		LobbyEntered?.Invoke(lobbyId);
	}

	private static string DescribeJoinFailure(EChatRoomEnterResponse response) =>
		response switch
		{
			EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist => "lobby does not exist (host may have left)",
			EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed => "not allowed to join this lobby",
			EChatRoomEnterResponse.k_EChatRoomEnterResponseFull => "lobby is full",
			EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned => "banned from this lobby",
			EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited => "account is limited and cannot join",
			EChatRoomEnterResponse.k_EChatRoomEnterResponseCommunityBan => "a community ban prevents joining",
			EChatRoomEnterResponse.k_EChatRoomEnterResponseMemberBlockedYou => "the host has blocked you",
			EChatRoomEnterResponse.k_EChatRoomEnterResponseYouBlockedMember => "you blocked the host",
			EChatRoomEnterResponse.k_EChatRoomEnterResponseRatelimitExceeded => "join rate limit exceeded",
			_ => $"error ({response})",
		};

	private void OnJoinRequested(GameLobbyJoinRequested_t callback)
	{
		var lobbyId = callback.m_steamIDLobby.m_SteamID;
		_log.LogInformation($"Join requested for lobby {lobbyId}");
		JoinRequested?.Invoke(lobbyId);
	}

	public void Dispose()
	{
		_lobbyCreated?.Dispose();
		_lobbyEntered?.Dispose();
		_joinRequested?.Dispose();

		if (IsInitialized)
		{
			SteamAPI.Shutdown();
			IsInitialized = false;
			_log.LogInformation("Steam API shut down.");
		}
	}

	void ICuoService.Initialize() => Initialize();

	void ICuoService.Start()
	{
	}

	void ICuoService.Update() => RunCallbacks();

	void ICuoService.Stop()
	{
	}

	void ICuoService.Dispose() => Dispose();
}
