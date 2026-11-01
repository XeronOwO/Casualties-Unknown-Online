using System;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.GameAdapter.World;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The host-descent follow's scene plumbing: deciding whether an edge applies to this member, driving the accept/settle/abort transitions against the live
/// scene, and handing the client back to the ordinary entry path when a follow can no longer finish.
/// </summary>
/// <remarks>
/// The STATE is <see cref="HostDescentFollow"/> (transitions and the two timeout questions, unit-pinned); this class is everything that needs the game — the
/// scene reads, the menu-return origin rule, the body guard and the run-phase mapping. It reads and writes the phase through the two delegates its owner passes, so
/// it never owns the phase and cannot drift from the coordinator's view of it.
/// </remarks>
internal sealed class DescentFollowController(
	HostDescentFollow follow,
	ISessionControl session,
	LayerAdvanceCoordinator layerAdvance,
	DescentBodyPreservation descentBody,
	RunMenuReturnCoordinator menuReturn,
	Func<bool> inWorld,
	Func<RunPhase> phase,
	Action<RunPhase> setPhase,
	ILogger log)
{
	/// <summary>
	/// The follow's ways out besides the settle: the member left the world (a real leave or the death
	/// menu destroys the world object — the follow must then hand the client back to the ordinary entry
	/// path, because its phase refuses every WorldJoin), or one of its own waits outlived its bound (the
	/// host's baseline never arrived, the generation never settled). Without this a member that leaves
	/// mid-descent stays out of the session for good (the independent review's blocker).
	/// </summary>
	/// <summary>Guest: the descent's generation actually started — the runtime's generation-stream reset is the launch edge, after the wrapper's baseline hold has ended.</summary>
	internal void OnGenerationStarted() => follow.Launched();

	/// <summary>Host: a local regeneration this side owns is starting — open the presentation window before the loading screen is visible, so the descent's own fade is skipped instead of reading as a black frame.</summary>
	internal void OnHostStarted()
	{
		if (session.Role == SessionRole.Host && session.SessionActive)
		{
			follow.BeginHostDescent();
		}
	}

	internal void UpdateAbort()
	{
		if (!follow.IsActive)
		{
			return;
		}

		var hasWorld = HarmonyTraverse.HasWorld;
		if (!follow.IsAbortDue(hasWorld, Environment.TickCount))
		{
			return;
		}

		Abort(follow.AbortReason(hasWorld), leaveToMenu: hasWorld);
	}

	/// <summary>Ends the follow and hands the member back to the ordinary entry path; a half-built world with no way forward is left to the menu, the same exit the start gate's valve uses.</summary>
	private void Abort(string reason, bool leaveToMenu)
	{
		follow.End();
		descentBody.Clear();
		setPhase(RunPhase.Idle);
		log.LogWarning("[LayerDescent] the follow is abandoned — {Reason}; the ordinary entry path takes over.", reason);
		if (leaveToMenu && PlayerCamera.main != null) // Unity object — ==
		{
			PlayerCamera.main.ToMainMenu();
		}
	}

	/// <summary>The native regeneration places <c>PlayerCamera.main.body</c>: without one the follow would start a generation that throws, so the member keeps the ordinary entry path.</summary>
	private bool HasLocalBody()
	{
		if (PlayerCamera.main != null && PlayerCamera.main.body != null) // Unity objects — ==
		{
			return true;
		}

		log.LogWarning("[LayerDescent] the host descended but this member has no local body to place — it keeps the ordinary entry path.");
		return false;
	}

	/// <summary>Accepts the host's descent and starts this member's own new-layer generation (the runtime already dropped the baseline for the layer being left).</summary>
	private void Accept(bool twice)
	{
		follow.Accept(Environment.TickCount);
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		descentBody.Note(body != null ? body.GetInstanceID() : 0);
		setPhase(RunPhase.Descending);
		log.LogInformation("[LayerDescent] host descent accepted (twice: {Twice}) — this member runs its own new-layer generation and waits for the gate instead of returning to the menu.", twice);
		layerAdvance.BeginLocalDescent(twice);
	}

	/// <summary>
	/// The descent's state machine. Guest: the settle edge — the generation actually started (the gen
	/// wrapper left its baseline hold) and the world is live again — moves the member to
	/// <see cref="RunPhase.WaitingReady"/>, or straight to Playing when the host released the gate while
	/// this member was still generating. Runs BEFORE the scene-state edge so the settle frame's
	/// transition is evaluated with the phase it belongs to (and so no report is sent for the transient
	/// "world object exists but the generation has not launched" frame of the wrapper's hold).
	/// </summary>
	internal void UpdateSettle()
	{
		if (!follow.TryDecideSettle(HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating(), HarmonyTraverse.HasLiveWorld, out var startsPlaying))
		{
			return;
		}

		if (startsPlaying)
		{
			follow.End();
			setPhase(RunPhase.Playing);
			log.LogInformation("[LayerDescent] the new layer is generated and the host's release was already in hand — this member starts playing.");
			return;
		}

		follow.WaitForReady();
		setPhase(RunPhase.WaitingReady);
		log.LogInformation("[LayerDescent] this member's new layer is generated — waiting for the host's release on the loading screen.");
	}

	/// <summary>
	/// Guest: the host's descent edge. Accepted from a member that is PLAYING (or waiting on this
	/// descent's gate when the host advances again) in a live world; a member in the menu, mid-entry, or
	/// with its own generation in flight follows the ordinary entry path instead. A pending menu return
	/// is cancelled only when it came from the host's own InMenu report — the player's own leave is never
	/// consumed by a descent.
	/// </summary>
	internal void OnEdge(bool twice)
	{
		if (follow.Phase == DescentPhase.WaitingReady)
		{
			// The host advanced again before releasing this group: the first layer is already generated
			// and only the release was outstanding, so this edge starts the next one. The body guard is
			// the same one the normal branch gets from DecideFollow — this branch does not pass through it.
			if (!HasLocalBody())
			{
				return;
			}

			if (!TryClearHostPullMenuReturn())
			{
				return;
			}

			Accept(twice);
			return;
		}

		var generationInFlight = follow.IsGenerationInFlight;
		var localBody = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		var decision = LayerDescentPolicy.DecideFollow(
			session.Role,
			session.SessionActive,
			inWorld(),
			phase() is RunPhase.Playing or RunPhase.WaitingReady,
			generationInFlight,
			HarmonyTraverse.HasWorld,
			localBody != null,
			localBody != null && !localBody.alive);
		switch (decision)
		{
			case LayerDescentDecision.Follow:
				break;

			case LayerDescentDecision.NotThisSides:
				log.LogDebug("[LayerDescent] a descent edge reached a side that is not a live guest — dropped.");
				return;

			case LayerDescentDecision.AlreadyDescending:
				log.LogWarning("[LayerDescent] a second descent edge arrived while this member's generation is still in flight — ignored; it generates the layer the host announced first.");
				return;

			case LayerDescentDecision.NotPlaying:
				log.LogInformation("[LayerDescent] the host descended but this member is not playing in its world ({Phase}, in world: {InWorld}) — it keeps the ordinary entry path.", phase(), inWorld());
				return;

			case LayerDescentDecision.NoBody:
				log.LogWarning("[LayerDescent] the host descended but this member has no local body to place — it keeps the ordinary entry path.");
				return;

			default:
				log.LogWarning("[LayerDescent] the host descended but this member has no world object to regenerate — it keeps the ordinary entry path.");
				return;
		}

		if (!TryClearHostPullMenuReturn())
		{
			return;
		}

		Accept(twice);
	}

	/// <summary>
	/// The descent may cancel a menu return recorded from the host's own InMenu report (the edge and the
	/// report travel one reliable lane, edge first, but a frame-end seam must never load the menu under a
	/// descent). Any other origin is not the descent's to consume — in particular the player's own leave,
	/// which must still strand nobody. Returns false when the member must not follow the edge.
	/// </summary>
	private bool TryClearHostPullMenuReturn()
	{
		if (!menuReturn.IsPending)
		{
			return true;
		}

		if (menuReturn.Origin != RunMenuReturnOrigin.HostPull)
		{
			log.LogInformation("[LayerDescent] the player's own leave is pending — the descent does not pull this member through it.");
			return false;
		}

		menuReturn.Clear();
		return true;
	}
}
