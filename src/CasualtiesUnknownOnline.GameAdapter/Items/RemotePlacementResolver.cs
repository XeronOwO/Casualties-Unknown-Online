using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The receiving client's own placement answer, asked BEFORE the item exists here: a free body slot by the
/// game's own pickup gate (<see cref="LocalBodyPlacement.WouldAccept"/>), or a container's own
/// <c>Container.CanHoldItem</c>. It is the answer the give's custody waits for, so it runs the same rules the
/// placement will run — one resolution, not two that can drift.
///
/// <para>
/// The rules need a real <c>Item</c>, so this creates a THROWAWAY prefab instance of the definition and destroys
/// it in the same call. It carries no instance id, which is what makes the destroy unobservable: the item trace's
/// zero-id rule reports nothing, no character snapshot names it, and the object lives only for the frame the
/// question is answered in.
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

		var created = Utils.Create(item.ItemId, body.transform.position, 0f);
		var probe = created != null ? created.GetComponent<Item>() : null; // Unity objects — ==
		if (probe == null) // Unity object — ==
		{
			if (created != null) // Unity object — ==
			{
				Object.Destroy(created);
			}

			return RemotePlacementResolution.Refused($"the item's definition '{item.ItemId}' cannot be created on this client, so its rules cannot answer for it.");
		}

		try
		{
			if (requestedContainerInstanceId != 0)
			{
				var parent = CarriedItemLocator.FindById(body, requestedContainerInstanceId);
				var container = parent == null ? null : parent.GetComponent<Container>(); // Unity objects — ==
				if (container == null) // Unity object — ==
				{
					return RemotePlacementResolution.Refused($"the target {requestedContainerInstanceId} is not a container on this client.");
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
			Object.Destroy(created);
		}
	}
}
