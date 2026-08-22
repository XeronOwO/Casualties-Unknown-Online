using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The capabilities a mod declares in its <see cref="CuoModAttribute.Permissions"/>.
/// The default is <see cref="None"/>: CUO never grants a capability implicitly —
/// a mod states exactly what it needs, and the discovery registry rejects unknown
/// bits and host/state permissions on local-only network modes
/// (see the framework's ModPermissionPolicy).
///
/// <see cref="SendNetworkMessage"/>, <see cref="RegisterCommand"/>,
/// <see cref="ExecuteHostAction"/> and <see cref="WriteGameState"/> have an
/// executable enforcement point today (the mod message channel, the host-command
/// domain and the mod-state store). The remaining values are part of the binding
/// permission contract: they are declared, validated and carried through the
/// handshake, and their future API surfaces must check them before they can be
/// used.
/// </summary>
[Flags]
public enum ModPermission
{
	/// <summary>No capabilities declared. The default — nothing is implicit.</summary>
	None = 0,

	/// <summary>Read game state through a future framework surface (not exposed yet).</summary>
	ReadGameState = 1 << 0,

	/// <summary>Write game state through a framework surface (enforced by <see cref="IModState"/> for host-persistent mod state).</summary>
	WriteGameState = 1 << 1,

	/// <summary>Spawn entities through a future framework surface (not exposed yet).</summary>
	SpawnEntity = 1 << 2,

	/// <summary>Use <see cref="IModNetwork"/> (send and receive). Enforced at the channel.</summary>
	SendNetworkMessage = 1 << 3,

	/// <summary>Register content through a future framework surface (not exposed yet).</summary>
	RegisterContent = 1 << 4,

	/// <summary>Register host commands via <see cref="IModCommands"/>. Enforced at registration.</summary>
	RegisterCommand = 1 << 5,

	/// <summary>Register/execute a host-action command (<see cref="ModCommand.IsHostAction"/>). Enforced at registration.</summary>
	ExecuteHostAction = 1 << 6,

	/// <summary>Access native/game-private APIs through a future explicit escape hatch (not exposed yet).</summary>
	AccessNativeApi = 1 << 7,

	/// <summary>The defined bit mask — used to reject unknown bits.</summary>
	All = ReadGameState | WriteGameState | SpawnEntity | SendNetworkMessage
		| RegisterContent | RegisterCommand | ExecuteHostAction | AccessNativeApi,
}
