using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Guest-side world-item follow with LOCAL PHYSICS: the host's physics is the
/// authority (10 Hz stream of position/velocity/rotation/angular velocity), the
/// guest copies simulate locally (dynamic bodies, ground-layer collisions only)
/// and the stream soft-corrects them (velocity sync every tick, hard snap past
/// a distance threshold). Items start FROZEN (kinematic — the drop-freeze in
/// ItemWorldSync / the materialize in ItemApplication) and switch to local
/// physics on their first stream tick: same-phase start, then continuous
/// simulation (the "smooth but unreal" kinematic render is gone — rotation
/// comes from the local angular velocity, never per-tick hard sets). The
/// non-authoritative interactions (player push, item-item push, trap triggers)
/// are isolated away by the layer matrix: Item (7) collides ONLY with Ground
/// (6); pickup queries (OverlapPoint, PlayerCamera.cs:1423) ignore the matrix,
/// so pickup still works. Host side: no isolation, original game physics.
/// The DECISIONS (settled / ease / snap — <see cref="ItemFollowDecision"/>) are
/// pure; this class is the scene-write shell.
/// </summary>
internal sealed class ItemPositionFollow(ItemService items, DropProtectionGuard guard, SessionService session, ILogger<ItemPositionFollow> log)
{
	/// <summary>Item layer — pickup queries (PlayerCamera.cs:1423/1702/1997) are
	/// queries and ignore the collision matrix, so isolating Item×X never breaks pickup.</summary>
	private const int ItemLayer = 7;

	/// <summary>Ground layer — the tilemap collider (WorldGeneration.cs:3827-3839).</summary>
	private const int GroundLayer = 6;

	private readonly ItemService _items = items;
	private readonly DropProtectionGuard _guard = guard;
	private readonly SessionService _session = session;
	private readonly ILogger<ItemPositionFollow> _log = log;

	private readonly ItemFollowDecision _follow = new();

	/// <summary>The layer isolation is applied while guest + session active — state-driven edge (idempotent).</summary>
	private bool _isolationApplied;

	internal void BindToSession() => _items.ItemMoveReceived += OnRemoteItemMove;

	internal void Unbind()
	{
		_items.ItemMoveReceived -= OnRemoteItemMove;
		_follow.Clear();
		RestoreLayerIsolation();
	}

	internal void Update()
	{
		UpdateLayerIsolation();
		if (_follow.Count == 0)
		{
			return;
		}

		foreach (var key in _follow.Keys.ToList()) // copy — removed while iterating
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
				_follow.Remove(key);
				_guard.Remove(key);
				continue;
			}

			var d = _follow.Decide(key, item.transform.position.x, item.transform.position.y, item.transform.eulerAngles.z, Time.deltaTime);
			if (d.Mode == FollowMode.Frozen)
			{
				continue; // no target — not yet streamed
			}

