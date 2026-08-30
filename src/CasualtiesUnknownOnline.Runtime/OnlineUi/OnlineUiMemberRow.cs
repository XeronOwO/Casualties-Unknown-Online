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

	/// <summary>The effective marker color used for the member's name in the UI.</summary>
	public PlayerColorValue Color { get; init; }

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

	/// <summary>True when this member is conscious/alive and the local player can climb onto this member's back.</summary>
	public bool CanPiggyback { get; init; }

	/// <summary>True when this member is conscious/alive and can ride on the local player's back (local player as carrier).</summary>
	public bool CanCarryOnBack { get; init; }

	/// <summary>True when the local player is carrying this member and can drop it.</summary>
	public bool CanDrop { get; init; }

	/// <summary>True when the local player is being carried by someone and can request to get down.</summary>
	public bool CanRequestDrop { get; init; }

	/// <summary>True for the remote carrier row when the local player is riding on this member and can request to get down.</summary>
	public bool CanRequestDropFromCarrier { get; init; }

	/// <summary>True when the local player can request a heal on this in-world member.</summary>
	public bool CanHeal { get; init; }

	/// <summary>True when this in-world member can be pushed/shoved by the local player (the host still validates distance/standing/cooldown).</summary>
	public bool CanPush { get; init; }

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

	/// <summary>True when this member's cached vitals show a dead body.</summary>
	public bool IsDead { get; init; }

	/// <summary>True when this member is alive but unconscious.</summary>
	public bool IsUnconscious { get; init; }

	/// <summary>True when the local player has direct line of sight to this in-world member.</summary>
	public bool CanSee { get; init; }

	/// <summary>True when this member is carrying another player.</summary>
	public bool IsCarryingSomeone { get; init; }

	/// <summary>True when this member is currently carried by another player.</summary>
	public bool IsCarried { get; init; }

	/// <summary>The member's inventory snapshot, or null when no snapshot is cached yet.</summary>
	public IReadOnlyList<RemoteInventoryEntry>? Inventory { get; init; }

	/// <summary>The concrete slot items that can be taken (empty unless <see cref="CanTake"/> is true).</summary>
	public IReadOnlyList<RemoteInventoryEntry> TakeableItems { get; init; } = [];

	/// <summary>The local player's medical items available for an explicit heal selector.</summary>
	public IReadOnlyList<LocalHealItem> HealItems { get; init; } = [];
}
