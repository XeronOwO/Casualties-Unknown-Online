using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The crafting domain's operation side: ONE crafting operation = ONE report.
/// The game splits a craft across several method calls (RecipeItem.UseItem
/// drains/destroys the materials, RecipeResult.SpawnResult creates the
/// products, the first-craft bonus mutates the body) and every sub-hook would
/// report separately — a consumed material reported as "dropped" is the ghost
/// family. A Craft scope silences the item hooks; the coordinator snapshots
/// the pre-state at the prefix, reads the terminal state at the postfix and
/// commits one CraftReportMsg carrying everything. The end-of-frame destroys
/// (Unity's Object.Destroy is deferred, so OnDestroy fires AFTER the scope
/// closes) are claimed into a set — ShouldSuppressDestroy consumes the claim,
/// and the same fact never reports twice. The first-craft bonus and the
/// fail-branch injuries deliberately ride the 1 Hz CharacterData snapshot.
/// </summary>
internal sealed class CraftingSync(
	ICraftControl craft, ItemIdAllocator ids, ItemReportCommitter reports,
	OperationTrace trace, ILogger<CraftingSync> log)
{
	private readonly ICraftControl _craft = craft;
	private readonly ItemIdAllocator _ids = ids;
	private readonly ItemReportCommitter _reports = reports;
	private readonly OperationTrace _trace = trace;
	private readonly ILogger<CraftingSync> _log = log;

	/// <summary>Ids the committed craft reports claimed as destroyed — the end-of-frame destroy hooks consume the claims (ids are never recycled, so a stale entry can only suppress the same item's correct claim).</summary>
	private readonly HashSet<ulong> _claimedDestroys = [];

	/// <summary>The world was left — the claims die with the scene.</summary>
	internal void ResetPending() => _claimedDestroys.Clear();

	/// <summary>True consumes the claim: this destroy's fact already rode a craft report.</summary>
	internal bool ShouldSuppressDestroy(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		return idComp != null && idComp.Id != 0 && _claimedDestroys.Remove(idComp.Id); // Unity object — ==
	}

	// ===== Recipe.TryMake =====

	internal object? OnCraftBegin(Recipe recipe)
	{
		_claimedDestroys.Clear();
		var scope = CallContext.Enter(CallContext.Origin.Craft);
		// Read-only, Random-free (Recipe.cs:107-135) — safe in a prefix. null =
		// no matching materials: the game plays the Deny sound and consumes
		// nothing — no scope, no report.
		var materials = recipe.GetItemsForRecipe();
		if (materials == null)
		{
			scope.Dispose();
			return null;
		}

		// A destroyable material that still carries contents (battery, container
		// children, liquid stack) would drop/lose those contents on the craft
		// path. Refuse the whole operation before the native TryMake consumes
		// anything — the player empties the item first (CraftingContentsGuard).
		for (var i = 0; i < materials.Count; i++)
		{
			if (CraftingContentsGuard.ShouldRefuse(recipe.items[i], materials[i]))
			{
				scope.Dispose();
				_log.LogWarning("[Crafting] craft refused: material {Material} (index {Index}) still has contents — empty it first.",
					materials[i].id, i);
				return CraftRefusal.Instance;
			}
		}

		var body = PlayerCamera.main.body;
		var op = _trace.NextOperationId();
		_trace.Begin(op, 0, "CraftReport", "Craft");
		// Bind every material to an id (a generation-time floor material may be
		// id-less — the host's world-table lookup needs the id) and snapshot the
		// pre-state: the liquid fingerprints (the liquid RESULT merges into an
		// existing container with no new item — RecipeResult.cs:36-49) and the
		// inventory object set (the product diff — AutoPickUpItem lands via
		// PickUpItem, Container.LoadItem or WearWearable, Body.cs:494-529).
		var inventoryBefore = body.GetAllItemsThorough();
		var liquidFingerprints = new List<(Item Item, List<(string Id, float Amount)> Stack)>();
		foreach (var carried in inventoryBefore)
		{
			var water = carried.GetComponent<WaterContainerItem>();
			if (water != null) // Unity object — ==
			{
				liquidFingerprints.Add((carried, [.. water.stack.Select(s => (s.liquidId, s.amount))]));
			}
		}

		foreach (var material in materials)
		{
			_ids.EnsureId(material);
		}

		return new CraftState(scope, recipe, materials, liquidFingerprints, [.. inventoryBefore], op);
	}

	internal void OnCraftEnd(object? stateObj)
	{
		var state = stateObj as CraftState;
		if (state == null)
		{
			return;
		}

		try
		{
			// Materials: the consumption disposition comes from the RECIPE data
			// (destroyItem && !isLiquid — RecipeItem.UseItem's destroy branch,
			// RecipeItem.cs:175-184), never from scene inference at the postfix
			// (the destroy is deferred, the object still reads alive — the
			// hook-inference lesson).
			var entries = new List<CraftEntryMsg>(state.Materials.Count);
			for (var i = 0; i < state.Materials.Count; i++)
			{
				var material = state.Materials[i];
				var recipeItem = state.Recipe.items[i]; // the same order — GetItemsForRecipe iterates recipe.items
				var id = OperationTrace.IdOf(material);
				if (recipeItem.destroyItem && !recipeItem.isLiquid)
				{
					_claimedDestroys.Add(id);
					entries.Add(new CraftEntryMsg
					{
						Disposition = CraftEntryDisposition.Destroyed,
						Item = new CharacterItemMsg { InstanceId = id, ItemId = material.id },
					});
				}
				else
				{
					entries.Add(new CraftEntryMsg
					{
						Disposition = CraftEntryDisposition.Changed,
						Item = ItemStateCodec.CaptureDigest(material),
					});
				}
			}

			// Liquid-result containers: a container NOT among the materials whose
			// stacks changed absorbed a liquid result (SpawnResult's merge branch).
			foreach (var (containerItem, before) in state.LiquidContainers)
			{
				if (state.Materials.Any(m => m == containerItem)) // Unity object — ==
				{
					continue; // its material entry carries the drain
				}

				var water = containerItem.GetComponent<WaterContainerItem>();
				if (water != null && LiquidsChanged(water, before)) // Unity object — ==
				{
					if (_ids.EnsureId(containerItem) == 0)
					{
						continue; // unbound — the host cannot match the entry
					}

					entries.Add(new CraftEntryMsg
					{
						Disposition = CraftEntryDisposition.Changed,
						Item = ItemStateCodec.CaptureDigest(containerItem),
					});
				}
			}

			// Products: the inventory object-set diff — new objects are the
			// crafted items (any landing variant). A noautopickup product stays
			// on the floor and rides the item domain's natural spawn path
			// (Item.Start fires outside the scope, OnItemInstantiated reports
			// the world item) — the diff never sees it, by design.
			var products = new List<CharacterItemMsg>();
			foreach (var carried in PlayerCamera.main.body.GetAllItemsThorough())
			{
				if (state.InventoryBefore.Contains(carried))
				{
					continue;
				}

				var id = _ids.EnsureId(carried);
				if (id == 0)
				{
					continue; // unbound — nothing to register
				}

				products.Add(ItemStateCodec.CaptureItem(carried, ItemStateCodec.SlotOf(carried)));
			}

			CommitReport(state.Op, CraftOperationKind.Craft, entries, products, "Craft");
		}
		catch (Exception ex)
		{
			// The suppressed sub-reports are lost — the 1 Hz character snapshot
			// and the next ordinary action self-heal. Log and abandon (H11).
			_trace.End(state.Op, 0, "CraftReport", "Failed", ex.GetType().Name);
			_log.LogWarning("[Crafting] craft capture failed ({Error}) — report abandoned, snapshot self-heals.", ex.GetType().Name);
		}
		finally
		{
			state.Scope.Dispose();
		}
	}

	// ===== Body.CombineItems =====

	internal object? OnCombineBegin(Body body, Item it1, Item it2)
	{
		_claimedDestroys.Clear();
		var scope = CallContext.Enter(CallContext.Origin.Craft);
		var branch = DetermineBranch(it1, it2);
		var gun = branch == CombineBranch.GunLoad ? it1.GetComponent<GunScript>() : null;
		var ammo = branch == CombineBranch.AmmoLoad ? it1.GetComponent<AmmoScript>() : null;
		var op = _trace.NextOperationId();
		_trace.Begin(op, 0, "CraftReport", "Combine");
		return new CombineState(
			scope, it1, it2, branch,
			gun != null ? Convert.ToInt32(gun.roundInChamber) : 0, // Unity object — ==
			gun != null && gun.hasMag, gun != null ? gun.roundsInMag : 0,
			ammo != null ? ammo.rounds : 0,
			it1.condition, it2.condition, OperationTrace.IdOf(it1), OperationTrace.IdOf(it2), op);
	}

	internal void OnCombineEnd(object? stateObj)
	{
		var state = stateObj as CombineState;
		if (state == null)
		{
			return;
		}

		try
		{
			if (state.Branch == CombineBranch.WaterTransfer)
			{
				// The transfer UI opened — nothing committed here; the Finish
				// reports the terminal state (cancel = no report anywhere).
				_trace.End(state.Op, 0, "CraftReport", "Skipped", "WaterTransferUi");
				return;
			}

			// Terminal-change verification (H8): LoadMag/LoadRound silently
			// refuse mismatched ammo, and a full-condition merge is a no-op —
			// a refused combine must commit nothing.
			var changed = state.Branch switch
			{
				CombineBranch.GunLoad => GunStateChanged(state),
				CombineBranch.AmmoLoad => state.Target.GetComponent<AmmoScript>()!.rounds != state.AmmoRounds, // Unity object — == (the branch check guarantees the component)
				_ => state.Target.condition != state.TargetCondition || state.Dragged.condition != state.DraggedCondition,
			};
			if (!changed)
			{
				_trace.End(state.Op, 0, "CraftReport", "Rejected", "NoChange");
				return;
			}

			var entries = new List<CraftEntryMsg>(2)
			{
				new()
				{
					Disposition = CraftEntryDisposition.Changed,
					Item = ItemStateCodec.CaptureDigest(state.Target),
				},
			};
			if (state.Branch is CombineBranch.GunLoad or CombineBranch.AmmoLoad)
			{
				// The dragged ammo/mag is destroyed (deferred — claim it).
				_claimedDestroys.Add(state.DraggedId);
				entries.Add(new CraftEntryMsg
				{
					Disposition = CraftEntryDisposition.Destroyed,
					Item = new CharacterItemMsg { InstanceId = state.DraggedId, ItemId = state.Dragged.id },
				});
			}
			else
			{
				entries.Add(new CraftEntryMsg
				{
					Disposition = CraftEntryDisposition.Changed,
					Item = ItemStateCodec.CaptureDigest(state.Dragged),
				});
			}

			CommitReport(state.Op, CraftOperationKind.Combine, entries, [], "Combine");
		}
		catch (Exception ex)
		{
			_trace.End(state.Op, 0, "CraftReport", "Failed", ex.GetType().Name);
			_log.LogWarning("[Crafting] combine capture failed ({Error}) — report abandoned, snapshot self-heals.", ex.GetType().Name);
		}
		finally
		{
			state.Scope.Dispose();
		}
	}

	private bool GunStateChanged(CombineState state)
	{
		var gun = state.Target.GetComponent<GunScript>();
		if (gun == null) // Unity object — ==
		{
			return false;
		}

		return gun.hasMag != state.GunHasMag
			|| gun.roundsInMag != state.GunRoundsInMag
			|| Convert.ToInt32(gun.roundInChamber) != state.GunRoundInChamber;
	}

	/// <summary>The branch decision mirrors CombineItems' own order (Body.cs:1258-1282), including the water gate (SpaceLeft &gt; 0, not the craftingbottle — Body.cs:1269-1275): a water-container pair with a full target falls through to NOTHING (no merge runs).</summary>
	private static CombineBranch DetermineBranch(Item it1, Item it2)
	{
		if (it1.GetComponent<GunScript>() != null && it2.GetComponent<AmmoScript>() != null) // Unity objects — ==
		{
			return CombineBranch.GunLoad;
		}

		if (it1.GetComponent<AmmoScript>() != null && it2.GetComponent<AmmoScript>() != null) // Unity objects — ==
		{
			return CombineBranch.AmmoLoad;
		}

		var w1 = it1.GetComponent<WaterContainerItem>();
		var w2 = it2.GetComponent<WaterContainerItem>();
		if (w1 != null && w2 != null && w1.SpaceLeft > 0f && it1.id != "craftingbottle") // Unity objects — ==
		{
			return CombineBranch.WaterTransfer;
		}

		return CombineBranch.ConditionMerge;
	}

	// ===== LiquidTransfer.Finish =====

	/// <summary>The transfer UI confirmed — ONE report with both containers' post-state (no scope: an overweight target's UnloadItem is a genuine world fact, its report fires naturally).</summary>
	internal void OnLiquidTransferFinished(WaterContainerItem transferTo, WaterContainerItem transferFrom)
	{
		var toItem = transferTo.GetComponent<Item>();
		var fromItem = transferFrom.GetComponent<Item>();
		var op = _trace.NextOperationId();
		_trace.Begin(op, OperationTrace.IdOf(toItem), "CraftReport", "LiquidTransfer");
		var entries = new List<CraftEntryMsg>
		{
			new() { Disposition = CraftEntryDisposition.Changed, Item = ItemStateCodec.CaptureDigest(toItem) },
			new() { Disposition = CraftEntryDisposition.Changed, Item = ItemStateCodec.CaptureDigest(fromItem) },
		};
		_reports.CommitReport(0, op, "CraftReport", ItemReportCommitter.CommitStatus.Committed,
			() =>
			{
				_craft.ReportCraft(new CraftReportMsg { Kind = CraftOperationKind.LiquidTransfer, Entries = entries });
				return 1;
			},
			"LiquidTransfer");
	}

	// ===== Blueprint use (the unlock fact — the destruction rides the existing use digest) =====

	internal void OnItemUsed(Item item)
	{
		var blueprint = item.GetComponent<BlueprintScript>();
		if (blueprint != null) // Unity object — ==
		{
			_craft.SendRecipeUnlock(blueprint.recipeIndex);
		}
	}

	private void CommitReport(long op, CraftOperationKind kind, List<CraftEntryMsg> entries, List<CharacterItemMsg> products, params string[] events)
	{
		_reports.CommitReport(0, op, "CraftReport", ItemReportCommitter.CommitStatus.Committed,
			() =>
			{
				_craft.ReportCraft(new CraftReportMsg { Kind = kind, Entries = entries, Products = products });
				_log.LogInformation("[Crafting] {Kind} committed: {Entries} entries, {Products} products.", kind, entries.Count, products.Count);
				return 1;
			},
			events);
	}

	private static bool LiquidsChanged(WaterContainerItem water, List<(string Id, float Amount)> before)
	{
		var after = water.stack;
		if (after.Count != before.Count)
		{
			return true;
		}

		foreach (var (id, amount) in before)
		{
			var now = after.FirstOrDefault(s => s.liquidId == id);
			if (now == null || Math.Abs(now.amount - amount) >= 0.01f)
			{
				return true;
			}
		}

		return false;
	}

	private enum CombineBranch
	{
		GunLoad,
		AmmoLoad,
		WaterTransfer,
		ConditionMerge,
	}

	private sealed class CraftState(
		IDisposable scope, Recipe recipe, List<Item> materials,
		List<(Item Item, List<(string Id, float Amount)> Stack)> liquidContainers,
		HashSet<Item> inventoryBefore, long op)
	{
		internal IDisposable Scope { get; } = scope;

		internal Recipe Recipe { get; } = recipe;

		internal List<Item> Materials { get; } = materials;

		internal List<(Item Item, List<(string Id, float Amount)> Stack)> LiquidContainers { get; } = liquidContainers;

		internal HashSet<Item> InventoryBefore { get; } = inventoryBefore;

		internal long Op { get; } = op;
	}

	/// <summary>Marker returned by <see cref="OnCraftBegin"/> when the content guard refused the craft — the Harmony prefix skips the native operation for this value.</summary>
	internal sealed class CraftRefusal
	{
		internal static readonly CraftRefusal Instance = new();

		private CraftRefusal()
		{
		}
	}

	private sealed class CombineState(
		IDisposable scope, Item target, Item dragged, CombineBranch branch,
		int gunRoundInChamber, bool gunHasMag, int gunRoundsInMag, int ammoRounds,
		float targetCondition, float draggedCondition, ulong targetId, ulong draggedId, long op)
	{
		internal IDisposable Scope { get; } = scope;

		internal Item Target { get; } = target;

		internal Item Dragged { get; } = dragged;

		internal CombineBranch Branch { get; } = branch;

		internal int GunRoundInChamber { get; } = gunRoundInChamber;

		internal bool GunHasMag { get; } = gunHasMag;

		internal int GunRoundsInMag { get; } = gunRoundsInMag;

		internal int AmmoRounds { get; } = ammoRounds;

		internal float TargetCondition { get; } = targetCondition;

		internal float DraggedCondition { get; } = draggedCondition;

		internal ulong TargetId { get; } = targetId;

		internal ulong DraggedId { get; } = draggedId;

		internal long Op { get; } = op;
	}
}