			var rb = item.rb;
			if (d.Mode == FollowMode.Settled)
			{
				// The host's velocity is zero — the local physics stopped too;
				// kill any residual drift, then ease the residual gap away (the
				// 1 Hz settled stream keeps feeding the target until it closes).
				rb.velocity = Vector2.zero;
				rb.angularVelocity = 0f;
				if (d.EaseToTarget)
				{
					var k = d.EaseK; // ease — no visible jump on a "stationary" item
					item.transform.position = Vector3.Lerp(item.transform.position, new Vector3(d.TargetX, d.TargetY, 0f), k);
					var rot = Mathf.LerpAngle(item.transform.eulerAngles.z, d.TargetRot, k);
					item.transform.eulerAngles = new Vector3(0f, 0f, rot);
					if (d.LogDivergence)
					{
						_log.LogInformation("[ItemPhysics] settle {Id} d={Dist:F2}.", key, d.Dist); // > 0.5 = a real divergence, worth tuning on
					}
				}
			}
			else
			{
				// Soft correction while moving: the host's velocity is
				// authoritative — the local physics continues from it every tick.
				// Position within the snap threshold is left to the local
				// simulation (continuous rotation included); past it the copy
				// hard-snaps to the host's state.
				rb.velocity = new Vector2(d.VelX, d.VelY);
				rb.angularVelocity = d.AngVel;
				if (d.HardSnap)
				{
					item.transform.position = new Vector3(d.TargetX, d.TargetY, 0f);
					item.transform.eulerAngles = new Vector3(0f, 0f, d.TargetRot);
					_log.LogInformation("[ItemPhysics] snap {Id} d={Dist:F1}.", key, d.Dist);
				}
			}
		}
	}

	/// <summary>The host's physics moved items — store the authoritative targets;
	/// the first tick for a still-frozen copy switches it to local physics and
	/// aligns it to the stream (same-phase start, no drop-off).</summary>
	private void OnRemoteItemMove(IReadOnlyList<ItemMoveEntryMsg> items)
	{
		foreach (var e in items)
		{
			if (_follow.UpdateTarget(e.ItemId, e.X, e.Y, e.VelX, e.VelY, e.Rotation, e.AngularVelocity))
			{
				StartLocalPhysics(e.ItemId, e);
			}
		}
	}

	/// <summary>A copy entering the stream for the first time: it may still be
	/// frozen (kinematic — dropped or materialized) — switch it to local physics
	/// and align it to the host's state once (start-point parity, the frozen
	/// wait's payoff). An already-simulating copy (target re-registered) just
	/// aligns once.</summary>
	private void StartLocalPhysics(ulong itemId, ItemMoveEntryMsg e)
	{
		var item = ItemApplication.FindWorldItem(itemId);
		if (item == null) // Unity object — == (not materialized yet — the next tick handles it)
		{
			return;
		}

		var rb = item.rb;
		var wasFrozen = rb.bodyType == RigidbodyType2D.Kinematic;
		if (wasFrozen)
		{
			rb.bodyType = RigidbodyType2D.Dynamic; // local physics takes over
			rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // aligns the game's throw (Body.cs:1664)
																			 // LiquidAffect.Start is gated off on kinematic bodies (LiquidAffectPatches —
																			 // the game destroys the component when bodyType != Dynamic and Item.Update
																			 // then NREs on the dead reference), so the component's private rb field was
																			 // never initialized; with the body live again the field must exist or
																			 // LiquidAffect.FixedUpdate NREs (LiquidAffect.cs:31). One-time backfill.
			var affect = item.GetComponent<LiquidAffect>();
			if (affect != null) // Unity object — ==
			{
				Traverse.Create(affect).Field("rb").SetValue(rb);
			}
		}

		item.transform.position = new Vector3(e.X, e.Y, 0f); // start-point parity
		item.transform.eulerAngles = new Vector3(0f, 0f, e.Rotation);
		rb.velocity = new Vector2(e.VelX, e.VelY);
		rb.angularVelocity = e.AngularVelocity;
		_log.LogInformation("[ItemPhysics] play {Id} (from {State}).", itemId, wasFrozen ? "freeze" : "simulation");
	}

	/// <summary>Isolate the guest's items to the ground layer: Item (7) collides
	/// ONLY with Ground (6). Everything else (player, limbs, items, traps) is
	/// non-authoritative interaction on the guest side — the host stream
	/// soft-corrects it. Host side keeps the full matrix (item-triggered traps
	/// are original game behaviour there). State-driven edge: applied while
	/// guest + session active, restored otherwise — idempotent.</summary>
	private void UpdateLayerIsolation()
	{
		var shouldIsolate = _session.Role == SessionRole.Guest && _session.SessionActive;
		if (shouldIsolate == _isolationApplied)
		{
			return;
		}

		_isolationApplied = shouldIsolate;
		for (var i = 0; i < 32; i++)
		{
			if (i != GroundLayer)
			{
				Physics2D.IgnoreLayerCollision(ItemLayer, i, shouldIsolate);
			}
		}
	}

	private void RestoreLayerIsolation()
	{
		if (!_isolationApplied)
		{
			return;
		}

		_isolationApplied = false;
		for (var i = 0; i < 32; i++)
		{
			if (i != GroundLayer)
			{
				Physics2D.IgnoreLayerCollision(ItemLayer, i, false);
			}
		}
	}
}
