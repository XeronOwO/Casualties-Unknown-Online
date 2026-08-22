using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// Read-only game-state half of <see cref="ModService"/> (Phase 4 Mod API
/// remainder). The surface projects framework-held character-stream facts into
/// immutable mod-facing DTOs; it never exposes Unity/game-assembly objects.
/// Every read is gated by <see cref="ModPermission.ReadGameState"/> — nothing
/// is implicit. Only the player character slice is exposed in this round; the
/// same projection pattern can be extended later without a wire change.
/// </summary>
public sealed partial class ModService
{
	/// <summary>
	/// The per-mod read-only game-state adapter. It reads from the same
	/// session-scoped remote-vitals/remote-inventory projections the Online UI
	/// uses, so a mod sees the same facts as the built-in UI without a second
	/// source of truth. Cached data is cleared automatically when a remote
	/// leaves the world or the session ends.
	/// </summary>
	private sealed class ModGameStateAdapter(
		ModService owner,
		ModManifest manifest,
		SessionService session,
		RemoteVitalsService vitals,
		RemoteInventoryService inventories) : IModGameState
	{
		public bool CanRead => HasPermission(manifest, ModPermission.ReadGameState);

		public bool TryGetPlayer(ulong steamId, out IModPlayerState player)
		{
			if (!CanRead)
			{
				owner.LogMissingPermission(manifest.Id, "ReadGameState");
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
