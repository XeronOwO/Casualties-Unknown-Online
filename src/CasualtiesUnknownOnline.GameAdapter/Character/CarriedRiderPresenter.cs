using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The carry presentation on one client's screens: the neutral-scale mount a
/// remote rider clone root is re-parented under when the LOCAL player is the
/// carrier, the per-frame pin of every carried rider clone to its carrier's
/// visual anchor (the local body, or that carrier's render clone on a
/// third-party view), and the read-only pin-drift reading of whether the frame
/// that rendered showed the clone where the pin put it. It observes and renders
/// only — nothing here simulates a remote body. The renderer owns the clone
/// lifecycle and the state stream; this type owns what the carry relation does
/// to those clones once they exist.
/// </summary>
internal sealed class CarriedRiderPresenter(
	ISessionControl session,
	IPlayerInteractionControl playerInteraction)
{
	private readonly ISessionControl _session = session;
	private readonly IPlayerInteractionControl _playerInteraction = playerInteraction;

	/// <summary>
	/// Name of the neutral-scale child mount placed under a carrier's Body
	/// transform. Remote rider clone roots are re-parented under this mount so
	/// the rider is a true descendant of the carrier and follows the carrier's
	/// final rendered transform (including any physics interpolation Unity
	/// applies after LateUpdate). The mount's scale is the inverse of the
	/// carrier's world scale, so the rider keeps its own normal facing scale.
	/// </summary>
	private const string CarryMountName = "CUO_CarryMount";

	/// <summary>
	/// Marks a remote clone's carry role BEFORE the caller applies stream state,
	/// so SessionStatePump can suppress the native sit replay in the same frame.
	/// This is not limited to the local carrier's view: a third-party rider clone
	/// also rides, and the attach pass forces its visible position anyway. A
	/// carrier participates in the same whole-family sit suppression.
	/// </summary>
	public void MarkCarryRole(ulong steamId, RemoteBodyDriver? driver)
	{
		if (driver == null) // Unity object — ==
		{
			return;
		}

		driver.IsCarriedRider = _playerInteraction.TryGetCarrier(steamId, out var carrierId)
			&& carrierId != 0;
		driver.IsCarrier = _playerInteraction.TryGetCarried(steamId, out _);
	}

	/// <summary>
	/// Whether a clone root hangs under the local carrier's carry mount — the
	/// fact the 1 Hz clone diagnostic prints as <c>mounted-to-local-carrier</c>,
	/// so the mount path is readable without a visual check.
	/// </summary>
	public bool IsMountedToLocalCarrier(Body? clone) =>
		clone != null
		&& clone.transform.parent != null
		&& clone.transform.parent.parent != null
		&& clone.transform.parent.parent.name == CarryMountName;

	/// <summary>
	/// Pins every remote clone that is currently a carried rider to its
	/// carrier's visual position after all clones have been interpolated this
	/// frame. For a local carrier the anchor is the local body; for any other
	/// carrier (third-party view) the anchor is that carrier's already-smoothed
	/// render clone. This keeps the carry pair visually rigid on every side —
	/// the per-entity interpolator may lag, but the rider always rides the same
	/// displayed carrier, never an independent smoothed point.
	/// In addition to writing the world position, a rider clone whose carrier
	/// is the LOCAL player is re-parented under a neutral-scale mount on the
	/// carrier Body. A true descendant follows the local carrier's final
	/// rendered transform no matter what moves the carrier after this pass
	/// (frame ordering, Rigidbody render interpolation, or a final render-time
	/// pose). Third-party remote carriers use the world-space pin only, because
	/// their clones are CUO-driven frozen transforms with no render
	/// interpolation and should not become children of another remote clone's
	/// hierarchy.
	/// </summary>
	public void AttachAll(Body? localBody, IReadOnlyDictionary<ulong, Body> clones)
	{
		foreach (var entry in clones)
		{
			var riderSteamId = entry.Key;
			var riderClone = entry.Value;
			// == null on Unity clones — a scene reload can destroy one between
			// the first pass and this diagnostic/second pass.
			if (riderClone == null)
			{
				continue;
			}

			if (!_playerInteraction.TryGetCarrier(riderSteamId, out var carrierSteamId)
				|| carrierSteamId == 0)
			{
				DetachCarriedRiderRoot(riderClone);
				CarryPresentationProbe.Clear(riderClone);
				continue;
			}

			if (carrierSteamId == _session.LocalSteamId)
			{
				if (localBody == null || localBody == riderClone) // Unity objects — ==
				{
					DetachCarriedRiderRoot(riderClone);
					continue;
				}

				var mount = GetOrCreateCarryMount(localBody.transform);
				AttachCarriedRiderRoot(riderClone, mount);
				CarriedBodyPlacement.ApplyRidePose(
					riderClone,
					localBody.transform.position,
					localBody.isRight,
					localBody.crouching,
					localBody.rb.velocity,
					localBody.targetLookPos);
				CarryPresentationProbe.Store(riderClone, carrierSteamId, localCarrier: true, localBody.transform.position);
				continue;
			}

			if (clones.TryGetValue(carrierSteamId, out var carrierClone)
				&& carrierClone != null) // Unity object — ==
			{
				// Third-party views have two CUO-driven frozen clones; they are
				// already placed by the same SessionStatePump pass and never go
				// through Unity Rigidbody render interpolation, so the existing
				// world-space pin is sufficient here. Mounting under a remote
				// carrier would make the rider clone a child of another remote's
				// hierarchy and therefore be destroyed when that carrier clone
				// leaves — unnecessary collateral for this case.
				DetachCarriedRiderRoot(riderClone);
				CarriedBodyPlacement.ApplyRidePose(
					riderClone,
					carrierClone.transform.position,
					carrierClone.isRight,
					carrierClone.crouching,
					carrierClone.rb.velocity,
					carrierClone.targetLookPos);
				CarryPresentationProbe.Store(riderClone, carrierSteamId, localCarrier: false, carrierClone.transform.position);
				continue;
			}

			// No carrier clone yet (still creating or in a menu scene): keep
			// the ordinary SessionStatePump fallback until the carrier exists.
			DetachCarriedRiderRoot(riderClone);
		}
	}

	/// <summary>
	/// Reads how far this clone was RENDERED from the position its carry pin wrote
	/// for it, relative to its carrier, and keeps the largest value of the current
	/// 1 Hz window. Called by the renderer at the top of its per-clone pass, before
	/// <c>SessionStatePump.Apply</c> overwrites the clone, so the transform still
	/// holds what the frame that rendered showed. What the reading covers — and the
	/// frame a carrier moved after its own pin still reads as zero — is stated on
	/// <see cref="CarryPresentationReading.Drift"/>.
	///
	/// The reference is dropped and no reading taken when the relation it was
	/// written for is gone; an anchor merely unavailable this frame keeps it, and
	/// the frames that follow measure what accumulated while it was missing.
	/// </summary>
	public void MeasurePinDrift(
		Body? localBody,
		ulong riderSteamId,
		Body riderClone,
		RemoteBodyDriver? cloneDriver,
		IReadOnlyDictionary<ulong, Body> clones)
	{
		if (cloneDriver == null || cloneDriver.PinnedCarrierSteamId == 0) // Unity object — ==
		{
			return;
		}

		if (!_playerInteraction.TryGetCarrier(riderSteamId, out var carrierSteamId)
			|| carrierSteamId != cloneDriver.PinnedCarrierSteamId)
		{
			CarryPresentationProbe.Clear(cloneDriver);
			return;
		}

		if (!TryResolvePinAnchor(cloneDriver, localBody, riderClone, clones, out var anchor))
		{
			return;
		}

		CarryPresentationProbe.RecordDrift(cloneDriver, riderClone.transform.position, anchor);
	}

	/// <summary>
	/// The anchor a stored pin was written against, resolved exactly the way the
	/// pin resolved it: the local body when the local player is the carrier,
	/// otherwise that carrier's render clone. False when the anchor does not
	/// exist this frame.
	/// </summary>
	private bool TryResolvePinAnchor(
		RemoteBodyDriver driver,
		Body? localBody,
		Body riderClone,
		IReadOnlyDictionary<ulong, Body> clones,
		out Vector3 anchor)
	{
		if (driver.PinnedToLocalCarrier)
		{
			if (localBody == null || localBody == riderClone) // Unity objects — ==
			{
				anchor = Vector3.zero;
				return false;
			}

			anchor = localBody.transform.position;
			return true;
		}

		if (clones.TryGetValue(driver.PinnedCarrierSteamId, out var carrierClone)
			&& carrierClone != null) // Unity object — ==
		{
			anchor = carrierClone.transform.position;
			return true;
		}

		anchor = Vector3.zero;
		return false;
	}

	/// <summary>
	/// Finds or creates the neutral-scale carry mount under a carrier Body.
	/// The mount is an empty direct child; its localScale is the inverse of the
	/// carrier's world scale so a rider parented beneath it keeps the same
	/// world-space scale/meaning it had before being attached.
	/// </summary>
	private static Transform GetOrCreateCarryMount(Transform carrierTransform)
	{
		var mount = carrierTransform.Find(CarryMountName);
		if (mount == null) // Unity object — ==
		{
			var mountObject = new GameObject(CarryMountName);
			mount = mountObject.transform;
			mount.SetParent(carrierTransform, false);
		}

		mount.localScale = CarriedBodyPlacement.CarryMountScale(carrierTransform.lossyScale);
		return mount;
	}

	/// <summary>
	/// Re-parents a remote clone root under a carry mount. The clone root stays
	/// the parent of the Body, so the existing destroy path
	/// (<c>Object.Destroy(clone.transform.parent.gameObject)</c>) still removes
	/// the whole remote player.
	/// </summary>
	private static void AttachCarriedRiderRoot(Body riderClone, Transform mount)
	{
		var root = riderClone.transform.parent;
		if (root == null) // Unity object — ==
		{
			return;
		}

		if (root.parent != mount) // Unity object — ==
		{
			root.SetParent(mount, worldPositionStays: true);
		}
	}

	/// <summary>
	/// Restores a remote clone root to the scene root when it is no longer a
	/// carried rider (release, cleared relation, missing carrier clone, or a
	/// scene reload). Without this, a formerly carried clone would keep
	/// inheriting the old carrier's transform and could not be driven by the
	/// ordinary state stream again.
	/// </summary>
	private static void DetachCarriedRiderRoot(Body riderClone)
	{
		// The clone is not a carried rider on this frame: drop the exact-pose
		// reference shape with the mount, so a later re-attach cannot be measured
		// (or reported) against a shape and a root that belong to the old
		// relation.
		var driver = riderClone.GetComponent<RemoteBodyDriver>();
		if (driver != null) // Unity object — ==
		{
			driver.LimbAnchor.Clear();
			driver.LimbSeparationWindowMax = 0f;
		}

		var root = riderClone.transform.parent;
		if (root == null) // Unity object — ==
		{
			return;
		}

		var parent = root.parent;
		if (parent == null || parent.name != CarryMountName) // Unity object — ==
		{
			return;
		}

		root.SetParent(null, worldPositionStays: true);
		if (parent.childCount == 0)
		{
			Object.Destroy(parent.gameObject);
		}
	}
}
