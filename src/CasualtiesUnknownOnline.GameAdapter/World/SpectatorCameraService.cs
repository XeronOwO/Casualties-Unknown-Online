using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Run;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
	ILocalizationService localization,
	IWorldControl world,
	ILogger<SpectatorCameraService> log) : ISpectatorControl, ISessionReset
{
	/// <summary>Subscribes the teardown this service owes the session: a session that ends while someone is watching must not leave that state alive for the next one.</summary>
	internal void BindToSession() => session.SessionEnded += ResetSessionState;

	internal void Unbind() => session.SessionEnded -= ResetSessionState;

	/// <inheritdoc />
	public void ResetSessionState()
	{
		if (!IsWatching && !IsSpectator && !_touchedCamera)
		{
			return;
		}

		log.LogInformation("[Spectator] session ended while watching {SteamId}; the camera state is dropped.", _watchingSteamId);
		_watchingSteamId = 0;
		_spectatorMode = false;
		SpectatorPresentationGate.Set(false);
		_freeFlyWanted = false;
		_touchedCamera = false;
	}

	/// <summary>Whether this client is in the spectator MODE (the death screen's button or the cheat verb).</summary>
	private bool _spectatorMode;

	/// <inheritdoc />
	public bool IsSpectator => _spectatorMode;

	/// <inheritdoc />
	public bool IsFreeFly => _freeFlyWanted;

	/// <inheritdoc />
	public bool EnterSpectatorMode()
	{
		if (_spectatorMode)
		{
			return true;
		}

		if (!session.SessionActive || !session.LocalInWorld || run.LocalBody == null) // Unity object 鈥?==
		{
			return false;
		}

		_spectatorMode = true;
		_enteredWhileDead = run.LocalBody != null && !run.LocalBody.alive; // Unity object — ==
		SpectatorPresentationGate.Set(true);
		log.LogInformation("[Spectator] spectator mode on (local body {Local}).", session.LocalSteamId);
		return true;
	}

	/// <inheritdoc />
	public void ExitSpectatorMode()
	{
		if (!_spectatorMode)
		{
			return;
		}

		_spectatorMode = false;
		SpectatorPresentationGate.Set(false);
		StopWatching();
		_freeFlyWanted = false;
		log.LogInformation("[Spectator] spectator mode off; normal play.");
	}


	private ulong _watchingSteamId;

	/// <summary>Whether this client's camera is currently carrying CUO's follow state. Released only after it was taken: the free-camera field has a second owner in the game itself.</summary>
	private bool _touchedCamera;

	/// <summary>Whether this client asked for the free camera. The RULE decides whether it may have it; this is the player's own choice inside that.</summary>
	private bool _freeFlyWanted;

	/// <inheritdoc />
	public bool IsWatching => _watchingSteamId != 0;

	/// <inheritdoc />
	public ulong WatchingSteamId => _watchingSteamId;

	/// <inheritdoc />
	public bool TryWatch(ulong steamId)
	{
		// Possession is a spectator's act (the ruling: the right-click menu offers it only in spectator mode, no
		// distance or reachability check). The old dead-only gate is gone: a spectator whose body is a passive
		// target in the world may watch anyone whose body this client renders.
		if (!_spectatorMode || steamId == 0 || steamId == session.LocalSteamId)
		{
			return false;
		}

		if (!TryResolveAnyBody(steamId, out _))
		{
			return false;
		}

		_watchingSteamId = steamId;
		_freeFlyWanted = false;
		// The watched player is TOLD (the ruling): the host relays this to that member alone.
		world.SendSpectatorNotice(new SpectatorNoticeMsg
		{
			WatcherSteamId = session.LocalSteamId,
			TargetSteamId = steamId,
			Watching = true,
		});
		log.LogInformation("[Spectator] following {SteamId} (local body {Local} is {State}).",
			steamId, session.LocalSteamId, Describe(run.LocalBody));
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
		// The watched player is told it ended, by the same targeted path the start took.
		world.SendSpectatorNotice(new SpectatorNoticeMsg
		{
			WatcherSteamId = session.LocalSteamId,
			TargetSteamId = _watchingSteamId,
			Watching = false,
		});
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

		// No host rule any more: the user's ruling made free movement a capability OF THE MODE, and the three
		// spectator switches are superseded by the cheat-gated verb and the receipt's button.
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

		// The ruling: a revived player returns to normal play IMMEDIATELY, whichever revive path woke them. Only a
		// mode entered from the death screen ends this way — a spectator's body staying ALIVE is the ruling's own
		// shape (the body remains in the world), so "alive" alone is not a revive; the transition from the death
		// state is what this watches, recorded at entry.
		if (_spectatorMode && _enteredWhileDead && run.LocalBody != null && run.LocalBody.alive) // Unity objects — ==
		{
			log.LogInformation("[Spectator] the body that died is alive again; leaving spectator mode.");
			ExitSpectatorMode();
		}

		// The death screen's own menu-return button is the mode's second entrance (the user's ruling): a dead
		// player on the receipt may watch instead of leaving the run.
		if (!run.LocalBody?.alive ?? false) // Unity object — ==
		{
			RelabelReceiptMenuButton(camera);
		}

		// The watch lives and dies with the MODE: leaving spectator mode (a revive or the verb) releases the camera,
		// and a followed member who dies, leaves or drops gives it up as well (the choice of who to watch next
		// belongs to the player).
		if (!_spectatorMode || !IsWatching)
		{
			Release(camera);
			return;
		}

		// The followed member died, left the layer or dropped — the case the ticket insists must not leave the
		// camera nowhere. The choice of who to watch next belongs to the player (the member list is the chooser
		// they already have), so this reports it and gives the screen back rather than picking for them.
		if (!TryResolveAnyBody(_watchingSteamId, out var target))
		{
			log.LogInformation("[Spectator] {SteamId} is no longer followable here (dead, out of the world or dropped); back to the death screen.", _watchingSteamId);
			StopWatching();
			Release(camera);
			return;
		}

		camera.following = target.transform;

		// The watched body's own screen state, mirrored for the effects patch: a body that is out (unconscious or
		// dead) blacks the screen, and a body bleeding out reddens the vignette — the spectator sees what the player
		// they follow is living through instead of their own standing body's clean screen.
		SpectatorPresentationGate.SetWatched(
			target.alive && !target.conscious ? UnconsciousBlackAmount : 0f,
			OpacityFor(BleedVignette, target.bloodVolume));

		// The free camera is given a position the first frame it is asked for: the game's own /freecam command
		// seeds it from wherever the camera is, and without that the follow target is dropped and the view jumps
		// to whatever freecamPos last held — the layer origin on a fresh session.
		var fly = _freeFlyWanted;
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
	/// The ruling's visual half on the receipt's menu button: its label becomes the spectator wording. The button's
	/// ACTION is not touched here — a scene's persistent listener cannot be removed at runtime, so the mode switch
	/// is a Harmony prefix on <c>PlayerCamera.ToMainMenu</c> instead (<see cref="Patches.PlayerCameraToMainMenuPatch"/>),
	/// and this only makes the button say what it now does.
	/// </summary>
	private void RelabelReceiptMenuButton(PlayerCamera camera)
	{
		if (camera.endScreen == null || !camera.endScreen.gameObject.activeSelf) // Unity object — ==
		{
			_receiptButtonRelabelled = false;
			return;
		}

		if (_receiptButtonRelabelled)
		{
			return;
		}

		foreach (var button in camera.endScreen.transform.root.GetComponentsInChildren<Button>(includeInactive: true))
		{
			for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
			{
				var method = button.onClick.GetPersistentMethodName(i);
				if (!string.Equals(method, "ToMainMenu", StringComparison.Ordinal)
					&& !string.Equals(method, "SaveAndExit", StringComparison.Ordinal))
				{
					continue;
				}

				var label = button.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
				if (label != null) // Unity object — ==
				{
					label.text = localization.T("spectator.receipt_enter");
				}

				_receiptButtonRelabelled = true;
				log.LogInformation("[Spectator] the death receipt's {Method} button is labelled for spectator mode.", method);
				return;
			}
		}
	}

	/// <summary>Whether the receipt's menu button was already relabelled for the current death screen.</summary>
	private bool _receiptButtonRelabelled;

	/// <summary>Whether the mode was entered from a DEAD body: only that mode ends when the body comes back to life
	/// (a spectator whose body was alive at entry is already in the ruling's shape and leaves by the verb).</summary>
	private bool _enteredWhileDead;

	/// <summary>
	/// Whether this client's local character is in a state that may watch. The terms are the SHARED rule's
	/// (<see cref="SpectatorPolicy"/>), which the member card reads too: a client-side copy of them is how the
	/// card once stopped reading the host's unconscious switch without anything failing.
	/// </summary>
	private bool MayWatch(out Body? localBody)
	{
		localBody = run.LocalBody;
		if (localBody == null) // Unity object — ==
		{
			return false;
		}

		return SpectatorPolicy.MayWatch(localBody.alive, localBody.conscious, hostRules.SpectateWhileUnconscious, session.SessionActive);
	}

	/// <summary>
	/// The body to follow, or false when there is nobody to follow. The clone's OWN <c>alive</c> is its brain
	/// health, which CUO never writes, so the session's published liveness is the authority — but DEATH IS NOT A
	/// REFUSAL any more (the user's KrokMP-aligned rule): a corpse stays watchable, and the watch moves on only when
	/// the body is not here at all (left the world, dropped, or was never rendered on this client).
	/// </summary>
	private bool TryResolveAnyBody(ulong steamId, out Body target)
	{
		target = null!;
		if (!renderer.TryGetRemoteBody(steamId, out var body) || body == null) // Unity object — ==
		{
			return false;
		}

		if (entities.GetRemotePlayer(steamId) is null)
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
	/// <summary>The black amount an unconscious body's screen sits at; the watched body's own screen effect.</summary>
	private const float UnconsciousBlackAmount = 0.8f;

	/// <summary>The colour the watched body's screen vignette reddens to as its blood runs out.</summary>
	private static readonly Color BleedVignette = new(0.65f, 0.05f, 0.05f, 0f);

	/// <summary>The vignette alpha for a blood volume: full at nothing left, clear at the whole five litres.</summary>
	private static Color OpacityFor(Color colour, float bloodVolume) =>
		new(colour.r, colour.g, colour.b, Mathf.Clamp01((5f - bloodVolume) / 5f) * 0.5f);

	private void Release(PlayerCamera camera)
	{
		if (!_touchedCamera)
		{
			return;
		}

		_touchedCamera = false;
		SpectatorPresentationGate.ClearWatched();
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
