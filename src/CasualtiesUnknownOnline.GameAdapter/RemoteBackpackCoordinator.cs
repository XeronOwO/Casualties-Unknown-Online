using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The Online UI bridge to the native backpack view: resolves a remote
/// SteamId to the live render clone and opens the game's radial inventory
/// focused on that clone. The actual UI patches read
/// <see cref="RemoteBackpackView"/>; this coordinator is the only layer that
/// knows how to find a remote Body from the session.
/// </summary>
internal sealed class RemoteBackpackCoordinator(
	ISessionControl session,
	RemotePlayerRenderer renderer,
	ILogger<RemoteBackpackCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly RemotePlayerRenderer _renderer = renderer;
	private readonly ILogger<RemoteBackpackCoordinator> _log = log;

	internal bool Open(ulong steamId, string displayName)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return false;
		}

		if (!_renderer.TryGetRemoteBody(steamId, out var body) || body == null) // Unity object — ==
		{
			_log.LogWarning("[BackpackView] cannot open remote backpack for {SteamId}: no render clone yet.", steamId);
			return false;
		}

		RemoteBackpackView.Open(body, steamId, displayName);
		_log.LogInformation("[BackpackView] opened native backpack view for {SteamId} ({Name}).", steamId, displayName);
		return true;
	}

	internal void Close() => RemoteBackpackView.Close();

	internal void Update() => RemoteBackpackView.ClearIfStale();
}
