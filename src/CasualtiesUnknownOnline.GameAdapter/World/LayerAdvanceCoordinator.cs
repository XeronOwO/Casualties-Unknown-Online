using System;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The layer advance's world half, both directions.
/// <para>
/// A member's choice reaches the host as one request (the request channel and its
/// arbitration are the Runtime's — see
/// <see cref="ILayerAdvanceControl.LayerAdvanceRequested"/>), and the host drives
/// the game's own entry for it.
/// </para>
/// <para>
/// The native entry is <c>WorldGeneration.ContinueRun</c> (WorldGeneration.cs:1011-1020),
/// and it belongs to the client whose OWN body stands at the layer's bottom. A
/// guest's click reports the choice instead of descending: the layer's baseline is
/// the HOST's capture, so the member's regeneration would build a world the session
/// never agreed on AND would flip the member's own state to "generating" — which is
/// the state in which the session's own follow-up (the pull back to the menu and the
/// world entry that re-invites it) refuses to act. Suppressing the local descent is
/// therefore not a polish: it is what keeps the member ON the session's transition
/// path. The entry's local steps are still the game's — its guard, the panel close,
/// the walk release and the deepest-layer record all run in <c>ContinueRun</c> before
/// the sink. The suppressed step is only the immediate regeneration: the member's
/// own new-layer generation runs when the host's descent edge arrives (the native
/// coroutine, progression step included), once per layer — a click-time progression
/// grant as well would pay the member twice (independent review major-2).
/// </para>
/// <para>
/// When ANOTHER member's choice is what arrives, the host drives the same entry
/// minus its LOCAL-BODY clause — <c>PlayerCamera.main.body.transform.position.y &lt;
/// -halfHeight + 3.1</c> is the clause that ties the choice to the body that made
/// it, and the choice here was made somewhere else entirely. Everything else is the
/// entry's own: its re-entrancy clauses (a request landing inside an advance already
/// running changes nothing, which is what makes two members choosing at the same
/// moment cost one layer), the panel close, the walk release, the local deepest-layer
/// record, and <c>RegenerateWorld</c> itself — the coroutine that carries the layer's
/// progression step and the baseline capture every member follows. No second
/// generation path is introduced: the host's own click and a member's request drive
/// the same coroutine.
/// </para>
/// <para>
/// The other direction is the host's own descent (<see cref="OnHostDescent"/>): it is
/// announced to the members before the regeneration starts
/// (<see cref="IWorldControl.SendLayerDescent"/>), and each member runs the SAME
/// native coroutine locally through <see cref="BeginLocalDescent"/>, with the host's
/// captured baseline arriving at its generation boundary exactly as it does for a run
/// start. That is why the member no longer has to be pulled to the menu and
/// re-invited after the host has finished: the whole group generates in parallel and
/// the start gate releases it together.
/// </para>
/// </summary>
internal sealed class LayerAdvanceCoordinator(
	ISessionControl session,
	IWorldControl world,
	ILayerAdvanceControl layerAdvance,
	ILogger<LayerAdvanceCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly ILayerAdvanceControl _layerAdvance = layerAdvance;
	private readonly ILogger<LayerAdvanceCoordinator> _log = log;

	internal void BindToSession() => _layerAdvance.LayerAdvanceRequested += OnLayerAdvanceRequested;

	internal void Unbind() => _layerAdvance.LayerAdvanceRequested -= OnLayerAdvanceRequested;

	/// <summary>
	/// The local player chose to continue and the game's own entry has run its guard
	/// and its local steps — see the type comment for why the local regeneration must
	/// not follow YET. Returns true when the caller must suppress it.
	/// <para>
	/// No progression step is taken here. The member's own regeneration runs for the
	/// session's descent at the host's edge (<see cref="BeginLocalDescent"/>), and that
	/// is the native coroutine's own <c>IncreaseDepthByLayer</c> — granting one at the
	/// click as well would pay the member twice. A click whose edge is later lost (the
	/// degraded path, re-entered through the host's world join and <c>StartRun</c>) is
	/// the one path that does not take the step; that cost is named in the ticket's
	/// Limits.
	/// </para>
	/// </summary>
	internal bool TryDelegateLocalAdvance()
	{
		if (!LayerAdvancePolicy.ShouldDelegateLocalAdvance(_session.Role, _session.SessionActive))
		{
			_log.LogDebug("[LayerChoice] this side keeps its own descent ({Role}, session active: {SessionActive}).", _session.Role, _session.SessionActive);
			return false; // the host's own click, solo play, or no live session: the game's own descent is the session's
		}

		// Nothing to report (no committed run baseline on this side) means nothing to suppress either: a
		// descent nobody was asked about moves neither the session nor the member, so the game's own
		// descent stands — the pre-change behaviour, which is the conservative side of this gap.
		if (!_layerAdvance.TrySendLayerAdvanceRequest())
		{
			_log.LogWarning("[LayerChoice] this member's choice could not be reported — it keeps the game's own descent.");
			return false;
		}

		return true;
	}

	/// <summary>
	/// Host only: a local regeneration this side owns is starting. The members in the world are told NOW —
	/// this hook runs at the regeneration's sink, before its scene transition can reach a member as an exit
	/// — so each generates its own new layer in parallel. Solo play and a guest's own descent are not the
	/// session's step and send nothing.
	/// </summary>
	internal void OnHostDescent(bool twice)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			_log.LogDebug("[LayerDescent] this side's descent ({Role}, session active: {SessionActive}) is not the session's to announce.", _session.Role, _session.SessionActive);
			return;
		}

		_world.SendLayerDescent(twice);
	}

	/// <summary>
	/// Guest: the host announced its descent and the run state accepted it. Run the native entry's own
	/// local steps and the game's own regeneration locally: the baseline is the host's (dropped from this
	/// side so the generation boundary holds for the kernel layer-advance), so the member builds the same
	/// layer as the host while keeping its body and carried items — it never loads the menu.
	/// </summary>
	internal void BeginLocalDescent(bool twice)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			_log.LogWarning("[LayerDescent] the host descended but this member has no world to regenerate — it follows through the ordinary entry instead.");
			return;
		}

		// The native entry's own steps, in its own order (WorldGeneration.cs:1015-1018), minus its
		// local-body clause: that clause ties the descent to the body standing at the layer's bottom,
		// and the body there is the host's, not this member's.
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body != null)
		{
			body.forceWalk = false;
		}

		world.savePanel.SetActive(false);
		PlayerPrefs.SetInt("deepestlayer", Math.Max(HarmonyTraverse.ReadBiomeDepth() + 1, PlayerPrefs.GetInt("deepestlayer")));
		world.StartCoroutine(world.RegenerateWorld(twice));
		_log.LogInformation("[LayerDescent] this member starts its own new-layer generation (twice: {Twice}) from the host's descent edge.", twice);
	}

	/// <summary>Host only: an admitted member choice (handshaken member, this host's generation) — drive the layer's advance unless the world cannot take it.</summary>
	private void OnLayerAdvanceRequested(ulong sender)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			_log.LogWarning("[LayerChoice] {Sender} chose to continue but this host has no live world — nothing to advance.", sender);
			return;
		}

		var decision = LayerAdvancePolicy.DecideDrive(
			_session.Role, _session.SessionActive, world.worldExists, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating());
		if (decision != LayerAdvanceDecision.Drive)
		{
			_log.LogInformation("[LayerChoice] {Sender}'s choice did not drive an advance ({Decision}) — the layer stays where it is.", sender, decision);
			return;
		}

		// The native entry's own steps, in its own order (WorldGeneration.cs:1015-1018).
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body != null)
		{
			body.forceWalk = false;
		}

		world.savePanel.SetActive(false);
		PlayerPrefs.SetInt("deepestlayer", Math.Max(HarmonyTraverse.ReadBiomeDepth() + 1, PlayerPrefs.GetInt("deepestlayer")));
		world.StartCoroutine(world.RegenerateWorld(false));
		_log.LogInformation("[LayerChoice] {Sender}'s choice drives the layer advance — this host regenerates the layer and the members follow its baseline.", sender);
	}
}
