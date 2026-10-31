using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The container half of the remote inventory intents: take an item out of its container, move it into another, expand a container's children into one,
/// and drain a liquid stack. Every call runs the game's own container guard on the owner's real objects, so a move the native rule refuses (weight, tag,
/// nesting, distance) stays unperformed and is logged as refused instead of being forced.
/// </summary>
/// <remarks>
/// Its own type because these four intents share the container lookup and the "verify the write on the scene, not on the call" rule, and because the applier's
/// switch is a list of native calls: one family whose guards all live on the container is easier to check against the game than a section of a 600-line class.
/// </remarks>
internal sealed class ContainerIntentApplier(ILogger log)
{
	internal void TakeOut(Item item)
	{
		var parent = item.transform.parent;
		var container = parent != null ? parent.GetComponent<Container>() : null; // Unity object — ==
		if (container == null) // Unity object — ==
		{
			log.LogWarning("[RemoteIntent] take-out refused: item {Item} is not inside a container on the owner's body.", item.id);
			return;
		}

		container.UnloadItem(item, null);
	}

	/// <summary>The native move pair, evaluated by the container's own guards (weight, tags, nesting, distance).</summary>
	internal void MoveInto(Body body, Item item, ulong containerInstanceId)
	{
		var containerItem = CarriedItemLocator.FindById(body, containerInstanceId);
		var container = containerItem != null ? containerItem.GetComponent<Container>() : null; // Unity objects — ==
		if (container == null) // Unity object — ==
		{
			log.LogWarning("[RemoteIntent] move refused: container {Container} is not resolvable on the owner's body.", containerInstanceId);
			return;
		}

		container.UnloadItem(item, null);
		container.LoadItem(item);
		if (item.transform.parent != container.transform) // Unity objects — ==
		{
			log.LogWarning("[RemoteIntent] move refused by the native container guard: item {Item} did not enter container {Container} (weight, tag restriction or distance).",
				item.id, containerInstanceId);
			return;
		}

		log.LogInformation("[RemoteIntent] item {Item} entered container {Container} through the native guard.", item.id, containerInstanceId);
	}

	/// <summary>
	/// R5's per-child loop on the owner's real objects (<c>PlayerCamera.cs:1585</c>):
	/// every direct child of the dragged item's OWN container that the target's native
	/// <c>CanHoldItem</c> admits is unloaded from that container and loaded into the
	/// target, one pair at a time — the same guard, the same child order and the same
	/// partial outcome the local gesture would have had. A child the guard refuses
	/// stays where it is and the rest still move, exactly as natively.
	/// </summary>
	internal void MoveChildren(Body body, Item item, ulong containerInstanceId)
	{
		var source = item.GetComponent<Container>();
		var targetItem = CarriedItemLocator.FindById(body, containerInstanceId);
		var target = targetItem != null ? targetItem.GetComponent<Container>() : null; // Unity objects — ==
		if (source == null || target == null) // Unity objects — ==
		{
			log.LogWarning("[RemoteIntent] container expansion refused: item {Item} is not a container on the owner's body, or container {Container} is not resolvable there.",
				item.id, containerInstanceId);
			return;
		}

		// The children are snapshotted before the first unload: unloading reparents
		// them, and enumerating a transform while it changes skips entries.
		var children = DirectChildren(source);
		var moved = 0;
		var refused = 0;
		foreach (var child in children)
		{
			if (!target.CanHoldItem(child))
			{
				refused++;
				continue;
			}

			source.UnloadItem(child, null);
			target.LoadItem(child);

			// LoadItem returns void and refuses silently (a nested container that still
			// holds something, a container inside a container, or its own final weight,
			// tag and distance test), so the count is taken from what the scene shows —
			// a hook reports only verified writes. A child whose load was refused is left
			// unloaded by the pair that already ran, exactly as the native loop leaves it.
			if (child.transform.parent == target.transform) // Unity objects — ==
			{
				moved++;
			}
			else
			{
				refused++;
			}
		}

		log.LogInformation("[RemoteIntent] container expansion of item {Item} into container {Container}: {Moved} of {Total} direct child item(s) entered the container; {Refused} did not (the native guard, or a native load refusal).",
			item.id, containerInstanceId, moved, children.Count, refused);
	}

	/// <summary>
	/// <c>WaterContainerItem.Drain</c> (<c>PlayerCamera.cs:1732</c>): the owner removes
	/// this frame's amount from its own stack. The requester sent the amount, not the
	/// per-stack list it computed from the proxy, so the distribution is derived here
	/// — from the stack the item really has.
	/// </summary>
	internal void Drain(Item item, float amount)
	{
		var water = item.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			log.LogWarning("[RemoteIntent] drain refused: item {Item} carries no liquid container on the owner's body.", item.id);
			return;
		}

		water.Drain(water.CalculateDrain(amount));
	}

	private static List<Item> DirectChildren(Container container)
	{
		var children = new List<Item>();
		var parent = container.transform;
		for (var index = 0; index < parent.childCount; index++)
		{
			var child = parent.GetChild(index).GetComponent<Item>();
			if (child != null) // Unity object — ==
			{
				children.Add(child);
			}
		}

		return children;
	}
}
