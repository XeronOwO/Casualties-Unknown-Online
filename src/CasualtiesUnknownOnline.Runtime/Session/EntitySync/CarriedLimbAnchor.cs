using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The exact-limb-pose reference shape of a carried rider clone, kept so the
/// rendered limbs can be MEASURED against the root the ride pose pinned.
///
/// Why it is only a measurement. The rider-teleport ticket's 2026-09-07 note
/// ends with a hypothesis: with an exact limb pose active, <c>HandleVisuals</c>
/// no longer re-attaches the limbs to the body root, so a root re-pinned every
/// frame leaves the visible limbs at the previous pose tick's world coordinates.
/// The current code contradicts the causal half of it: the visible limb
/// transforms are transform CHILDREN inside the body's own hierarchy
/// (<c>Body.HandleVisuals</c> places them with a local write derived from the
/// animator node, and <c>Body.Stand</c> counter-moves every limb by the root's
/// own shift), and a remote clone's limb rigidbodies are frozen
/// (<c>RemoteBodyFactory</c>), so a Body-root write carries the limbs with it.
/// The carried LOCAL rider is the control case: its root is teleported every
/// frame with every rigidbody frozen and nothing re-anchors it, and no report
/// describes its body coming apart.
///
/// So nothing may reposition limbs on that hypothesis. What is recorded here
/// instead is the shape the exact poses were applied as, relative to the root at
/// application time and in application (parents-first) order; the ride pose then
/// compares each rendered limb against the position a rigid follow of the pinned
/// root would put it at. Zero means the hierarchy already carried the limbs (the
/// expected result); a non-zero reading is the runtime evidence that the
/// hypothesis is live, on the screen, for that limb.
/// </summary>
public sealed class CarriedLimbAnchor
{
	private readonly List<int> _limbIndices = [];
	private readonly List<float> _offsetX = [];
	private readonly List<float> _offsetY = [];

	/// <summary>
	/// Whether a clone's exact limb poses are compared against its pinned body
	/// root. True for a carried rider clone with an active exact pose: its root
	/// is placed on the carrier by the ride pose while the limbs are placed by
	/// the pose stream, which is the only combination the hypothesis is about. An
	/// ordinary ragdoll clone takes root and limbs from the same tick, and a clone
	/// without exact poses is animator-driven, so neither is measured.
	/// </summary>
	public static bool ShouldMeasureAgainstPinnedRoot(bool isCarriedRider, bool hasExactLimbPose) =>
		isCarriedRider && hasExactLimbPose;

	/// <summary>Whether no shape is captured (no exact limb pose is being rendered).</summary>
	public bool IsEmpty => _limbIndices.Count == 0;

	/// <summary>Number of limbs in the captured shape, in application order.</summary>
	public int Count => _limbIndices.Count;

	/// <summary>
	/// Drops the captured shape: the stream stopped carrying exact poses, the
	/// clone stopped being a carried rider, or the relation ended. A reading
	/// taken after this is no reading at all, which is why every release and
	/// detach path clears it.
	/// </summary>
	public void Clear()
	{
		_limbIndices.Clear();
		_offsetX.Clear();
		_offsetY.Clear();
	}

	/// <summary>
	/// Captures one applied limb pose as its offset from the body root at
	/// application time. Call order is preserved, and it must stay the order the
	/// poses were applied in (parents-first), so a nested limb is compared after
	/// the parent whose move it inherits.
	/// </summary>
	public void Capture(int limbIndex, float limbX, float limbY, float rootX, float rootY)
	{
		_limbIndices.Add(limbIndex);
		_offsetX.Add(limbX - rootX);
		_offsetY.Add(limbY - rootY);
	}

	/// <summary>
	/// The world position the captured limb at <paramref name="order"/> would be
	/// rendered at if it followed the body root rigidly — the reference a
	/// rendered limb is measured against for a root at
	/// (<paramref name="rootX"/>, <paramref name="rootY"/>).
	/// </summary>
	public bool TryTarget(int order, float rootX, float rootY, out int limbIndex, out float x, out float y)
	{
		if (order < 0 || order >= _limbIndices.Count)
		{
			limbIndex = 0;
			x = 0f;
			y = 0f;
			return false;
		}

		limbIndex = _limbIndices[order];
		x = _offsetX[order] + rootX;
		y = _offsetY[order] + rootY;
		return true;
	}
}
