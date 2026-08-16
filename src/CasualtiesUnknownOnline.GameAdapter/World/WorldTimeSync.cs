using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.Run;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The multiplayer world-time domain (host authority). Time.timeScale is
/// process-global world state, so exactly one side owns it: the host. Guests
/// report their speed intents (WorldTimeRequest) and apply host broadcasts;
/// their local SetTimeScale writes for manual speeds and the vanilla
/// unconscious fast-forward are suppressed by the thin patches. The host
/// policy is pure (WorldTimePolicy): movement forces Normal, sleep
/// acceleration applies only when EVERY in-world player is unconscious, and a
/// request is cleared when either override fires — a fast-forward never
/// re-applies itself later. Direct Time.timeScale writers (quake reset,
/// console) are re-adopted by the host pump and corrected on guests; the
/// 5 s resend + world-entry fan-out heal late joiners and local-only effects.
/// </summary>
internal sealed class WorldTimeSync(
	ISessionControl session,
	EntitySyncService entities,
	CharacterDataStore characterData,
	RunCoordinator run,
	StartGateCoordinator gate,
	IWorldTimeControl worldTime,
	ILogger<WorldTimeSync> log)
{
	private readonly ISessionControl _session = session;
	private readonly EntitySyncService _entities = entities;
	private readonly CharacterDataStore _characterData = characterData;
	private readonly RunCoordinator _run = run;
	private readonly StartGateCoordinator _gate = gate;
	private readonly IWorldTimeControl _worldTime = worldTime;
	private readonly ILogger<WorldTimeSync> _log = log;

	/// <summary>The host-side resend interval — the idempotent self-heal for lazy sessions, reconnects and local-only time effects.</summary>
	private const float ResendIntervalSeconds = 5f;

	private WorldTimeSpeed _requestedSpeed = WorldTimeSpeed.Normal;
	private WorldTimeSpeed _appliedSpeed = WorldTimeSpeed.Normal;
	private float _nextResendTime;

	private bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	internal void BindToSession()
	{
		_worldTime.RequestReceived += OnRequestReceived;
		_worldTime.TimeReceived += OnTimeReceived;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_session.SessionEnded += OnSessionEnded;
	}

	internal void Unbind()
	{
		_worldTime.RequestReceived -= OnRequestReceived;
		_worldTime.TimeReceived -= OnTimeReceived;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
	}

	/// <summary>Pump: host policy + direct-write adoption + the 5 s resend; guest enforcement of the last host speed.</summary>
	internal void Update()
	{
		if (!_session.SessionActive || _run.LocalBody == null || _gate.WaitingForReady) // Unity object — ==
		{
			return; // the start gate owns timeScale 0 while everyone loads — world time must not touch it
		}

		if (IsHostMode)
		{
			AdoptDirectTimeScaleWrite();
			TryApplyPolicy();

			if (Time.unscaledTime >= _nextResendTime)
			{
				_nextResendTime = Time.unscaledTime + ResendIntervalSeconds;
				_worldTime.Broadcast(_appliedSpeed);
			}

			return;
		}

		EnforceAppliedSpeed();
	}

	/// <summary>
	/// PlayerCamera.SetTimeScale is about to run on this side (outside CUO
	/// apply/sleep scopes). Host: allow — it is the authority, the postfix
	/// reports the change. Guest: forced local transitions and local-only
	/// Slowmo/Paused stay; manual speeds become requests, sleep speeds are
	/// host-owned and swallowed.
	/// </summary>
	internal bool OnTimeScaleSetRequested(PlayerCamera.SpeedType speed, bool force)
	{
		if (!_session.SessionActive || _session.Role == SessionRole.Host)
		{
			return true;
		}

		if (force)
		{
			return true; // pause/menu/death transitions stay local-only
		}

		switch (speed)
		{
			case PlayerCamera.SpeedType.Normal:
			case PlayerCamera.SpeedType.Fast:
			case PlayerCamera.SpeedType.SuperFast:
				_log.LogInformation("[WorldTime] guest speed intent {Speed} — reporting to host.", speed);
				_worldTime.SendRequest(ToWorldTimeSpeed(speed));
				return false;
			case PlayerCamera.SpeedType.UnconsciousFast:
			case PlayerCamera.SpeedType.DyingFast:
				_log.LogInformation("[WorldTime] guest sleep fast-forward suppressed — the host's all-unconscious policy owns it.");
				return false;
			default:
				return true; // Slowmo/Paused — local-only presentation semantics
		}
	}

	/// <summary>
	/// The host's local SetTimeScale just ran — adopt the speed as the request
	/// (manual speeds only) and run the policy immediately so movement or
	/// sleep overrides correct it in the same frame. Apply/sleep scopes are
	/// excluded: the apply scope already owns its broadcast, the sleep scope
	/// never ran.
	/// </summary>
	internal void OnLocalTimeScaleChanged(PlayerCamera.SpeedType speed)
	{
		if (!IsHostMode)
		{
			return;
		}

		if (CallContext.Current is CallContext.Origin.WorldTimeApply or CallContext.Origin.WorldTimeSleepLocal)
		{
			return;
		}

		if (speed is not (PlayerCamera.SpeedType.Normal or PlayerCamera.SpeedType.Fast or PlayerCamera.SpeedType.SuperFast))
		{
			return; // Slowmo/Paused/unmapped speeds stay local-only
		}

		_requestedSpeed = ToWorldTimeSpeed(speed);
		TryApplyPolicy();
	}

	private void OnRequestReceived(ulong sender, WorldTimeSpeed speed)
	{
		if (!IsHostMode)
		{
			return;
		}

		if (!_session.TryGetMember(sender, out var member) || !member.Handshaken)
		{
			_log.LogWarning("[WorldTime] refused request from non-member {Sender}.", sender);
			return;
		}

		if (!WorldTimePolicy.IsGuestRequestSpeed(speed))
		{
			_log.LogWarning("[WorldTime] refused invalid guest request {Speed} from {Sender}.", speed, sender);
			return;
		}

		_log.LogInformation("[WorldTime] host received {Speed} request from {Sender}.", speed, sender);
		_requestedSpeed = speed;
		TryApplyPolicy();
	}

	private void OnTimeReceived(WorldTimeSpeed speed)
	{
		if (IsHostMode)
		{
			return; // direction guard — the host never applies its own broadcast
		}

		_appliedSpeed = Normalize(speed);
		if (!_gate.WaitingForReady)
		{
			ApplyLocalTime(_appliedSpeed); // during the start gate the gate owns timeScale 0; Update enforces the host speed on release
		}
	}

	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		// A member (re)entered the world — it starts at the game's default 1×;
		// send the host's current speed immediately instead of waiting up to 5 s.
		if (inWorld && IsHostMode && _run.LocalBody != null) // Unity object — ==
		{
			_worldTime.Broadcast(_appliedSpeed);
		}
	}

	private void OnSessionEnded()
	{
		_requestedSpeed = WorldTimeSpeed.Normal;
		_appliedSpeed = WorldTimeSpeed.Normal;
		_nextResendTime = 0f;
	}

	/// <summary>
	/// Host policy step: build the per-player facts (local body health + the
	/// 20 Hz velocity buffers + the host's 1 Hz character-data store for the
	/// guests' consciousness/blood pressure), decide, keep the policy's next
	/// request and apply/broadcast only on a real change.
	/// </summary>
	private void TryApplyPolicy()
	{
		if (_run.LocalBody == null || _gate.WaitingForReady) // Unity object — ==
		{
			return; // the start gate owns timeScale 0 while everyone loads
		}

		var decision = WorldTimePolicy.Decide(_requestedSpeed, CapturePlayerStates());
		_requestedSpeed = decision.NextRequested;
		if (decision.Speed == _appliedSpeed)
		{
			return;
		}

		_appliedSpeed = decision.Speed;
		ApplyLocalTime(_appliedSpeed);
		_worldTime.Broadcast(_appliedSpeed);
		_log.LogInformation("[WorldTime] host policy applied {Speed} (next request {Request}).", _appliedSpeed, _requestedSpeed);
	}

	private List<WorldTimePlayerState> CapturePlayerStates()
	{
		var players = new List<WorldTimePlayerState>();
		var localBody = _run.LocalBody;
		if (localBody != null) // Unity object — ==
		{
			var velocity = _entities.LocalPlayer.Velocity;
			players.Add(new WorldTimePlayerState(
				StateKnown: true,
				Alive: localBody.alive,
				Consciousness: localBody.consciousness,
				BrainDying: localBody.brainDying,
				VelocityX: velocity.X,
				VelocityY: velocity.Y));
		}

		foreach (var member in _session.Members)
		{
			if (!member.Handshaken || !member.InWorld)
			{
				continue;
			}

			var data = _characterData.GetSavedCharacter(member.SteamId);
			var health = data?.Health;
			var entity = _entities.GetRemotePlayer(member.SteamId);
			var velocity = entity?.Velocity ?? default;
			var stateKnown = health != null && entity != null;
			players.Add(new WorldTimePlayerState(
				StateKnown: stateKnown,
				Alive: health?.Alive ?? false,
				Consciousness: health?.Consciousness ?? WorldTimePolicy.SleepConsciousnessThreshold + 1f,
				BrainDying: health != null && health.BloodPressure < 10f && health.Consciousness < 5f,
				VelocityX: velocity.X,
				VelocityY: velocity.Y));
		}

		return players;
	}

	/// <summary>
	/// The host's actual Time.timeScale moved to another domain speed without a
	/// SetTimeScale call (e.g. the quake start resets 1×, WorldGeneration.cs:
	/// 870, or a console write) — adopt it as the request so the broadcast
	/// keeps guests on the same clock.
	/// </summary>
	private void AdoptDirectTimeScaleWrite()
	{
		var actual = MapTimeScaleToWorldTime(Time.timeScale);
		if (actual == null || actual == _appliedSpeed)
		{
			return;
		}

		_log.LogInformation("[WorldTime] host direct timeScale write {Scale} adopted as {Speed}.", Time.timeScale, actual);
		_requestedSpeed = actual.Value;
		_appliedSpeed = actual.Value;
		_worldTime.Broadcast(_appliedSpeed);
	}

	/// <summary>
	/// The guest's actual Time.timeScale moved to another domain speed without
	/// a relayed SetTimeScale (console, a forced local transition) — enforce
	/// the last host speed. Slowmo/Paused values are deliberately not domain
	/// speeds, so local-only effects are left alone.
	/// </summary>
	private void EnforceAppliedSpeed()
	{
		var actual = MapTimeScaleToWorldTime(Time.timeScale);
		if (actual != null && actual != _appliedSpeed)
		{
			ApplyLocalTime(_appliedSpeed);
		}
	}

	private void ApplyLocalTime(WorldTimeSpeed speed)
	{
		if (PlayerCamera.main == null) // Unity object — ==
		{
			return;
		}

		using (CallContext.Enter(CallContext.Origin.WorldTimeApply))
		{
			// force:true — the host's authority applies even while a local
			// pause/death transition would otherwise gate SetTimeScale.
			PlayerCamera.main.SetTimeScale(ToGameSpeed(speed), switchSound: true, force: true);
		}
	}

	private static WorldTimeSpeed Normalize(WorldTimeSpeed speed) => speed switch
	{
		WorldTimeSpeed.Normal or WorldTimeSpeed.Fast or WorldTimeSpeed.SuperFast
			or WorldTimeSpeed.UnconsciousFast or WorldTimeSpeed.DyingFast => speed,
		_ => WorldTimeSpeed.Normal,
	};

	private static PlayerCamera.SpeedType ToGameSpeed(WorldTimeSpeed speed) => speed switch
	{
		WorldTimeSpeed.Fast => PlayerCamera.SpeedType.Fast,
		WorldTimeSpeed.SuperFast => PlayerCamera.SpeedType.SuperFast,
		WorldTimeSpeed.UnconsciousFast => PlayerCamera.SpeedType.UnconsciousFast,
		WorldTimeSpeed.DyingFast => PlayerCamera.SpeedType.DyingFast,
		_ => PlayerCamera.SpeedType.Normal,
	};

	private static WorldTimeSpeed ToWorldTimeSpeed(PlayerCamera.SpeedType speed) => speed switch
	{
		PlayerCamera.SpeedType.Fast => WorldTimeSpeed.Fast,
		PlayerCamera.SpeedType.SuperFast => WorldTimeSpeed.SuperFast,
		PlayerCamera.SpeedType.UnconsciousFast => WorldTimeSpeed.UnconsciousFast,
		PlayerCamera.SpeedType.DyingFast => WorldTimeSpeed.DyingFast,
		_ => WorldTimeSpeed.Normal,
	};

	private static WorldTimeSpeed? MapTimeScaleToWorldTime(float timeScale)
	{
		if (timeScale <= 0.1f)
		{
			return null; // Paused (0) and Slowmo (0.16) are local-only
		}

		if (Mathf.Abs(timeScale - 1f) < 0.01f)
		{
			return WorldTimeSpeed.Normal;
		}

		if (Mathf.Abs(timeScale - 3.5f) < 0.05f)
		{
			return WorldTimeSpeed.DyingFast;
		}

		if (Mathf.Abs(timeScale - 5f) < 0.05f)
		{
			return WorldTimeSpeed.Fast;
		}

		if (Mathf.Abs(timeScale - 20f) < 0.2f)
		{
			return WorldTimeSpeed.SuperFast;
		}

		if (Mathf.Abs(timeScale - 25f) < 0.25f)
		{
			return WorldTimeSpeed.UnconsciousFast;
		}

		return null;
	}
}
