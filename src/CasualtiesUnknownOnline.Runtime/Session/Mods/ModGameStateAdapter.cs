using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod read-only game-state adapter. It reads from the same
/// session-scoped remote-vitals/remote-inventory projections the Online UI
/// uses, so a mod sees the same facts as the built-in UI without a second
/// source of truth.
/// </summary>
internal sealed class ModGameStateAdapter(
	ModManifest manifest,
	SessionService session,
	RemoteVitalsService vitals,
	RemoteInventoryService inventories,
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
}
