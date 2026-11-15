using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
	IEntitySyncControl entities,
	RemoteVitalsService vitals,
	ILogger<LayerAdvanceCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly ILayerAdvanceControl _layerAdvance = layerAdvance;
	private readonly IEntitySyncControl _entities = entities;
	private readonly RemoteVitalsService _vitals = vitals;
	private readonly ILogger<LayerAdvanceCoordinator> _log = log;

	/// <summary>The panel whose choices this client already hid (Unity object); a new activation is a new panel object.</summary>
	private GameObject? _choicesHiddenFor;

	internal void BindToSession()
	{
		_layerAdvance.LayerAdvanceRequested += OnLayerAdvanceRequested;
		_layerAdvance.LayerAdvanceRefused += OnLayerAdvanceRefused;
	}

	internal void Unbind()
	{
		_layerAdvance.LayerAdvanceRequested -= OnLayerAdvanceRequested;
		_layerAdvance.LayerAdvanceRefused -= OnLayerAdvanceRefused;
	}

	/// <summary>
	/// The end-of-layer panel's two choices belong to the HOST (the user's 2026-10-24 ruling): on a member both
	/// buttons are hidden rather than refused, because a control the player may not use should not be there to
	/// press. The buttons are found by what they CALL (<c>WorldGeneration.ContinueRun</c> / <c>SaveAndExit</c>), so a
	/// renamed or re-skinned panel object still resolves; the work happens once per panel activation, not per frame.
	/// </summary>
	internal void UpdatePanelChoices()
	{
		if (!LayerPanelChoicePolicy.HidesMemberChoices(_session.Role, _session.SessionActive))
		{
			return;
		}

		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		var panel = world.savePanel;
		if (panel == null || !panel.activeInHierarchy) // Unity objects — ==
		{
			return;
		}

		if (_choicesHiddenFor == panel) // Unity object — the same panel already had its choices hidden
		{
			return;
		}

		_choicesHiddenFor = panel;
		var hidden = 0;
		foreach (var button in panel.GetComponentsInChildren<Button>(true))
		{
			if (!CallsAnEndOfLayerEntry(button))
			{
				continue;
			}

			button.gameObject.SetActive(false);
			hidden++;
		}

		if (hidden > 0)
		{
			_log.LogInformation("[LayerChoice] this member does not own the end-of-layer choices: {Count} button(s) hidden (the host presses them).", hidden);
		}
	}

	/// <summary>Whether this button's own click invokes one of the native end-of-layer entries.</summary>
	private static bool CallsAnEndOfLayerEntry(Button button)
	{
		var calls = button.onClick;
		for (var i = 0; i < calls.GetPersistentEventCount(); i++)
		{
			var name = calls.GetPersistentMethodName(i);
			if (name is "ContinueRun" or "SaveAndExit")
			{
				return true;
			}
		}

		return false;
	}

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
	internal bool TryDelegateLocalAdvance(bool twice)
	{
		if (!LayerAdvancePolicy.ShouldDelegateLocalAdvance(_session.Role, _session.SessionActive))
		{
			_log.LogDebug("[LayerChoice] this side keeps its own descent ({Role}, session active: {SessionActive}).", _session.Role, _session.SessionActive);
			return false; // the host's own click, solo play, or no live session: the game's own descent is the session's
		}

		// Nothing to report (no committed run baseline on this side) means nothing to suppress either: a
		// descent nobody was asked about moves neither the session nor the member, so the game's own
		// descent stands — the pre-change behaviour, which is the conservative side of this gap.
		if (!_layerAdvance.TrySendLayerAdvanceRequest(twice))
		{
			_log.LogWarning("[LayerChoice] this member's choice could not be reported — it keeps the game's own descent (twice: {Twice}).", twice);
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
		// This call IS the host's edge, not a local choice to report: the sink patch must not delegate it a
		// second time (the pod's `doPod` flag is still set here on purpose — the rider's own regeneration reads
		// it for the arrival effects). The mark is consumed by the sink patch.
		InHostEdgeDescent = true;
		try
		{
			world.StartCoroutine(world.RegenerateWorld(twice));
		}
		finally
		{
			InHostEdgeDescent = false;
		}
		_log.LogInformation("[LayerDescent] this member starts its own new-layer generation (twice: {Twice}) from the host's descent edge.", twice);
	}

	/// <summary>
	/// True while <see cref="BeginLocalDescent"/> starts the member's own regeneration for the HOST's descent
	/// edge. The sink patch reads it so that call is never delegated a second time: the pod's <c>doPod</c> flag
	/// is still set at that moment (the rider's own arrival effects need it), so without this mark the follow-up
	/// generation would look like another pod request.
	/// </summary>
	internal static bool InHostEdgeDescent { get; private set; }

	/// <summary>
	/// Host only, at the drill pod's own sink: the native pod fires after ONE body crouches under it for 5 s
	/// (<c>DrillPod.cs:18-33</c>), but a session must not take the whole party down from one player's press. Every
	/// living, connected member has to stand inside the pod's own 1.5 x 5 area; dead and disconnected members are not
	/// counted, and a living member whose body the host cannot place yet counts as missing. The panel's own
	/// all-living rule (<see cref="LayerPanelGate"/>) is separate and unchanged.
	/// </summary>
	internal bool TryBeginHostPodDescent()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return true; // solo, or a guest's own call: the host's request path is the gate
		}

		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return true; // no local pod trigger; the native guard cannot fire either
		}

		var pod = ToNet(body.transform.position);
		if (TryPodTeam(pod, out var gate))
		{
			return true;
		}

		ResetNearestPod();
		_log.LogWarning(
			"[PodTrigger] the descent waits for every living member at the drill pod: {Living} living, {AtPod} at the pod; missing: {Missing}.",
			gate.Living, gate.AtPod, string.Join(", ", gate.Missing));
		return false;
	}

	/// <summary>Whether every living, connected member is inside the pod (plus the host's own body when it exists).</summary>
	private bool TryPodTeam(NetVector2 pod, out PodGateResult gate)
	{
		var members = new List<PodMember>();
		var hostBody = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (hostBody != null) // Unity object — ==
		{
			members.Add(new PodMember(
				_session.LocalSteamId,
				NameOf(_session.LocalSteamId),
				InWorld: true,
				Alive: hostBody.alive,
				Position: ToNet(hostBody.transform.position)));
		}

		foreach (var steamId in _session.InWorldRemoteSteamIds())
		{
			if (steamId == _session.LocalSteamId)
			{
				continue;
			}

			var entity = _entities.GetRemotePlayer(steamId);
			var alive = entity?.Alive ?? true;
			if (_vitals.TryGet(steamId, out var snapshot))
			{
				alive = snapshot.Alive;
			}

			members.Add(new PodMember(
				steamId,
				NameOf(steamId),
				InWorld: true,
				Alive: alive,
				Position: entity?.Position));
		}

		gate = PodTeamGate.Evaluate(pod, members);
		return gate.CanFire;
	}

	/// <summary>The requester's synced body is the pod position the host evaluates the whole team against.</summary>
	private bool TryRequesterAtPod(ulong sender, out PodGateResult gate)
	{
		var entity = _entities.GetRemotePlayer(sender);
		if (entity == null)
		{
			gate = new PodGateResult(false, 0, 0, [PodTeamGate.Label(sender, null)]);
			return false;
		}

		return TryPodTeam(entity.Position, out gate);
	}

	private string NameOf(ulong steamId) =>
		_session.TryGetMember(steamId, out var member)
			? PodTeamGate.Label(steamId, member.DisplayName)
			: PodTeamGate.Label(steamId, null);

	/// <summary>Guest: the host refused this descent. The native pod has already set <c>didTeleport</c>; restore it so the next press can fire again.</summary>
	private void OnLayerAdvanceRefused(string reason)
	{
		ResetNearestPod();
		_log.LogInformation("[PodTrigger] the host refused the descent: {Reason}", reason);
	}

	/// <summary>Undo the native pod's 5 s latch on this client: the next press must be able to try again.</summary>
	private static void ResetNearestPod()
	{
		var world = WorldGeneration.world;
		if (world != null) // Unity object — ==
		{
			world.doPod = false;
		}

		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return;
		}

		DrillPod? nearest = null;
		var best = float.MaxValue;
		foreach (var pod in Object.FindObjectsOfType<DrillPod>())
		{
			if (pod == null) // Unity object — ==
			{
				continue;
			}

			var dx = pod.transform.position.x - body.transform.position.x;
			var dy = pod.transform.position.y - body.transform.position.y;
			var distance = (dx * dx) + (dy * dy);
			if (distance >= best)
			{
				continue;
			}

			best = distance;
			nearest = pod;
		}

		if (nearest == null) // Unity object — ==
		{
			return;
		}

		AccessTools.Field(typeof(DrillPod), "didTeleport")?.SetValue(nearest, false);
		AccessTools.Field(typeof(DrillPod), "timeHeld")?.SetValue(nearest, 0f);
		if (PlayerCamera.main != null) // Unity object — ==
		{
			PlayerCamera.main.bonusActionBarTime = 0f;
		}
	}

	private static NetVector2 ToNet(Vector3 value) => new(value.x, value.y);

	/// <summary>Host only: an admitted member choice (handshaken member, this host's generation) — drive the layer's advance unless the world cannot take it.</summary>
	private void OnLayerAdvanceRequested(ulong sender, bool twice)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			_log.LogWarning("[LayerChoice] {Sender} chose to continue but this host has no live world — nothing to advance.", sender);
			_layerAdvance.RefuseLayerAdvance(sender, "this host has no live world.");
			return;
		}

		if (twice && !TryRequesterAtPod(sender, out var podGate))
		{
			var waiting = podGate.Missing.Count == 0 ? "the group" : string.Join(", ", podGate.Missing);
			_log.LogInformation(
				"[PodTrigger] {Sender}'s pod request waits for every living member: {Living} living, {AtPod} at the pod; missing: {Missing}.",
				sender, podGate.Living, podGate.AtPod, waiting);
			_layerAdvance.RefuseLayerAdvance(sender, "every living member must be at the drill pod; still waiting for: " + waiting + ".");
			return;
		}

		var decision = LayerAdvancePolicy.DecideDrive(
			_session.Role, _session.SessionActive, world.worldExists, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating());
		if (decision != LayerAdvanceDecision.Drive)
		{
			_log.LogInformation("[LayerChoice] {Sender}'s choice did not drive an advance ({Decision}) — the layer stays where it is.", sender, decision);
			_layerAdvance.RefuseLayerAdvance(sender, LayerAdvancePolicy.DescribeRefusal(decision));
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
		// The step is the requester's own: the drill pod's descent is two layers, the panel's is one. The
		// members follow through BeginLocalDescent with the same flag, so the rider's pod arrival effects run
		// once on the body that rode.
		world.StartCoroutine(world.RegenerateWorld(twice));
		_log.LogInformation("[LayerChoice] {Sender}'s choice drives the layer advance (twice: {Twice}) — this host regenerates the layer and the members follow its baseline.", sender, twice);
	}
}
