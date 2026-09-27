using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The carried rider's two READINGS, and the reports that carry them out of a
/// player's default session: how far a rendered exact limb pose was left behind
/// the body root the ride pose pinned (<c>limbSeparation</c>), and how far the
/// clone was RENDERED from the position its last carry pin wrote for it, relative
/// to its carrier (<c>riderDrift</c>). Both are zero when the carry relation does
/// its job, and both are read-only — nothing here places anything.
///
/// Why readings rather than a fix. The rider-teleport ticket records a mechanism
/// — "the mount moved only the Body root and the visible limbs stayed at the
/// previous pose tick's world coordinates" — that the current code contradicts
/// (the visible limbs are transform children of the body hierarchy, and a remote
/// clone's limb rigidbodies are frozen) and that no session has ever verified.
/// So this type OBSERVES and RENDERS: every reading is printed for every clone it
/// can be taken on, ZERO INCLUDED, so that "measured zero" and "not measured"
/// cannot be confused, and which window has to report what is decided by
/// <see cref="CarryPresentationReading.Anomalies"/> — a Runtime rule a test can
/// settle without a game — and only rendered here as a WARNING, because a
/// reportable reading is the defect being live on that screen and must not sit
/// behind a Debug log level nobody would raise before judging a motion artifact.
/// </summary>
internal static class CarryPresentationProbe
{
	/// <summary>
	/// Records the placement a carry pin just wrote, so the next frame can read
	/// whether it survived to the frame that rendered, and counts the pin into the
	/// window's "was this clone pinned at all". The offset is stored
	/// rider-minus-anchor: it is invariant while the rider rides its carrier, so
	/// the carrier's own motion, a facing flip and a crouch change cannot read as
	/// a problem. Called AFTER the ride pose wrote the root, never before.
	/// </summary>
	public static void Store(Body riderClone, ulong carrierSteamId, bool localCarrier, Vector3 anchorPosition)
	{
		var driver = riderClone.GetComponent<RemoteBodyDriver>();
		if (driver == null) // Unity object — ==
		{
			return;
		}

		var position = riderClone.transform.position;
		CarryPresentationReading.Offset(
			position.x, position.y, anchorPosition.x, anchorPosition.y,
			out var offsetX, out var offsetY);
		driver.PinnedCarrierSteamId = carrierSteamId;
		driver.PinnedToLocalCarrier = localCarrier;
		driver.PinnedOffsetX = offsetX;
		driver.PinnedOffsetY = offsetY;
		driver.PinCountInWindow++;
	}

	/// <summary>Drops a clone's stored reference: the relation it was written for is gone.</summary>
	public static void Clear(Body riderClone)
	{
		var driver = riderClone.GetComponent<RemoteBodyDriver>();
		if (driver != null) // Unity object — ==
		{
			Clear(driver);
		}
	}

	/// <summary>
	/// Drops the stored reference — the carrier it was written against and the
	/// offset it wrote — because the rig the next reading would be taken against
	/// no longer exists (the relation ended, or another carrier took it). The
	/// WINDOW is deliberately untouched: a reading taken while the pin was in
	/// force is still the window's reading, and dropping it here is how a real
	/// drift gets erased by the release that followed it.
	/// </summary>
	public static void Clear(RemoteBodyDriver driver)
	{
		driver.PinnedCarrierSteamId = 0;
		driver.PinnedToLocalCarrier = false;
		driver.PinnedOffsetX = 0f;
		driver.PinnedOffsetY = 0f;
	}

	/// <summary>
	/// Adds one rendered-position reading to the driver's 1 Hz window: the
	/// distance between where the clone is NOW and where the stored pin put it,
	/// both measured against the same anchor — which is why the carrier's own
	/// travel cancels out and only a rider moved on its own reads as drift.
	/// </summary>
	public static void RecordDrift(RemoteBodyDriver driver, Vector3 riderPosition, Vector3 anchorPosition)
	{
		var drift = CarryPresentationReading.Drift(
			riderPosition.x, riderPosition.y, anchorPosition.x, anchorPosition.y,
			driver.PinnedOffsetX, driver.PinnedOffsetY);
		if (drift > driver.PinDriftWindowMax)
		{
			driver.PinDriftWindowMax = drift;
		}
	}

	/// <summary>
	/// The reading half of one clone's 1 Hz diagnostic line: whether a pin was in
	/// force in this window, and both readings — each printed for every clone it
	/// applies to with zero included, so a quiet window and an unmeasured one stay
	/// distinguishable (a window with no pin prints no drift reading, and the
	/// no-pin anomaly is what says so). Appended to the caller's own tags, so the
	/// line stays one line and the caller keeps owning the position/tag facts it
	/// already had.
	/// </summary>
	public static string Describe(RemoteBodyDriver driver)
	{
		var pinnedInWindow = driver.PinCountInWindow > 0;
		var text = pinnedInWindow ? ", pinned-to-carrier" : "";
		if (driver.RagdollPoseActive)
		{
			text += $", limbSeparation={driver.LimbSeparationWindowMax.ToString("0.###", CultureInfo.InvariantCulture)}";
		}

		if (pinnedInWindow)
		{
			text += $", riderDrift={driver.PinDriftWindowMax.ToString("0.###", CultureInfo.InvariantCulture)}";
		}

		return text;
	}

	/// <summary>
	/// Renders one carry participant's window: what
	/// <see cref="CarryPresentationReading.Anomalies"/> decides, at WARNING, with
	/// the numbers and the carrier that produced them. Every report is the
	/// acceptance run's eye-free evidence, so none of them may wait for someone to
	/// raise the log level.
	/// </summary>
	public static void Report(ILogger log, ulong steamId, RemoteBodyDriver driver)
	{
		var anomalies = CarryPresentationReading.Anomalies(
			pinnedInWindow: driver.PinCountInWindow > 0,
			limbSeparation: driver.LimbSeparationWindowMax,
			riderDrift: driver.PinDriftWindowMax,
			carriedRider: driver.IsCarriedRider);

		if (anomalies.LimbSeparation)
		{
			log.LogWarning(
				"Carried rider clone {SteamId}: exact limb poses were left {Separation:0.###} world units behind the body root the ride pose pinned, so the limbs did not travel with it.",
				steamId, driver.LimbSeparationWindowMax);
		}

		if (anomalies.RiderDrift)
		{
			log.LogWarning(
				"Carried rider clone {SteamId}: the rendered frame showed it {Drift:0.###} world units away from the position its carry pin wrote for it (carrier {CarrierSteamId}), so the pair was drawn apart.",
				steamId, driver.PinDriftWindowMax, driver.PinnedCarrierSteamId);
		}

		if (anomalies.NoCarryPin)
		{
			log.LogWarning(
				"Carried rider clone {SteamId}: the carry relation is live but no carry pin was in force for the whole window, so not one frame of it was measured against a carrier.",
				steamId);
		}
	}
}
