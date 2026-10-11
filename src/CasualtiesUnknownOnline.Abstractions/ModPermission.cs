using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The capabilities a mod declares in its <see cref="CuoModAttribute.Permissions"/>.
/// The default is <see cref="None"/>: CUO never grants a capability implicitly —
/// a mod states exactly what it needs, and the discovery registry rejects unknown
/// bits and host/state permissions on local-only network modes
/// (see the framework's ModPermissionPolicy).
///
/// Every declared value now has a live enforcement point: the mod message
/// channel (<see cref="IModNetwork"/>), the host-command domain
/// (<see cref="IModCommands"/>), the mod-state store (<see cref="IModState"/>),
/// content registration (<see cref="IModContent"/>), the read-only game-state
/// projection (<see cref="IModGameState"/>), entity/item spawn and tile/
/// structure/liquid placement (<see cref="IModEntitySpawn"/>, <see cref="IModItemSpawn"/>,
/// <see cref="IModTilePlacement"/>, <see cref="IModStructurePlacement"/>,
/// <see cref="IModLiquidPlacement"/>), the native operation registry
/// (<see cref="IModNativeApi"/>) and the mod-authored effect and item-action faces
/// (<see cref="IModLiquidEffects"/>, <see cref="IModItemActions"/>).
/// </summary>
[Flags]
public enum ModPermission
{
	/// <summary>No capabilities declared. The default — nothing is implicit.</summary>
	None = 0,

	/// <summary>Read game state through <see cref="IModGameState"/> (enforced at the read surface).</summary>
	ReadGameState = 1 << 0,

	/// <summary>Write game state through a framework surface (enforced by <see cref="IModState"/> for host-persistent mod state).</summary>
	WriteGameState = 1 << 1,

	/// <summary>Spawn world entities/items or place custom tiles/structures/liquids via <see cref="IModEntitySpawn"/> / <see cref="IModItemSpawn"/> / <see cref="IModTilePlacement"/> / <see cref="IModStructurePlacement"/> / <see cref="IModLiquidPlacement"/> (enforced at each surface).</summary>
	SpawnEntity = 1 << 2,

	/// <summary>Use <see cref="IModNetwork"/> (send and receive). Enforced at the channel.</summary>
	SendNetworkMessage = 1 << 3,

	/// <summary>Register mod content definitions via <see cref="IModContent"/>. Enforced at registration.</summary>
	RegisterContent = 1 << 4,

	/// <summary>Register host commands via <see cref="IModCommands"/>. Enforced at registration.</summary>
	RegisterCommand = 1 << 5,

	/// <summary>Register/execute a host-action command (<see cref="ModCommand.IsHostAction"/>). Enforced at registration.</summary>
	ExecuteHostAction = 1 << 6,

	/// <summary>Access native/game-private operations through <see cref="IModNativeApi"/> (enforced at the registry surface).</summary>
	AccessNativeApi = 1 << 7,

	/// <summary>
	/// Register a mod-authored effect for content this mod registered, through
	/// <see cref="IModLiquidEffects"/> and <see cref="IModItemActions"/> (enforced at
	/// registration). A separate capability from <see cref="WriteGameState"/> on
	/// purpose: that one writes host-persistent mod state, this one writes a LIVE
	/// player's body — the mod's function runs where the game's own effect would, on
	/// the affected player's own client, and what it may write is the named-operation
	/// surface of <see cref="IModEffectBody"/> / <see cref="IModEffectLimb"/> /
	/// <see cref="IModEffectItem"/>.
	///
	/// <para>
	/// It is deliberately NOT one of the host/state permissions a
	/// <see cref="NetworkMode.ClientOnly"/> or <see cref="NetworkMode.Cosmetic"/> mod
	/// is refused for: it grants nothing on its own, because every registration is
	/// keyed by content of this mod's own and registering content already requires
	/// <see cref="RegisterContent"/>, which those modes ARE refused for. Such a mod
	/// may therefore declare this bit and register nothing — a declaration that
	/// cannot be reached rather than a capability it is denied.
	/// </para>
	/// </summary>
	AuthorEffects = 1 << 8,

	/// <summary>The defined bit mask — used to reject unknown bits.</summary>
	All = ReadGameState | WriteGameState | SpawnEntity | SendNetworkMessage
		| RegisterContent | RegisterCommand | ExecuteHostAction | AccessNativeApi
		| AuthorEffects,
}
