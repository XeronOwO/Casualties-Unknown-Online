using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Guest-side world-item follow: the host's physics is the single simulation,
/// the local copies are kinematic RENDERS of it — never simulating on their
/// own (local gravity/collisions would fight the host's stream forever:
/// "dropped — immediately desynced"). The 10 Hz stream updates the targets,
/// the pump eases toward them. No state on the host side — the host pump is
/// <see cref="ItemPositionAuthority"/>.
/// </summary>
internal sealed class ItemPositionFollow(ItemService items, DropProtectionGuard guard)
{
	private readonly ItemService _items = items;
	private readonly DropProtectionGuard _guard = guard;

	/// <summary>id → the host's authoritative move target (position/velocity/rotation) — interpolated toward every frame.</summary>
	private readonly Dictionary<ulong, (Vector2 Pos, Vector2 Vel, float Rot, float AngVel)> _followTargets = [];

	internal void BindToSession() => _items.ItemMoveReceived += OnRemoteItemMove;

	internal void Unbind() => _items.ItemMoveReceived -= OnRemoteItemMove;

	internal void Update()
	{
		if (_followTargets.Count == 0)
		{
			return;
		}

		foreach (var key in _followTargets.Keys.ToList()) // copy — removed while iterating
		{
			var item = ItemApplication.FindWorldItem(key);
			// Unity object — ==. Gone (picked up/destroyed), not yet materialized,
			// or no longer a WORLD item (picked into an inventory/hand — the item
			// object persists in Item.allItems, so FindWorldItem still finds it;
			// without this check the stale target keeps yanking the carried item
			// toward the host's last world position — "everything desynced"
			// after picking things up).
			if (item == null || !ItemWorldSync.IsStandaloneWorldItem(item))
			{
				_followTargets.Remove(key);
				_guard.Remove(key);
				continue;
			}

			var (pos, vel, rot, angVel) = _followTargets[key];
			// Kinematic bodies take no physics input (no push/pull/gap
			// accumulation), yet their colliders still register in pickup
			// queries, so the player can still grab them.
			if (item.rb.bodyType != RigidbodyType2D.Kinematic)
			{
				item.rb.bodyType = RigidbodyType2D.Kinematic;
			}

			item.transform.position = Vector3.Lerp(item.transform.position, new Vector3(pos.x, pos.y, 0f), Mathf.Clamp01(Time.deltaTime * 12f));
			item.transform.eulerAngles = new Vector3(0f, 0f, rot);
			item.rb.velocity = vel;
			item.rb.angularVelocity = angVel;
		}
	}

	/// <summary>
	/// The host's physics moved items — store the authoritative targets; Update
	/// interpolates toward them every frame. Direct placement per stream tick
	/// (10 Hz) made items that occupy the same spot (a dropped bag and its
	/// contents) visibly snap and jitter — "twitching in place"; pursuit keeps
	/// the follow smooth.
	/// </summary>
	private void OnRemoteItemMove(IReadOnlyList<ItemMoveEntryMsg> items)
	{
		foreach (var e in items)
		{
			_followTargets[e.ItemId] = (new Vector2(e.X, e.Y), new Vector2(e.VelX, e.VelY), e.Rotation, e.AngularVelocity);
		}
	}
}
