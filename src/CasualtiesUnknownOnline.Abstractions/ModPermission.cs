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
/// projection (<see cref="IModGameState"/>), entity spawn
/// (<see cref="IModEntitySpawn"/>) and the native operation registry
/// (<see cref="IModNativeApi"/>).
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

	/// <summary>Spawn world entities/items via <see cref="IModEntitySpawn"/> / <see cref="IModItemSpawn"/> (enforced at the spawn surface).</summary>
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

	/// <summary>The defined bit mask — used to reject unknown bits.</summary>
	All = ReadGameState | WriteGameState | SpawnEntity | SendNetworkMessage
		| RegisterContent | RegisterCommand | ExecuteHostAction | AccessNativeApi,
}
