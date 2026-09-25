using System;
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
/// process-global world state, so exactly one side owns it: the host. A manual
/// speed change is LOCAL FIRST (user ruling 2026-09-18, decision 184): the
/// initiator's own client applies it at once and reports the intent
/// (WorldTimeRequest), the host arbitrates accept-first and answers with the
/// authoritative speed, and the initiator reconciles through
/// WorldTimeLocalInitiation — a refused or overridden intent ramps back to the
/// host's value, never snaps. The vanilla per-side unconscious fast-forward
/// stays suppressed by its own patch, and the host policy is pure
/// (WorldTimePolicy): the all-unconscious gate owns the clock while it applies
/// and clears the request, otherwise the standing request is honored. Direct
/// Time.timeScale writers (quake reset, console) are re-adopted by the host pump
/// and corrected on guests; the 5 s resend + world-entry fan-out heal late
/// joiners and local-only effects.
/// </summary>
internal sealed class WorldTimeSync(
	ISessionControl session,
	IEntitySyncControl entities,
	ICharacterDataControl characterData,
	RunCoordinator run,
	StartGateCoordinator gate,
	IWorldTimeControl worldTime,
	ILogger<WorldTimeSync> log)
{
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;
	private readonly ICharacterDataControl _characterData = characterData;
	private readonly RunCoordinator _run = run;
	private readonly StartGateCoordinator _gate = gate;
	private readonly IWorldTimeControl _worldTime = worldTime;
	private readonly ILogger<WorldTimeSync> _log = log;

	/// <summary>The host-side resend interval — the idempotent self-heal for lazy sessions, reconnects and local-only time effects.</summary>
	private const float ResendIntervalSeconds = 5f;

	private readonly WorldTimeLocalInitiation _initiation = new();
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

	/// <summary>Pump: host policy + direct-write adoption + the 5 s resend; guest ramp stepping and enforcement of the host's last speed (suspended while a local initiation is in flight).</summary>
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

		if (_initiation.IsRamping)
		{
			var step = _initiation.AdvanceRamp(Time.unscaledDeltaTime);
			Time.timeScale = step.TimeScale;
			if (step.Done)
			{
				ApplyLocalTime(_appliedSpeed); // the exact host value, through the normal path (HUD + sound)
				_log.LogInformation("[WorldTime] local clock returned to the host's {Speed}.", _appliedSpeed);
			}

			return;
		}

		if (_initiation.SuspendsEnforcement)
		{
			return; // the initiator's lead window — the local clock is deliberately ahead of the host's
		}

		EnforceAppliedSpeed();
	}

	/// <summary>
	/// PlayerCamera.SetTimeScale is about to run on this side (outside CUO
	/// apply/sleep scopes), carrying the native flags that say what the call is.
	/// The decision itself is <see cref="WorldTimeScaleCall.Route"/> — this method
	/// only executes its verdict, so the rule that decides who may write the clock
	/// has one owner and is tested without Unity. Outside a session the vanilla
	/// behaviour stands; an announced change is the host's own (the postfix adopts
	/// it as the request) or a guest's LOCAL FIRST report; a forced transition,
	/// Slowmo/Paused and the host's own sleep speed stay local; the sleep
	/// fast-forward is suppressed on a guest; and a SILENT automatic reset — the
	/// native movement rule above all — never writes the shared clock, never
	/// becomes a request, and keeps this screen on the session's speed (user
	/// ruling 2026-09-21). While the start gate holds the clock nothing is written
	/// at all, resets included.
	/// </summary>
	internal bool OnTimeScaleSetRequested(PlayerCamera.SpeedType speed, bool switchSound, bool force)
	{
		var kind = WorldTimeScaleCall.Classify(SpeedFamily(speed), switchSound, force);
		var action = WorldTimeScaleCall.Route(kind, _session.Role == SessionRole.Host, _session.SessionActive, AtStartGate);
		switch (action)
		{
			case WorldTimeScaleCall.Action.SwallowAndKeepSessionSpeed:
				// Not a speed intent, on EITHER side: the native movement rule (or
				// any other in-world event reset) neither ends the shared
				// acceleration nor dips this client's clock.
				KeepSessionSpeed(speed);
				return false;
			case WorldTimeScaleCall.Action.Swallow:
				if (kind == WorldTimeScaleCall.Kind.AutomaticReset)
				{
					// Low frequency and the only direct evidence that the gate's 0
					// survived a reset — Info, as the pre-rule code logged here.
					_log.LogInformation("[WorldTime] silent {Speed} reset swallowed — the start gate owns the world clock.", speed);
				}
				else
				{
					_log.LogInformation("[WorldTime] guest sleep fast-forward suppressed — the host's all-unconscious policy owns it.");
				}

				return false;
			case WorldTimeScaleCall.Action.DeferToStartGate:
				_log.LogInformation("[WorldTime] guest {Speed} stays local-only — the start gate owns the world clock.", speed);
				return false;
			case WorldTimeScaleCall.Action.RunLocalFirstAndReport:
				BeginLocalFirst(speed);
				return true; // local-first: the native call writes this client's clock at once
			case WorldTimeScaleCall.Action.RunAsAuthority:
			case WorldTimeScaleCall.Action.RunLocalOnly:
				return true; // the authority, a forced transition, Slowmo/Paused or the host's sleep speed
			default:
				// Route's table is exhaustive, so a value arriving here is a
				// programming error: fail loudly instead of running the call.
				throw new InvalidOperationException($"unrouted world-time action {action} — extend WorldTimeScaleCall.Route and this switch together");
		}
	}

	/// <summary>The start gate owns timeScale 0 while everyone loads — world time must not touch it.</summary>
	private bool AtStartGate => _run.LocalBody == null || _gate.WaitingForReady; // Unity object — ==

	/// <summary>A guest's announced change: this client's clock first, then the intent the host answers.</summary>
	private void BeginLocalFirst(PlayerCamera.SpeedType speed)
	{
		var intent = ToWorldTimeSpeed(speed);
		_initiation.BeginLocalInitiation(intent);
		_log.LogInformation(
			"[WorldTime] guest {Speed} applied locally and reported (host authority {Authority}).",
			intent,
			_initiation.Authoritative);
		_worldTime.SendRequest(intent);
	}

	/// <summary>Which of CUO's speed families the game's speed belongs to (the routing rule's only speed input).</summary>
	private static WorldTimeScaleCall.SpeedFamily SpeedFamily(PlayerCamera.SpeedType speed) => speed switch
	{
		PlayerCamera.SpeedType.UnconsciousFast or PlayerCamera.SpeedType.DyingFast => WorldTimeScaleCall.SpeedFamily.SleepOwned,
		PlayerCamera.SpeedType.Normal or PlayerCamera.SpeedType.Fast or PlayerCamera.SpeedType.SuperFast => WorldTimeScaleCall.SpeedFamily.SharedClock,
		_ => WorldTimeScaleCall.SpeedFamily.Presentation,
	};

	/// <summary>
	/// The host's local SetTimeScale just ran — adopt the speed as the request
	/// (an announced change or a forced transition, as before) and run the policy
	/// immediately so the sleep gate corrects it in the same frame. A SILENT
	/// automatic reset is NOT adopted: it is not a speed intent, so the host's
	/// own movement (or any in-world event reset) no longer replaces the standing
	/// acceleration (user ruling 2026-09-21). Apply/sleep scopes are excluded:
	/// the apply scope already owns its broadcast, the sleep scope never ran.
	/// </summary>
	internal void OnLocalTimeScaleChanged(PlayerCamera.SpeedType speed, bool switchSound, bool force)
	{
		if (!IsHostMode)
		{
			return;
		}

		if (_run.LocalBody == null || _gate.WaitingForReady) // Unity object — == (menu/gate timeScale writes must not leak a request)
		{
			return;
		}

		if (CallContext.Current is CallContext.Origin.WorldTimeApply or CallContext.Origin.WorldTimeSleepLocal)
		{
			return;
		}

		if (WorldTimeScaleCall.Classify(SpeedFamily(speed), switchSound, force) == WorldTimeScaleCall.Kind.AutomaticReset)
		{
			// THE ONLY line that stops the host adopting a swallowed reset: the
			// prefix returns false, but a skipped original still RUNS this postfix
			// (verified on this repository's own HarmonyX 2.9.0 — the original does
			// not run, __runOriginal is false, and the postfix still fires), so
			// without it the host's own movement would replace the standing request
			// and the acceleration would end anyway.
			return;
		}

		if (speed is not (PlayerCamera.SpeedType.Normal or PlayerCamera.SpeedType.Fast or PlayerCamera.SpeedType.SuperFast))
		{
			return; // Slowmo/Paused/unmapped speeds stay local-only
		}

		_requestedSpeed = ToWorldTimeSpeed(speed);
		TryApplyPolicy();
	}

	/// <summary>
	/// A guest's local initiation arrived. Arbitration is ACCEPT FIRST (user
	/// ruling 2026-09-18): only a request this host cannot represent is refused —
	/// not an in-world member, not a guest-requestable speed, the start gate
	/// owning the clock. EVERY path ANSWERS with the authoritative speed: an
	/// accepted request that does not change the speed still settles the
	/// initiator's pending intent, a refusal is how it learns to return to the
	/// host's value, and a silent drop would leave that client's local initiation
	/// pending — and therefore its enforcement of the host clock suspended —
	/// until the next 5 s resend.
	/// </summary>
	private void OnRequestReceived(ulong sender, WorldTimeSpeed speed)
	{
		if (!IsHostMode)
		{
			return;
		}

		if (!_session.TryGetMember(sender, out var member) || !member.Handshaken || !member.InWorld)
		{
			_log.LogWarning("[WorldTime] refused request from {Sender} (not an in-world member) — answered with the authoritative {Speed}.", sender, _appliedSpeed);
			Answer();
			return;
		}

		if (_gate.WaitingForReady)
		{
			_log.LogInformation("[WorldTime] ignored {Speed} request from {Sender} — the start gate owns the world clock.", speed, sender);
			Answer(); // the verdict still goes out: an unanswered request suspends the initiator's enforcement
			return;
		}

		if (!WorldTimePolicy.IsGuestRequestSpeed(speed))
		{
			_log.LogWarning("[WorldTime] refused invalid guest request {Speed} from {Sender} — answered with the authoritative {Applied}.", speed, sender, _appliedSpeed);
			Answer();
			return;
		}

		_log.LogInformation("[WorldTime] host accepted {Speed} request from {Sender}.", speed, sender);
		_requestedSpeed = speed;
		if (!TryApplyPolicy())
		{
			Answer(); // accepted and unchanged — the initiator still needs its verdict now
		}
	}

	/// <summary>Send the authoritative speed as the answer to a request (idempotent for members already on it).</summary>
	private void Answer() => _worldTime.Broadcast(_appliedSpeed);

	/// <summary>
	/// The host's authoritative speed arrived — including the answer to this
	/// client's own request. The local initiation owns the verdict decision
	/// (confirm / adopt / ramp) and this method only applies what it returns, so
	/// an acceleration in flight is never fought by its own enforcement and an
	/// idempotent resend still writes nothing.
	/// </summary>
	private void OnTimeReceived(WorldTimeSpeed speed)
	{
		if (IsHostMode)
		{
			return; // direction guard — the host never applies its own broadcast
		}

		var incoming = WorldTimePolicy.NormalizeSpeed(speed);
		var pending = _initiation.Pending;
		var reconcile = _initiation.OnAuthoritative(incoming, Time.timeScale);
		_appliedSpeed = incoming;

		switch (reconcile)
		{
			case WorldTimeReconcile.Adopt:
				if (!_gate.WaitingForReady)
				{
					ApplyLocalTime(_appliedSpeed); // during the start gate the gate owns timeScale 0; Update enforces the host speed on release
				}

				break;
			case WorldTimeReconcile.Ramp:
				_log.LogInformation("[WorldTime] host answered {Speed} for the locally applied {Pending} — ramping back.", incoming, pending);
				break;
			default:
				break; // confirmed or idempotent — the clock already runs this value, so no sound is replayed
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
		_initiation.ResetSessionState();
		_nextResendTime = 0f;
	}

	/// <summary>
	/// Host policy step: build the per-player facts (local body health + the
	/// host's 1 Hz character-data store for the guests' consciousness/blood
	/// pressure), decide, keep the policy's next request and apply/broadcast only
	/// on a real change. Returns whether a change was broadcast — a caller that
	/// owes a requester an answer sends one when this returns false.
	/// </summary>
	private bool TryApplyPolicy()
	{
		if (_run.LocalBody == null || _gate.WaitingForReady) // Unity object — ==
		{
			return false; // the start gate owns timeScale 0 while everyone loads
		}

		var decision = WorldTimePolicy.Decide(_requestedSpeed, CapturePlayerStates());
		_requestedSpeed = decision.NextRequested;
		if (decision.Speed == _appliedSpeed)
		{
			return false;
		}

		_appliedSpeed = decision.Speed;
		ApplyLocalTime(_appliedSpeed);
		_worldTime.Broadcast(_appliedSpeed);
		_log.LogInformation("[WorldTime] host policy applied {Speed} (next request {Request}).", _appliedSpeed, _requestedSpeed);
		return true;
	}

	/// <summary>
	/// The per-player facts the sleep gate needs: the local body's health plus
	/// the host's 1 Hz character-data store for the guests, with the remote body
	/// proxy as the "this host can observe that member" requirement. No velocity
	/// is read — a movement key is the mover's own action, never a host-side veto
	/// (decision 184).
	/// </summary>
	private List<WorldTimePlayerState> CapturePlayerStates()
	{
		var players = new List<WorldTimePlayerState>();
		var localBody = _run.LocalBody;
		if (localBody != null) // Unity object — ==
		{
			players.Add(new WorldTimePlayerState(
				StateKnown: true,
				Alive: localBody.alive,
				Consciousness: localBody.consciousness,
				BrainDying: localBody.brainDying));
		}

		foreach (var member in _session.Members)
		{
			if (!member.Handshaken || !member.InWorld)
			{
				continue;
			}

			var health = _characterData.GetSavedCharacter(member.SteamId)?.Health;
			var entity = _entities.GetRemotePlayer(member.SteamId);
			players.Add(new WorldTimePlayerState(
				StateKnown: health != null && entity != null,
				Alive: health?.Alive ?? false,
				Consciousness: health?.Consciousness ?? WorldTimePolicy.SleepConsciousnessThreshold + 1f,
				BrainDying: health != null && health.BloodPressure < 10f && health.Consciousness < 5f));
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
		var actual = WorldTimeSpeedScale.FromTimeScale(Time.timeScale);
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
		var actual = WorldTimeSpeedScale.FromTimeScale(Time.timeScale);
		if (actual != null && actual != _appliedSpeed)
		{
			ApplyLocalTime(_appliedSpeed);
		}
	}

	/// <summary>
	/// A silent automatic reset ran into a session: the value it asked for is
	/// discarded and this screen keeps the session's speed. Nothing is written
	/// when the clock is already there — the movement case, so a movement key
	/// produces no dip and no speed sound — and nothing is written while the
	/// local initiation deliberately leads the host.
	/// </summary>
	private void KeepSessionSpeed(PlayerCamera.SpeedType speed)
	{
		var atSessionSpeed = WorldTimeSpeedScale.FromTimeScale(Time.timeScale) == _appliedSpeed;
		if (!WorldTimeScaleCall.ShouldRestoreSessionSpeed(WorldTimeScaleCall.Kind.AutomaticReset, _initiation.SuspendsEnforcement, atSessionSpeed))
		{
			_log.LogDebug("[WorldTime] silent {Speed} reset ignored — the session clock stands at {Applied}.", speed, _appliedSpeed);
			return;
		}

		// A local presentation effect (Slowmo/Paused) had this screen off the
		// session speed: put it back SILENTLY — the reset being replaced was
		// silent too, and the session speed is not a new player intent.
		ApplyLocalTime(_appliedSpeed, switchSound: false, force: false);
		if (WorldTimeSpeedScale.FromTimeScale(Time.timeScale) == _appliedSpeed)
		{
			_log.LogDebug("[WorldTime] silent {Speed} reset replaced by the session clock {Applied}.", speed, _appliedSpeed);
		}
		else
		{
			// The write did not land — no camera yet, or the native pause/death
			// guard — so report what happened rather than what was intended.
			_log.LogDebug("[WorldTime] silent {Speed} reset left the clock at {Scale} — the session clock {Applied} was not written.", speed, Time.timeScale, _appliedSpeed);
		}
	}

	private void ApplyLocalTime(WorldTimeSpeed speed, bool switchSound = true, bool force = true)
	{
		if (PlayerCamera.main == null) // Unity object — ==
		{
			return;
		}

		using (CallContext.Enter(CallContext.Origin.WorldTimeApply))
		{
			// force defaults to true — the host's authority applies even while a
			// local pause/death transition would otherwise gate SetTimeScale.
			PlayerCamera.main.SetTimeScale(ToGameSpeed(speed), switchSound, force);
		}
	}

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
}
