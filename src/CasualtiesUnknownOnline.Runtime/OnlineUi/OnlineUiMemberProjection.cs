using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Projects the read-only session/member/vitals/inventory surfaces into
/// <see cref="OnlineUiMemberRow"/> values for the Unity IMGUI overlay. Keeping
/// the action eligibility rules here (rather than inside OnGUI) makes the UI a
/// dumb renderer and gives the interaction-button conditions an L0 test face.
/// </summary>
public static class OnlineUiMemberProjection
{
	/// <summary>
	/// Build one row per lobby member, in lobby order. Members without a
	/// session presence yet are still shown as "not handshaken" rows.
	/// </summary>
	public static IReadOnlyList<OnlineUiMemberRow> Build(
		ulong localSteamId,
		ulong lobbyOwner,
		IReadOnlyList<ulong> lobbyMembers,
		IEnumerable<MemberPresenceTable.MemberPresence> members,
		Func<ulong, string> displayName,
		Func<ulong, RemoteVitalsSnapshot?> getVitals,
		Func<ulong, RemoteInventorySnapshot?> getInventory,
		IPlayerInteractionControl? playerInteraction,
		IHostBanService? hostBan,
		bool canAdmin,
		bool localInWorld,
		bool hasHealItem,
		IReadOnlyList<LocalHealItem> healItems,
		bool hasUseItem,
		IReadOnlyList<LocalUseItem> useItems)
	{
		var memberMap = members.ToDictionary(m => m.SteamId, m => m);
		var rows = new List<OnlineUiMemberRow>();

		foreach (var memberId in lobbyMembers)
		{
			memberMap.TryGetValue(memberId, out var member);
			var vitals = member is { InWorld: true } ? getVitals(memberId) : null;
			var inventory = member is { InWorld: true } ? getInventory(memberId) : null;
			var isLocal = memberId == localSteamId;

			var isCarryingThis = !isLocal
				&& playerInteraction?.TryGetCarried(localSteamId, out var carried) == true
				&& carried == memberId;
			var hasExistingCarry = playerInteraction?.TryGetCarried(localSteamId, out _) == true;
			var isLocalCarried = playerInteraction?.TryGetCarrier(localSteamId, out _) == true;
			var isAlreadyCarried = playerInteraction?.TryGetCarrier(memberId, out _) == true;
			var isCarryingSomeone = playerInteraction?.TryGetCarried(memberId, out _) == true;

			var canCarry = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& vitals is not null
				&& (!vitals.Conscious || !vitals.Alive)
				&& !isAlreadyCarried
				&& !isCarryingSomeone
				&& !hasExistingCarry;

			var canPiggyback = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& vitals is { Alive: true, Conscious: true }
				&& !isAlreadyCarried
				&& !isCarryingSomeone
				&& !hasExistingCarry;

			var canDrop = isCarryingThis;
			var canRequestDrop = isLocal
				&& localInWorld
				&& playerInteraction?.TryGetCarrier(localSteamId, out _) == true;
			var canHeal = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& vitals is { Alive: true }
				&& hasHealItem;
			var canUseItem = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& vitals is { Alive: true, Conscious: true }
				&& hasUseItem;
			var canPush = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& vitals is not null
				&& !isLocalCarried
				&& !isAlreadyCarried
				&& !isCarryingSomeone
				&& !hasExistingCarry;
			var canRecruit = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& vitals is { Alive: false };
			var takeable = canTake(inventory, vitals, isLocal, member);
			var canAdminMember = canAdmin && !isLocal && member is not null;

			rows.Add(new OnlineUiMemberRow
			{
				SteamId = memberId,
				Name = displayName(memberId),
				IsHost = memberId == lobbyOwner,
				IsLocal = isLocal,
				Handshaken = member?.Handshaken ?? false,
				InWorld = member?.InWorld ?? false,
				RttMs = member?.RttMs ?? -1f,
				VitalsText = vitals?.ToShortString(),
				InventoryText = inventory?.ToShortString(),
				IsCarryingThis = isCarryingThis,
				CanCarry = canCarry,
				CanPiggyback = canPiggyback,
				CanDrop = canDrop,
				CanRequestDrop = canRequestDrop,
				CanHeal = canHeal,
				CanUseItem = canUseItem,
				CanPush = canPush,
				CanRecruit = canRecruit,
				CanTake = takeable.Count > 0,
				CanKick = canAdminMember,
				CanBan = canAdminMember,
				IsBanned = hostBan?.IsBanned(memberId) ?? false,
				IsDead = vitals is not null && !vitals.Alive,
				IsUnconscious = vitals is not null && vitals.Alive && !vitals.Conscious,
				IsCarryingSomeone = isCarryingSomeone,
				IsCarried = isAlreadyCarried,
				Inventory = inventory?.Items,
				TakeableItems = takeable,
				HealItems = canHeal ? healItems : [],
				UseItems = canUseItem ? useItems : [],
			});
		}

		return rows;
	}

	private static List<RemoteInventoryEntry> canTake(
		RemoteInventorySnapshot? inventory,
		RemoteVitalsSnapshot? vitals,
		bool isLocal,
		MemberPresenceTable.MemberPresence? member)
	{
		if (isLocal
			|| member is not { InWorld: true }
			|| inventory is null
			|| vitals is null
			|| (vitals.Conscious && vitals.Alive))
		{
			return [];
		}

		return [.. inventory.Items.Where(e => e.SlotIndex >= 0 && e.InstanceId != 0)];
	}
}
