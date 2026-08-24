using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One member row projected for the Online UI. The row contains everything the
/// UI needs to render a member and its interaction buttons; it never exposes
/// session services or Unity objects. Built by
/// <see cref="OnlineUiMemberProjection"/> each frame from the read-only runtime
/// surfaces.
/// </summary>
public sealed class OnlineUiMemberRow
{
	public ulong SteamId { get; init; }

	public string Name { get; init; } = "";

	public bool IsHost { get; init; }

	public bool IsLocal { get; init; }

	public bool Handshaken { get; init; }

	public bool InWorld { get; init; }

	public float RttMs { get; init; } = -1f;

	public string? VitalsText { get; init; }

	public string? InventoryText { get; init; }

	/// <summary>True when the local player is currently carrying this member.</summary>
	public bool IsCarryingThis { get; init; }

	/// <summary>True when this member is unconscious/dead and can be carried by the local player.</summary>
	public bool CanCarry { get; init; }

	/// <summary>True when the local player is carrying this member and can drop it.</summary>
	public bool CanDrop { get; init; }

	/// <summary>True when the local player can request a heal on this in-world member.</summary>
	public bool CanHeal { get; init; }

	/// <summary>True when the local player has at least one drink/food consumable usable on this in-world member.</summary>
	public bool CanUseItem { get; init; }

	/// <summary>True when the local player is at a trader and can recruit this dead member.</summary>
	public bool CanRecruit { get; init; }

	/// <summary>True when this member has at least one slot item the local player may take.</summary>
	public bool CanTake { get; init; }

	/// <summary>Host-only: true when this non-local member can be kicked.</summary>
	public bool CanKick { get; init; }

	/// <summary>Host-only: true when this non-local member can be banned.</summary>
	public bool CanBan { get; init; }

	/// <summary>True when this member is on the host's persisted ban list.</summary>
	public bool IsBanned { get; init; }

	/// <summary>The member's inventory snapshot, or null when no snapshot is cached yet.</summary>
	public IReadOnlyList<RemoteInventoryEntry>? Inventory { get; init; }

	/// <summary>The concrete slot items that can be taken (empty unless <see cref="CanTake"/> is true).</summary>
	public IReadOnlyList<RemoteInventoryEntry> TakeableItems { get; init; } = [];

	/// <summary>The local player's medical items available for an explicit heal selector.</summary>
	public IReadOnlyList<LocalHealItem> HealItems { get; init; } = [];

	/// <summary>The local player's drink/food items available for an explicit cross-player use selector.</summary>
	public IReadOnlyList<LocalUseItem> UseItems { get; init; } = [];
}
