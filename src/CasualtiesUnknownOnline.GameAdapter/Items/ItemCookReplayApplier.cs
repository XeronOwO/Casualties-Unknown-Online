using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Logger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The heater-cook replay apply side for remote world items: one host→guest
/// ItemCook event replays the native conversion atomically in ONE RemoteApply
/// scope — kill the raw-meat copy, materialize the cooked steak from the full
/// carried state, then replay the game's Scald sound exactly once. Both
/// operations are idempotent: a missing source is fine (the message fact still
/// spawns the steak), a duplicate cooked id is skipped.
/// </summary>
internal sealed class ItemCookReplayApplier(
	ItemApplication owner,
	StandingItemMaterializer standing,
	ISessionControl session,
	Logger log)
{
	private readonly ItemApplication _owner = owner;
	private readonly StandingItemMaterializer _standing = standing;
	private readonly ISessionControl _session = session;
	private readonly Logger _log = log;

	public void OnRemoteItemCooked(ulong sourceItemId, WorldItem cooked)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var source = ItemApplication.FindWorldItem(sourceItemId);
			if (source != null) // Unity object — ==
			{
				_owner.KillRemoteItem(source);
			}
			else
			{
				_log.LogWarning("[ItemCook] source {Source} not present locally — spawning the cooked item from the event fact.", sourceItemId);
			}

			// The cooked row is a WORLD fact, so a standing incarnation of that id must go before the
			// duplicate check below — this path branches before the materialization funnel, and the standing
			// object would otherwise answer the check and keep the steak from ever being materialized.
			_standing.Retire(cooked.ItemId, "the cooked row is a world fact");

			if (ItemApplication.FindWorldItem(cooked.ItemId) != null) // Unity object — ==; reliable-channel duplicate
			{
				_log.LogInformation("[ItemCook] cooked item {Cooked} already present — duplicate event ignored.", cooked.ItemId);
				return;
			}

			_owner.SpawnWorldItem(cooked);

			// The game plays the Scald sound on the conversion side
			// (Heater.cs:48). The guest's isolated layer prevents the native
			// conversion and therefore the native sound, so replay the same
			// call once here — same positional sound, same one Random pitch
			// consumption as the host's native side.
			if (_session.Role == SessionRole.Guest)
			{
				Sound.Play("Scald", new Vector2(cooked.Pos.X, cooked.Pos.Y), false, true, null, 1f, 1f, false, false);
			}
		}
	}
}
