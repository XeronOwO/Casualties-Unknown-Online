using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod read-only game-state adapter. It reads from the same
/// session-scoped remote-vitals/remote-inventory projections the Online UI
/// uses, so a mod sees the same facts as the built-in UI without a second
/// source of truth — and, for an item read by id, from the kernel fact itself,
/// which is the one place an applied item exists as a row.
/// </summary>
internal sealed class ModGameStateAdapter(
	ModManifest manifest,
	SessionService session,
	RemoteVitalsService vitals,
	RemoteInventoryService inventories,
	IKernelItemFacts itemFacts,
	ILogger log) : IModGameState
{
	public bool CanRead => ModPermissionGate.HasPermission(manifest, ModPermission.ReadGameState);

	public bool TryGetPlayer(ulong steamId, out IModPlayerState player)
	{
		if (!ModPermissionGate.Try(log, manifest, ModPermission.ReadGameState))
		{
			player = null!;
			return false;
		}

		vitals.TryGet(steamId, out var vitalsSnapshot);
		inventories.TryGet(steamId, out var inventorySnapshot);
		if (vitalsSnapshot is null && inventorySnapshot is null)
		{
			player = null!;
			return false;
		}

		var inWorld = steamId == session.LocalSteamId
			? session.LocalInWorld
			: session.IsRemoteInWorld(steamId);

		player = new ModPlayerState(
			steamId,
			inWorld,
			vitalsSnapshot is null ? null : new ModPlayerVitals(vitalsSnapshot),
			inventorySnapshot is null ? null : new ModPlayerInventory(inventorySnapshot));
		return true;
	}

	/// <summary>
	/// The by-id read: the kernel row projected into the mod contract. The kernel
	/// is the source because it is the ONE place every item exists as a fact —
	/// including an applied item, which no character snapshot lists because the
	/// player does not carry it — and every client holds it: the host's kernel
	/// owns the rows, and a guest's mirror receives each committed batch and the
	/// join checkpoint. A null row is "this framework holds no such id", which
	/// includes an id whose creation was never observed here.
	/// </summary>
	public bool TryGetItem(ulong instanceId, out IModItemState item)
	{
		if (!ModPermissionGate.Try(log, manifest, ModPermission.ReadGameState))
		{
			item = null!;
			return false;
		}

		var row = itemFacts.FindItem(instanceId);
		if (row is null)
		{
			item = null!;
			return false;
		}

		var state = row.Value;
		item = new ModItemState(
			state.Identity.InstanceId,
			state.Identity.DefinitionId,
			Project(state.Location.Kind),
			state.Location.Owner.Value,
			state.Location.LimbIndex,
			state.Location.ParentItemId,
			state.Location.X,
			state.Location.Y,
			state.Data.Condition,
			state.Data.Favourited);
		return true;
	}

	/// <summary>
	/// The kernel's location family mapped member by member, never cast: the two
	/// enums are separate contracts, so a family added to the kernel has to be
	/// named here (and is a framework defect until it is) instead of silently
	/// reading as whichever member happens to share its ordinal.
	/// </summary>
	private static ModItemLocation Project(ItemLocationKind kind) => kind switch
	{
		ItemLocationKind.World => ModItemLocation.World,
		ItemLocationKind.Carried => ModItemLocation.Carried,
		ItemLocationKind.Contained => ModItemLocation.Contained,
		ItemLocationKind.Applied => ModItemLocation.Applied,
		ItemLocationKind.Terminal => ModItemLocation.Terminal,
		_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "the kernel's item location family has no mod-contract counterpart"),
	};

	private sealed record ModPlayerState(
		ulong SteamId,
		bool InWorld,
		IModPlayerVitals? Vitals,
		IModPlayerInventory? Inventory) : IModPlayerState;

	private sealed record ModPlayerVitals(
		float BrainHealth,
		float Hunger,
		float Thirst,
		float Stamina,
		float Energy,
		float Temperature,
		bool Alive,
		bool Conscious) : IModPlayerVitals
	{
		internal ModPlayerVitals(RemoteVitalsSnapshot source)
			: this(
				source.BrainHealth,
				source.Hunger,
				source.Thirst,
				source.Stamina,
				source.Energy,
				source.Temperature,
				source.Alive,
				source.Conscious)
		{
		}
	}

	private sealed record ModPlayerInventory(
		IReadOnlyList<IModInventoryEntry> Items,
		int HandSlot) : IModPlayerInventory
	{
		public int Count => Items.Count;

		internal ModPlayerInventory(RemoteInventorySnapshot source)
			: this([.. source.Items.Select(Project)], source.HandSlot)
		{
		}

		private static IModInventoryEntry Project(RemoteInventoryEntry entry) =>
			new ModInventoryEntry(
				entry.InstanceId,
				entry.ItemId,
				entry.SlotIndex,
				entry.Condition,
				entry.Favourited,
				[.. entry.Contents.Select(Project)]);
	}

	private sealed record ModInventoryEntry(
		ulong InstanceId,
		string ItemId,
		int SlotIndex,
		float Condition,
		bool Favourited,
		IReadOnlyList<IModInventoryEntry> Contents) : IModInventoryEntry;

	private sealed record ModItemState(
		ulong InstanceId,
		string ItemId,
		ModItemLocation Location,
		ulong OwnerSteamId,
		int LimbIndex,
		ulong ParentItemId,
		float X,
		float Y,
		float Condition,
		bool Favourited) : IModItemState;
}
