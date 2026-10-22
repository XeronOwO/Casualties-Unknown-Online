using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The receiving client's own placement answer, asked BEFORE the item exists here: a free body slot by the
/// game's own pickup gate (<see cref="LocalBodyPlacement.WouldAccept"/>), or a container's own rules
/// (<c>Container.CanHoldItem</c> plus the two refusals <c>Container.LoadItem</c> adds after it: an item that is
/// itself a container with holdings, and a destination container that is itself nested). It is the answer the
/// give's custody waits for, so it mirrors the placement's own sequence — the probe carries the item's real
/// condition, so its weight is <c>Item.totalWeight</c>'s condition-scaled value rather than the prefab default.
///
/// <para>
/// The rules need a real <c>Item</c>, so this creates a THROWAWAY prefab instance of the definition and destroys
/// it in the same call. It carries no instance id, which is what makes the destroy unobservable: the item trace's
/// zero-id rule reports nothing, no character snapshot names it, and the object lives only for the frame the
/// question is answered in. The creation sits inside the try: a definition this client cannot load throws from
/// <c>Utils.Create</c>, and that must become a named refusal instead of an unanswered question.
/// </para>
///
/// <para>
/// A placement answer is a QUESTION answer, not a mutation: this client writes nothing, so a refusal leaves the
/// item with the giver and this client's body untouched.
/// </para>
/// </summary>
internal sealed class RemotePlacementResolver(ILogger<RemotePlacementResolver> log) : IRemotePlacementResolver
{
	public RemotePlacementResolution Resolve(CharacterItemMsg item, int requestedSlot, ulong requestedContainerInstanceId)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return RemotePlacementResolution.Refused("this client's body is not ready, so it cannot answer where an item would land.");
		}

		if (item.InstanceId == 0 || string.IsNullOrEmpty(item.ItemId))
		{
			return RemotePlacementResolution.Refused("the item carries no definition this client could place.");
		}

		GameObject? created = null;
		try
		{
			created = Utils.Create(item.ItemId, body.transform.position, 0f);
			var probe = created.GetComponent<Item>();
			if (probe == null) // Unity object — ==
			{
				return RemotePlacementResolution.Refused($"the item's definition '{item.ItemId}' cannot be created on this client, so its rules cannot answer for it.");
			}

			// The placement restores the item's real condition before its checks, so the weight the container
			// rules see must be the same condition-scaled totalWeight — not the prefab's default.
			probe.condition = item.Condition;

			if (requestedContainerInstanceId != 0)
			{
				var parent = CarriedItemLocator.FindById(body, requestedContainerInstanceId);
				var container = parent == null ? null : parent.GetComponent<Container>(); // Unity objects — ==
				if (container == null) // Unity object — ==
				{
					return RemotePlacementResolution.Refused($"the target {requestedContainerInstanceId} is not a container on this client.");
				}

				// Container.LoadItem's own two refusals, checked before CanHoldItem just as the placement meets
				// them in that order: a non-empty container cannot go into another container, and a destination
				// container inside another container can never be loaded into.
				if (item.Contents.Count > 0)
				{
					return RemotePlacementResolution.Refused($"{item.ItemId} holds items of its own, so it cannot go into container {parent!.id}.");
				}

				var parentItem = parent!.GetComponent<Item>();
				if (parentItem != null && parentItem.TryGetParentContainer(out _)) // Unity object — ==
				{
					return RemotePlacementResolution.Refused($"container {parent.id} is itself inside another container, so items cannot be loaded into it.");
				}

				if (!container.CanHoldItem(probe))
				{
					return RemotePlacementResolution.Refused($"{item.ItemId} does not fit container {parent!.id} by the container's own rules.");
				}

				return RemotePlacementResolution.IntoContainer(requestedContainerInstanceId);
			}

			var slot = LocalBodyPlacement.FirstAcceptingSlot(body, probe, requestedSlot);
			if (slot < 0)
			{
				return RemotePlacementResolution.Refused($"{item.ItemId} does not fit any free slot on this client's body.");
			}

			return RemotePlacementResolution.ToSlot(slot);
		}
		catch (Exception exception)
		{
			log.LogWarning(exception, "[Placement] the local rules failed while answering where {ItemId} (id {InstanceId}) would land.",
				item.ItemId, item.InstanceId);
			return RemotePlacementResolution.Refused("this client's rules could not answer where the item would land.");
		}
		finally
		{
			if (created != null) // Unity object — ==
			{
				Object.Destroy(created);
			}
		}
	}
}
