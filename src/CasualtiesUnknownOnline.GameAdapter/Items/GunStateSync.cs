using System.Runtime.CompilerServices;
using CasualtiesUnknownOnline.GameAdapter.Character;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The gun-state report side: GunScript's persistent fields (chamber,
/// magazine, racked, safe and condition) are already part of the [Saveable]
/// component digest, but the native fire/rack/load/unload/safety transitions
/// had no dedicated report — they only reached the host and the peer clones on
/// the next 1 Hz character snapshot. This domain owns the per-instance
/// "last reported snapshot" and reports every actual persistent-state change
/// through the existing item-use fact path (one operation = one message, the
/// same accept-with-correction shape as a use). The Harmony patches are thin:
/// they only call <see cref="TryReport"/> after the native transition; the
/// deduplication state belongs here, not in the patch.
/// </summary>
internal sealed class GunStateSync(ItemUseSync itemUseSync, ILogger<GunStateSync> log)
{
	private readonly ItemUseSync _itemUseSync = itemUseSync;
	private readonly ILogger<GunStateSync> _log = log;
	private readonly ConditionalWeakTable<GunScript, Snapshot> _last = new();

	/// <summary>
	/// Called by the GunScript patches after any native gun-state transition
	/// (and after Update, so timed auto-rack/auto-unrack transitions are caught
	/// too). A remote render clone never reports; a first sight of a gun is not
	/// a state change and only starts the tracker.
	/// </summary>
	internal void TryReport(GunScript gun)
	{
		if (gun.GetComponentInParent<RemoteBodyDriver>() != null) // Unity object — ==
		{
			return;
		}

		var item = gun.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			return;
		}

		var current = Snapshot.Capture(item, gun);
		if (!_last.TryGetValue(gun, out var previous))
		{
			// First sight of this GunScript is not a transition: the starting
			// state already rides the carried-inventory/1 Hz snapshot paths.
			// Seed the tracker so the next actual change reports exactly once.
			_last.Add(gun, current);
			return;
		}

		if (previous.Matches(current))
		{
			return;
		}

		_last.Remove(gun);
		_last.Add(gun, current);

		_itemUseSync.OnItemUsed(item);
		_log.LogInformation(
			"[GunState] {Item} (id {ItemId}) reported: chamber={Chamber}, racked={Racked}, mag={Mag}, hasMag={HasMag}, safe={Safe}, condition={Condition:F3}.",
			item.id,
			item.GetComponent<ItemInstanceId>()?.Id ?? 0,
			current.RoundInChamber,
			current.Racked,
			current.RoundsInMag,
			current.HasMag,
			current.Safe,
			current.Condition);
	}

	/// <summary>
	/// The persistent gun facts that can diverge and that a peer clone needs to
	/// render/use correctly. Transient per-frame fields (triggerPressed,
	/// firingPinStruck) and the mirror field lastRacked are deliberately not
	/// part of the report signature — they are either re-derived by the game or
	/// not visible state.
	/// </summary>
	private sealed class Snapshot(float condition, int roundInChamber, int roundsInMag, bool hasMag, bool safe, bool racked)
	{
		internal readonly float Condition = condition;
		internal readonly int RoundInChamber = roundInChamber;
		internal readonly int RoundsInMag = roundsInMag;
		internal readonly bool HasMag = hasMag;
		internal readonly bool Safe = safe;
		internal readonly bool Racked = racked;

		internal static Snapshot Capture(Item item, GunScript gun) =>
			new(item.condition, (int)gun.roundInChamber, gun.roundsInMag, gun.hasMag, gun.safe, gun.racked);

		internal bool Matches(Snapshot other) =>
			Condition == other.Condition
			&& RoundInChamber == other.RoundInChamber
			&& RoundsInMag == other.RoundsInMag
			&& HasMag == other.HasMag
			&& Safe == other.Safe
			&& Racked == other.Racked;
	}
}
