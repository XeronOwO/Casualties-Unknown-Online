using System;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The end-of-layer choice's world half: a member's choice reaches the host as
/// one request (the request channel and its arbitration are the Runtime's — see
/// <see cref="ILayerAdvanceControl.LayerAdvanceRequested"/>), and the host drives
/// the game's own entry for it.
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
/// the sink — and the one step whose carrier is suppressed, the layer's own
/// progression (<c>IncreaseDepthByLayer</c>, called inside <c>RegenerateWorld</c>),
/// is run here instead, once per layer.
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
/// </summary>
internal sealed class LayerAdvanceCoordinator(
	ISessionControl session,
	ILayerAdvanceControl layerAdvance,
	ILogger<LayerAdvanceCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly ILayerAdvanceControl _layerAdvance = layerAdvance;
	private readonly ILogger<LayerAdvanceCoordinator> _log = log;

	/// <summary>The local layer whose descent progression was already granted (see <see cref="GrantLocalDescentProgression"/>).</summary>
	private int _progressionGrantedAtLayer = int.MinValue;

	internal void BindToSession() => _layerAdvance.LayerAdvanceRequested += OnLayerAdvanceRequested;

	internal void Unbind()
	{
		_layerAdvance.LayerAdvanceRequested -= OnLayerAdvanceRequested;
		_progressionGrantedAtLayer = int.MinValue;
	}

	/// <summary>
	/// The local player chose to continue and the game's own entry has run its guard
	/// and its local steps — see the type comment for why the local regeneration must
	/// not follow. Returns true when the caller must suppress it.
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

		GrantLocalDescentProgression();
		return true;
	}

	/// <summary>
	/// The native descent's own progression step (<c>IncreaseDepthByLayer</c>, called
	/// inside the regeneration this side suppresses), run here because the member DOES
	/// descend with the session: its own body keeps the step it would have taken.
	/// Latched per local layer, because nothing is generating on this side while the
	/// session's transition is in flight — the native panel re-shows (its condition
	/// holds again) and a second click must not grant a second time. The layer's own
	/// rarity multipliers and depth are NOT touched here: they arrive with the host's
	/// baseline, which is applied at this member's next generation boundary.
	/// </summary>
	private void GrantLocalDescentProgression()
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		var layer = HarmonyTraverse.ReadBiomeDepth();
		if (layer == _progressionGrantedAtLayer)
		{
			return;
		}

		_progressionGrantedAtLayer = layer;
		world.IncreaseDepthByLayer();
		_log.LogInformation("[LayerChoice] the delegated descent granted this member's own layer progression at depth {Layer}.", layer);
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
