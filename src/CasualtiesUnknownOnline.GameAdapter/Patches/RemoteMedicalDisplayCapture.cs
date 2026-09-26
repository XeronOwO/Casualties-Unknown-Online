using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The position a remote medical step's gore clip must be reported at. The
/// remote medical view inspects a DISPLAY copy of the patient's body, and
/// <c>RemoteMedicalCoordinator.TryCreateDisplayBody</c> deliberately parks that
/// copy out of the world (<c>body.transform.position = new Vector3(0f, -10000f, 0f)</c>,
/// "keep it out of the world and out of all simulation/collision paths"). The
/// native gore call plays at the LIMB's and the BODY's own transform, so on the
/// remote path it would play — and be relayed — at (0, -10000): a 3D one-shot off
/// the map, inaudible to the operator and to every peer.
/// <para>
/// The capture window therefore moves the displayed body onto the patient's live
/// render clone for the duration of the step and restores the parked position
/// afterwards, so the clip is played (and reported) at a real world position,
/// next to the patient the peers can see. It is presentation-only: the copy is
/// inactive and out of simulation and collision either way, and every reader of
/// it reads state rather than a transform.
/// <c>RemoteMedicalOperationHandler.PlayTreatmentSound</c> re-points the same
/// fact for the treatment table (at the treated limb); this helper is that
/// re-point for the step scopes, whose native call decides its own transform.
/// </para>
/// </summary>
internal static class RemoteMedicalDisplayCapture
{
	/// <summary>
	/// Repositions the displayed body onto its owner's live render clone for one
	/// capture window, when the body the sound plays on IS that display copy.
	/// Returns an empty window for a local body (a real in-world body, no move
	/// needed) and for a body whose clone is not rendered yet (the peers' replay
	/// then stays at the reported position, which is the best fact available).
	/// </summary>
	internal static IDisposable Enter(Body? body)
	{
		if (body == null // Unity object — ==
			|| !body.name.StartsWith("MedicalDisplay_", StringComparison.Ordinal)
			|| RemoteMedicalView.TargetSteamId == 0
			|| PatchBridge.Impl is not IPlayerAnchorQuery anchors
			|| !anchors.TryGetRemoteHeadPosition(RemoteMedicalView.TargetSteamId, out var x, out var y))
		{
			return EmptyWindow.Instance;
		}

		var previous = body.transform.position;
		body.transform.position = new Vector3(x, y, previous.z);
		return new Window(body, previous);
	}

	/// <summary>The no-move window: the clip plays where the native call placed it.</summary>
	private sealed class EmptyWindow : IDisposable
	{
		internal static readonly EmptyWindow Instance = new();

		public void Dispose()
		{
		}
	}

	/// <summary>One repositioned display body, restored when the capture window closes.</summary>
	private sealed class Window(Body body, Vector3 previous) : IDisposable
	{
		private readonly Body _body = body;
		private readonly Vector3 _previous = previous;

		public void Dispose() => _body.transform.position = _previous;
	}
}
