using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The world-entry instruction's recovery, both halves.
/// <para>
/// The member's half: the host's world exit (a layer change, or the host leaving) pulls every
/// member to the menu — the member's own <c>RunCoordinator</c> does that from the host's scene
/// report. Coming back is driven by ONE <c>WorldJoin</c> the host sends at its world-entry edge,
/// and that instruction can be lost (the lazy-P2P swallow) or discarded by the member's own entry
/// gate (an invite that arrives while the member is still in its old world waits, then dies with
/// that world). So after a host-caused exit this side keeps a bounded window
/// (<see cref="ReentryWindow"/>): while it is in the menu, the host is in a world, and no entry is
/// in hand, it asks the host to re-issue the instruction. The evidence that this is the gate is
/// batch `20261007-a`'s park experiment — held inbound keeps the member in the world, unheld
/// inbound drops it — and batch `20261025-a`, which dropped exactly one <c>WorldJoin</c> and left
/// the member without a body while the control member with the same change (delivered invite)
/// came back.
/// </para>
/// <para>
/// The host's half: the ask is answered with the SAME instruction every other entry uses
/// (<see cref="IWorldControl.SendWorldJoinTo"/>), once per member per <see cref="ReentryPolicy"/>'s
/// world facts and the Runtime channel's cooldown. A member that left the world on its own never
/// asks — the reason it is in the menu is the one fact the host cannot see, and the arm's
/// <c>memberInWorldOrEntering</c> condition is what implements "the host's exit is what took this
/// member out".
/// </para>
/// </summary>
internal sealed class ReentryCoordinator(
	ISessionControl session,
	IWorldControl world,
	IReentryControl reentry,
	RunCoordinator run,
	ITimeSource time,
	ILogger<ReentryCoordinator> log) : ISessionReset
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly IReentryControl _reentry = reentry;
	private readonly RunCoordinator _run = run;
	private readonly ITimeSource _time = time;
	private readonly ILogger<ReentryCoordinator> _log = log;

	/// <summary>The member-side bounded window (pure — see <see cref="ReentryWindow"/>).</summary>
	private readonly ReentryWindow _window = new();

	internal void BindToSession()
	{
		_reentry.ReentryRequested += OnReentryRequested;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_session.SessionEnded += ResetSessionState;
	}

	internal void Unbind()
	{
		_reentry.ReentryRequested -= OnReentryRequested;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= ResetSessionState;
		DropWindow("the session binding was unbound");
	}

	/// <summary>The session is gone — the window belongs to it (ISessionReset).</summary>
	public void ResetSessionState() => DropWindow("the session ended");

	/// <summary>
	/// Host only: a member's ask was admitted against the session facts (handshaken, held out of
	/// the world, outside its cooldown). Answer it with this host's own world facts — or name why
	/// it cannot be answered yet, so the member's window keeps asking.
	/// </summary>
	private void OnReentryRequested(ulong sender)
	{
		var decision = ReentryPolicy.Decide(
			_session.Role,
			_session.SessionActive,
			HarmonyTraverse.HasLiveWorld,
			_world.HasCommittedRunBaseline);
		switch (decision)
		{
			case ReentryDecision.Invite:
				_world.SendWorldJoinTo(sender);
				_log.LogInformation(
					"[Reentry] {Sender} asked to be taken back in and this host's world is up — sent the entry instruction (with its run-baseline checkpoint).",
					sender);
				break;

			case ReentryDecision.NoLiveWorld:
				_log.LogInformation(
					"[Reentry] {Sender}'s ask is not answered yet: this host has no live world (entering or regenerating) — the member's bounded window asks again.",
					sender);
				break;

			case ReentryDecision.NoRunBaseline:
				_log.LogWarning(
					"[Reentry] {Sender} asked to be taken back in but this host has no committed run baseline — refused: an entry now would hold the member's generation on params that never arrive.",
					sender);
				break;

			default:
				_log.LogDebug("[Reentry] {Sender}'s ask reached a side that is not an active host — dropped.", sender);
				break;
		}
	}

	/// <summary>
	/// Guest: the HOST left the world (its regeneration, or its own exit). The window is armed only
	/// when this exit is what takes this member out of a world it is in, entering or generating; a
	/// member already in the menu left on its own and is not the host's to pull back.
	/// </summary>
	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || steamId != _session.HostSteamId || inWorld)
		{
			return;
		}

		var memberInWorldOrEntering = _run.IsInWorld || _run.IsEnteringWorld || HarmonyTraverse.IsGenerating();
		if (_run.IsDescending)
		{
			// The host "left" the world because it is advancing a layer and this member is generating its
			// own new layer in parallel — nothing was lost, so there is no menu to arm a window against.
			_log.LogDebug("[Reentry] the host left the world on its way to the next layer and this member is generating its own — no re-entry window is armed.");
			return;
		}

		if (!_window.TryArm(_time.NowMs, memberInWorldOrEntering))
		{
			_log.LogDebug("[Reentry] the host left the world but this member was already in the menu — its menu is its own, so no re-entry window is armed.");
			return;
		}

		_log.LogInformation(
			"[Reentry] the host left the world — this member follows it to the menu and will ask for its entry instruction again if the host's one-shot invite does not land.");
	}

	/// <summary>
	/// Guest: one step of the bounded re-entry window. The member asks only while it is genuinely
	/// out of the world with nothing in hand — never while an instruction is being followed or the
	/// world is generating, and never before the host is back in a world to enter. In the world, the
	/// window is done.
	/// </summary>
	internal void Update()
	{
		if (!_window.Armed)
		{
			return;
		}

		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			DropWindow("this is no longer an active guest session");
			return;
		}

		var nowMs = _time.NowMs;
		var state = _window.Step(
			nowMs,
			_run.IsInWorld,
			HarmonyTraverse.IsGenerating(),
			_run.IsEnteringWorld,
			_session.IsRemoteInWorld(_session.HostSteamId));
		switch (state)
		{
			case ReentryWindowState.Closed:
				_log.LogInformation("[Reentry] the window is closed — {Reason}.", _window.CloseReason);
				break;

			case ReentryWindowState.Ready:
				if (!_reentry.TrySendReentryRequest())
				{
					return; // nothing left the client — a request that never went out must not spend the window
				}

				_window.NoteAsk(nowMs);
				_log.LogInformation(
					"[Reentry] asked the host to re-issue this member's entry instruction ({Requests}/{Max}) — out of the world, the host in one, and no instruction in hand.",
					_window.Asks, ReentryWindow.MaxRequests);
				break;
		}
	}

	/// <summary>Drops the window and says why (the log line belongs to the transition, not to the caller).</summary>
	private void DropWindow(string reason)
	{
		if (!_window.Armed)
		{
			return;
		}

		_window.Reset();
		_log.LogInformation("[Reentry] the window is closed — {Reason}.", reason);
	}
}
