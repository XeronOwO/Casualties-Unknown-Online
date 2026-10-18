using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The two native LIMB-ITEM transforms, observed on the client that performs them —
/// the adapter's half of the ruled model in which an applied item keeps its instance
/// id and gains a location on the limb.
/// <list type="number">
/// <item>APPLY (<see cref="OnItemAppliedToLimb"/>): the local body's own
/// <c>useLimbAction</c> turned a carried item into a <c>SplintLimb</c> /
/// <c>TourniquetScript</c>. Reported as a RELOCATION from the seam that ran the
/// action, so the generic <c>Item.OnDestroy</c> hook does not report the deferred
/// destroy as the item's end — the item is on the limb, not gone.</item>
/// <item>TAKE-OFF (<see cref="OnLimbComponentTakeOffBegin"/> /
/// <see cref="OnItemAwake"/> / <see cref="OnLimbComponentTakeOffEnd"/>): the
/// component's own <c>TakeOff</c> instantiates the item it revives. That object must
/// wear the id the APPLIED ROW carries, which is why the id is stamped in
/// <c>Item.Awake</c> — the one moment the object exists inside the native call, and
/// the same hook the building-death-drop marker already uses.</item>
/// </list>
/// <para>
/// Both halves read the applied row rather than a local memo: the item row is the
/// authority for WHICH item is installed, the limb row for the component being there.
/// A component the rows do not name is the two-carriers DIVERGENCE, and it is named
/// and left alone rather than answered with an invented id.
/// </para>
/// </summary>
internal sealed class LimbItemTransform(ISessionControl session, IItemControl items, ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly IItemControl _items = items;
	private readonly ILogger _log = log;

	/// <summary>The take-off whose object has not been created yet, if one is in flight.</summary>
	private PendingTakeOff? _pendingTakeOff;

	/// <summary>
	/// The scope opened around the native <c>TakeOff</c> this seam armed. It marks the call as
	/// the removal's own internal pickup (see <see cref="CallContext.Origin.LimbComponentTakeOff"/>):
	/// the game's <c>AutoPickUpItem</c> inside the call would otherwise report a pickup of an item
	/// the host's row still holds as APPLIED — refused, and the refusal rolled the live object out
	/// into the world (batch <c>20261018-u</c>'s row I4). Held here because the two patch halves
	/// are one native call; released at the postfix, and by the frame boundary if the native call
	/// throws and Harmony skips the postfix.
	/// </summary>
	private IDisposable? _takeOffScope;

	/// <summary>
	/// The local body's limb action installed <paramref name="item"/> as the component of
	/// <paramref name="limb"/>. Only the side that ran the delegate can see this, and only
	/// it can report it: the item keeps its id, so the fact is a relocation.
	/// </summary>
	internal void OnItemAppliedToLimb(Item item, Limb limb)
	{
		if (item == null || limb == null) // Unity objects — ==
		{
			return;
		}

		var idComponent = item.GetComponent<ItemInstanceId>();
		if (idComponent == null || idComponent.Id == 0) // Unity object — ==
		{
			// An id-less object is one CUO never tracked: the world's generation-time items and
			// the tutorial props are left id-less on purpose. There is no id to keep, and the
			// applied row has nothing to relocate — named, never invented.
			_log.LogWarning(
				"[LimbItem] the local limb action of {Type} installed the item on a limb, but the item carries no instance id — no applied row was reported.",
				item.id);
			return;
		}

		var limbIndex = Array.IndexOf(limb.body.limbs, limb);
		if (limbIndex < 0)
		{
			_log.LogWarning(
				"[LimbItem] the local limb action of {Type} (id {ItemId}) installed the item on a limb that is not part of its own body's layout — no applied row was reported.",
				item.id, idComponent.Id);
			return;
		}

		// The marker first: the delegate's destroy lands at the end of THIS frame, and the
		// generic hook must already know this is not an end when it gets there.
		item.gameObject.AddComponent<LimbAppliedItemOrigin>();
		_items.SendItemAppliedToLimb(idComponent.Id, limbIndex);
		_log.LogInformation(
			"[LimbItem] the local limb action of {Type} (id {ItemId}) installed the item on limb {Limb}; it keeps its id and now lives on that limb.",
			item.id, idComponent.Id, limbIndex);
	}

	/// <summary>
	/// The local body's limb component is about to come off: the game's own <c>TakeOff</c>
	/// instantiates the item from the component's template, and that object must wear the id
	/// the applied row carries. Resolved BEFORE the object exists, because the id can only be
	/// stamped while the native call is still running.
	/// </summary>
	internal void OnLimbComponentTakeOffBegin(GameObject limbObject, string definitionId)
	{
		_pendingTakeOff = null;
		if (limbObject == null || string.IsNullOrEmpty(definitionId)) // Unity object — ==
		{
			return;
		}

		var limb = limbObject.GetComponent<Limb>();
		if (limb == null || limb.body == null) // Unity objects — ==
		{
			return;
		}

		var limbIndex = Array.IndexOf(limb.body.limbs, limb);
		if (limbIndex < 0)
		{
			return;
		}

		var itemId = _items.FindAppliedLimbItem(_session.LocalSteamId, limbIndex, definitionId);
		if (itemId == 0)
		{
			// The divergence, named. The limb row says the component is there and the item rows
			// say no item is installed on it, so there is no id to revive: the object the native
			// call is about to create takes an ordinary new one, exactly as any item CUO never
			// saw installed would. Nothing is invented and nothing is moved.
			_log.LogError(
				"[LimbItem] the limb carries a {Definition} component but no applied item row names limb {Limb} of {Owner} — the item being taken off has no recorded id, so it becomes a new item instead of keeping one. This is the two-carriers divergence.",
				definitionId, limbIndex, _session.LocalSteamId);
			return;
		}

		_pendingTakeOff = new PendingTakeOff(itemId, limbIndex, definitionId);

		// The native call this prefix wraps is the removal's own run: the object it instantiates is
		// the item that comes back, and the pickup it performs is the relocation this seam reports
		// below — not a second, independent gesture. The scope is what tells the pickup and
		// container-load hooks to stay silent; a take-off with no applied row (the divergence
		// logged above) opens none, because the item it creates is genuinely new.
		_takeOffScope = CallContext.Enter(CallContext.Origin.LimbComponentTakeOff);
	}

	/// <summary>
	/// The native call returned. The object it created either adopted the applied id (and the
	/// relocation is reported here, once per gesture) or was never created — in which case the
	/// row stays applied and is named, because a silent no-op is the one outcome nobody could
	/// diagnose. <paramref name="nativeRan"/> is the postfix's <c>__runOriginal</c>: a prefix that
	/// skipped the call still leaves that postfix running, and then the armed record must be
	/// DROPPED without a report, since a removal that never happened must never reach the kernel.
	/// </summary>
	internal void OnLimbComponentTakeOffEnd(bool nativeRan)
	{
		// The native call is over, so the removal's own pickup scope ends before anything reports:
		// the item report below is the removal, and any later gesture on the revived object is an
		// ordinary one again.
		_takeOffScope?.Dispose();
		_takeOffScope = null;

		if (_pendingTakeOff is not { } pending)
		{
			return;
		}

		_pendingTakeOff = null;
		if (!nativeRan)
		{
			_log.LogInformation(
				"[LimbItem] the {Definition} component of limb {Limb} was not taken off: the native call was skipped, so item {ItemId} stays applied and nothing was reported.",
				pending.DefinitionId, pending.LimbIndex, pending.ItemId);
			return;
		}

		if (!pending.Adopted)
		{
			_log.LogWarning(
				"[LimbItem] the {Definition} component of limb {Limb} came off but created no item object — item {ItemId} stays applied on that limb, and no removal was reported.",
				pending.DefinitionId, pending.LimbIndex, pending.ItemId);
			return;
		}

		_items.SendItemRemovedFromLimb(pending.ItemId, pending.LimbIndex, RevivedState(pending));
		_log.LogInformation(
			"[LimbItem] the {Definition} component of limb {Limb} came off as item {ItemId}; the item kept the id it had while installed.",
			pending.DefinitionId, pending.LimbIndex, pending.ItemId);
	}

	/// <summary>
	/// The revived object's state at the moment it landed, so the relocation's row carries the
	/// real placement instead of the pre-apply one: by the time this runs, the game's own
	/// <c>AutoPickUpItem</c> inside <c>TakeOff</c> has put the item where the player will find it
	/// (a slot, a limb, a container, or — when nothing could take it — the world). <c>SlotOf</c>
	/// answers -1 for the last two, which is the honest "no slot or limb" the row should record.
	/// Null when the native call created no object (the removal was not reported either).
	/// </summary>
	private static CharacterItemMsg? RevivedState(PendingTakeOff pending) =>
		pending.Item is { } item && item != null // Unity object — ==
			? ItemStateCodec.CaptureItem(item, ItemStateCodec.SlotOf(item))
			: null;

	/// <summary>
	/// <c>Item.Awake</c>, which runs synchronously inside the <c>Object.Instantiate</c> a
	/// take-off makes. A take-off that resolved an applied row stamps that id here, so
	/// <c>Item.Start</c> finds an object that is already bound and reports no spawn for it —
	/// the removal relocation carries the fact instead. Hot path: every item instantiation
	/// calls this, so it leaves at the first null check.
	/// </summary>
	internal void TryAdoptLimbRemovalItem(Item item)
	{
		if (_pendingTakeOff is not { Adopted: false } pending)
		{
			return;
		}

		if (item == null || item.GetComponent<ItemInstanceId>() != null) // Unity objects — ==
		{
			return;
		}

		item.gameObject.AddComponent<ItemInstanceId>().Id = pending.ItemId;
		_pendingTakeOff = pending with { Adopted = true, Item = item };
	}

	/// <summary>The session ended: a take-off in flight belongs to the world it was started in.</summary>
	internal void ResetSessionState()
	{
		_takeOffScope?.Dispose();
		_takeOffScope = null;

		if (_pendingTakeOff is { } pending)
		{
			_log.LogInformation(
				"[LimbItem] session reset dropped an unsettled take-off of the {Definition} component on limb {Limb} (item {ItemId}).",
				pending.DefinitionId, pending.LimbIndex, pending.ItemId);
			_pendingTakeOff = null;
		}
	}

	private readonly record struct PendingTakeOff(ulong ItemId, int LimbIndex, string DefinitionId, bool Adopted = false, Item? Item = null);
}
