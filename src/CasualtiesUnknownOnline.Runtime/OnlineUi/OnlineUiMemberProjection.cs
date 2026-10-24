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
		bool remoteBackpackDenied = false, bool reviveFromTrader = true,
		bool permadeath = false,
		Func<ulong, bool>? hasLineOfSight = null,
		Func<ulong, PlayerColorValue>? getColor = null,
		bool sessionActive = false,
		ulong watchingSteamId = 0,
		bool spectatorMode = false)
	{
		var memberMap = members.ToDictionary(m => m.SteamId, m => m);
		var rows = new List<OnlineUiMemberRow>();

		var duplicateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var group in lobbyMembers
			.Select(displayName)
			.GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
			.Where(group => group.Count() > 1))
		{
			duplicateNames.Add(group.Key);
		}

		foreach (var memberId in lobbyMembers)
		{
			memberMap.TryGetValue(memberId, out var member);
			var vitals = member is { InWorld: true } ? getVitals(memberId) : null;
			var inventory = member is { InWorld: true } ? getInventory(memberId) : null;
			var isLocal = memberId == localSteamId;
			var canSee = isLocal || (member is { InWorld: true }
				&& localInWorld
				&& (hasLineOfSight?.Invoke(memberId) ?? true));
			var canViewMedical = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& canSee
				&& vitals is not null;

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
				&& canSee
				&& vitals is not null
				&& (!vitals.Conscious || !vitals.Alive)
				&& !isLocalCarried
				&& !isAlreadyCarried
				&& !isCarryingSomeone
				&& !hasExistingCarry;

			var canPiggyback = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& canSee
				&& vitals is { Alive: true, Conscious: true }
				&& !isLocalCarried
				&& !isAlreadyCarried
				&& !isCarryingSomeone
				&& !hasExistingCarry;
			var canCarryOnBack = canPiggyback;

			var canDrop = isCarryingThis;
			var canRequestDrop = isLocal
				&& localInWorld
				&& playerInteraction?.TryGetCarrier(localSteamId, out _) == true;
			var canRequestDropFromCarrier = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& playerInteraction?.TryGetCarrier(localSteamId, out var localCarrier) == true
				&& localCarrier == memberId;
			var canHeal = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& canSee
				&& vitals is { Alive: true }
				&& hasHealItem;
			var canPush = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& canSee
				&& vitals is not null
				&& !isLocalCarried
				&& !isAlreadyCarried
				&& !isCarryingSomeone
				&& !hasExistingCarry;
			// The same two terms the host's own gate reads before it executes a recruit
			// (RespawnPolicy.CanUseTraderRecruit, over RespawnOptions): a member's card
			// offers the button only when the rules the host announced would let the
			// recruit happen, so the operator does not click into a refusal. Both sides
			// read the same rule surface — the host its own config, the member the
			// announced set — and the host still decides.
			var canRecruit = !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& canSee
				&& vitals is { Alive: false }
				&& reviveFromTrader
				&& !permadeath;
			var takeable = canTake(inventory, vitals, isLocal, member, remoteBackpackDenied, canSee);
			var canOpenBackpack = !remoteBackpackDenied
				&& !isLocal
				&& member is { InWorld: true }
				&& localInWorld
				&& canSee
				&& inventory is not null;
			var canAdminMember = canAdmin && !isLocal && member is not null;

			var name = displayName(memberId);
			var showPeerId = duplicateNames.Contains(name);

			rows.Add(new OnlineUiMemberRow
			{
				SteamId = memberId,
				Name = name,
				PeerIdHex = showPeerId ? memberId.ToString("X") : null,
				Color = getColor?.Invoke(memberId) ?? PlayerColorResolver.Resolve(memberId),
				IsHost = memberId == lobbyOwner,
				IsLocal = isLocal,
				Handshaken = member?.Handshaken ?? false,
				InWorld = member?.InWorld ?? false,
				RttMs = member?.RttMs ?? -1f,
				VitalsText = vitals?.ToShortString(),
				InventoryText = inventory?.ToShortString(),
				IsCarryingThis = isCarryingThis,
				CanSee = canSee,
				CanCarry = canCarry,
				CanPiggyback = canPiggyback,
				CanCarryOnBack = canCarryOnBack,
				CanDrop = canDrop,
				CanRequestDrop = canRequestDrop,
				CanRequestDropFromCarrier = canRequestDropFromCarrier,
				CanHeal = canHeal,
				CanPush = canPush,
				CanRecruit = canRecruit,
				CanTake = takeable.Count > 0,
				CanOpenBackpack = canOpenBackpack,
				CanKick = canAdminMember,
				CanBan = canAdminMember,
				IsBanned = hostBan?.IsBanned(memberId) ?? false,
				CanViewMedical = canViewMedical,
				IsDead = vitals is not null && !vitals.Alive,
				IsWatched = memberId == watchingSteamId,
				// The ruling: the watch entry is gated by the MODE and nothing else — no distance, no reachability,
				// no dead-body condition. A local row is never a target, and the member must be in the world with a
				// living body this client renders.
				CanSpectate = spectatorMode
					&& !isLocal && member is { InWorld: true } && (vitals is null || vitals.Alive),
				IsUnconscious = vitals is not null && vitals.Alive && !vitals.Conscious,
				IsCarryingSomeone = isCarryingSomeone,
				IsCarried = isAlreadyCarried,
				TakeableItems = takeable,
				HealItems = canHeal ? healItems : [],
			});
		}

		return rows;
	}

	private static List<RemoteInventoryEntry> canTake(
		RemoteInventorySnapshot? inventory,
		RemoteVitalsSnapshot? vitals,
		bool isLocal,
		MemberPresenceTable.MemberPresence? member,
		bool remoteBackpackDenied,
		bool canSee)
	{
		if (remoteBackpackDenied
			|| isLocal
			|| !canSee
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
