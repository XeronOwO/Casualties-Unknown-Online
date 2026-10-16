using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Run;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The dead player's camera, using the game's own mechanisms and nothing else: <c>PlayerCamera.following</c> is
/// the follow target (<c>PlayerCamera.cs</c> reads its position whenever it is set), <c>PlayerCamera.isFreecam</c>
/// is the free camera, and the death screen is hidden and restored through <c>PlayerCamera.endScreen</c> with
/// <c>PlayerCamera.didDeathScreen</c> respected — all four are public fields on the game's own camera, so there
/// is no CUO camera, no CUO death screen and no new window.
///
/// <para>
/// It runs on EVERY client and decides from its own facts: the local body's own liveness, the member rows the
/// session already publishes, and the host's spectator rules. A member's body here is its display clone, which
/// this client already renders — following it reads nothing it was not already sent.
/// </para>
///
/// <para>
/// The exits are the reason the release path is not "just clear the field": the game's own
/// <c>HandleDeathScreen</c> re-arms itself when the body is alive again (<c>PlayerCamera.cs</c>, the second
/// <c>if</c> in that method), so a revive is native — but a player who stops watching while still dead has to
/// get the screen back, and that only happens when the death screen was already latched.
/// </para>
/// </summary>
internal sealed class SpectatorCameraService(
	RunCoordinator run,
	RemotePlayerRenderer renderer,
	IHostRules hostRules,
	ISessionControl session,
	IEntitySyncControl entities,
	ILogger<SpectatorCameraService> log) : ISpectatorControl, ISessionReset
{
	/// <summary>Subscribes the teardown this service owes the session: a session that ends while someone is watching must not leave that state alive for the next one.</summary>
	internal void BindToSession() => session.SessionEnded += ResetSessionState;

	internal void Unbind() => session.SessionEnded -= ResetSessionState;

	/// <inheritdoc />
	public void ResetSessionState()
	{
		if (!IsWatching && !_touchedCamera)
		{
			return;
		}

		log.LogInformation("[Spectator] session ended while watching {SteamId}; the camera state is dropped.", _watchingSteamId);
		_watchingSteamId = 0;
		_freeFlyWanted = false;
		_touchedCamera = false;
	}

	private ulong _watchingSteamId;

	/// <summary>Whether this client's camera is currently carrying CUO''s follow state. Released only after it was taken: the free-camera field has a second owner in the game itself.</summary>
	private bool _touchedCamera;

	/// <summary>Whether this client asked for the free camera. The RULE decides whether it may have it; this is the player's own choice inside that.</summary>
	private bool _freeFlyWanted;

	/// <inheritdoc />
	public bool IsWatching => _watchingSteamId != 0;

	/// <inheritdoc />
	public bool TryWatch(ulong steamId)
	{
		if (!MayWatch(out var localBody) || steamId == 0 || steamId == session.LocalSteamId)
		{
			return false;
		}

		if (!TryResolveLiving(steamId, out _))
		{
			return false;
		}

		_watchingSteamId = steamId;
		_freeFlyWanted = false;
		log.LogInformation("[Spectator] following {SteamId} (local body {Local} is {State}).",
			steamId, session.LocalSteamId, Describe(localBody));
		return true;
	}

	/// <inheritdoc />
	public void StopWatching()
	{
		if (!IsWatching)
		{
			return;
		}

		log.LogInformation("[Spectator] stopped following {SteamId}.", _watchingSteamId);
		_watchingSteamId = 0;
		_freeFlyWanted = false;
	}

	/// <inheritdoc />
	public bool TrySetFreeFly(bool wanted)
	{
		if (!IsWatching)
		{
			log.LogInformation("[Spectator] free-fly {State} refused: this client is not watching anyone.", wanted ? "on" : "off");
			return false;
		}

		if (wanted && !hostRules.SpectatorFreeFly)
		{
			log.LogInformation("[Spectator] free-fly refused: the host has SpectatorFreeFly off.");
			return false;
		}

		log.LogInformation("[Spectator] free-fly {State} by this client's own request.", wanted ? "on" : "off");
		_freeFlyWanted = wanted;
		return true;
	}

	/// <summary>
	/// One frame of the camera's own state. Called from the adapter's pump, so it runs on every client and only
	/// touches this client's camera.
	/// </summary>
	internal void Update()
	{
		var camera = PlayerCamera.main;
		if (camera == null)
		{
			return;
		}

		if (!IsWatching || !MayWatch(out _))
		{
			Release(camera);
			return;
		}

		// The followed member died, left the layer or dropped — the case the ticket insists must not leave the
		// camera nowhere. The choice of who to watch next belongs to the player (the member list is the chooser
		// they already have), so this reports it and gives the screen back rather than picking for them.
		if (!TryResolveLiving(_watchingSteamId, out var target))
		{
			log.LogInformation("[Spectator] {SteamId} is no longer followable here (dead, out of the world or dropped); back to the death screen.", _watchingSteamId);
			StopWatching();
			Release(camera);
			return;
		}

		camera.following = target.transform;

		// The free camera is given a position the first frame it is asked for: the game's own /freecam command
		// seeds it from wherever the camera is, and without that the follow target is dropped and the view jumps
		// to whatever freecamPos last held — the layer origin on a fresh session.
		var fly = _freeFlyWanted && hostRules.SpectatorFreeFly;
		if (fly && !camera.isFreecam && ConsoleScript.instance != null) // Unity object — ==
		{
			ConsoleScript.instance.freecamPos = camera.transform.position;
		}

		camera.isFreecam = fly;
		_touchedCamera = true;

		// The death screen is the entry point, so it is hidden only while a follow target is actually set.
		if (camera.endScreen != null && camera.endScreen.gameObject.activeSelf) // Unity object — ==
		{
			camera.endScreen.gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// Whether this client's local character is in a state that may watch. The rule decides the unconscious case:
	/// an unconscious body is still its owner's to act on, so watching then changes a live player's client.
	/// </summary>
	private bool MayWatch(out Body? localBody)
	{
		localBody = run.LocalBody;
		if (localBody == null || !session.SessionActive) // Unity object — ==
		{
			return false;
		}

		return !localBody.alive || (hostRules.SpectateWhileUnconscious && !localBody.conscious);
	}

	/// <summary>
	/// The body to follow, or false when there is nobody to follow. The clone's OWN <c>alive</c> is its brain
	/// health, which CUO never writes — asking the clone whether a teammate is alive answers with the local
	/// template's value and would keep the camera on a corpse forever. The authoritative answer is the one the
	/// session already publishes for that member.
	/// </summary>
	private bool TryResolveLiving(ulong steamId, out Body target)
	{
		target = null!;
		if (!renderer.TryGetRemoteBody(steamId, out var body) || body == null) // Unity object — ==
		{
			return false;
		}

		if (entities.GetRemotePlayer(steamId) is not { Alive: true })
		{
			return false;
		}

		target = body;
		return true;
	}

	/// <summary>
	/// Give the camera back: no follow target, no free camera, and the death screen restored if the game latched
	/// it. It gives back only what it took (<see cref="_touchedCamera"/>): <c>isFreecam</c> has a second owner in
	/// the game's own <c>/freecam</c> command, and clearing it on every frame of every session would switch that
	/// command off for everyone.
	/// </summary>
	private void Release(PlayerCamera camera)
	{
		if (!_touchedCamera)
		{
			return;
		}

		_touchedCamera = false;
		if (camera.following != null) // Unity object — ==
		{
			camera.following = null;
		}

		camera.isFreecam = false;
		if (camera.endScreen != null // Unity object — ==
			&& camera.didDeathScreen
			&& !camera.endScreen.gameObject.activeSelf)
		{
			camera.endScreen.gameObject.SetActive(true);
		}
	}

	private static string Describe(Body? body) =>
		body == null ? "gone" : body.alive ? "alive" : body.conscious ? "dead" : "dead and unconscious";
}
