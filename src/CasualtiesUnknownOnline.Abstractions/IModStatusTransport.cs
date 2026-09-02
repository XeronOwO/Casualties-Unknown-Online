namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The typed status transport seam (mod-status domain phase 2). It converts a
/// committed <see cref="IModStatusRuntime"/> value into a typed
/// <see cref="ModStatusUpdate"/> frame and routes it over the existing
/// <see cref="IModNetwork"/> mod-message channel — no new NetMsg, no generic
/// snapshot protocol.
///
/// Authority:
/// - Host only: <see cref="TryBroadcastBodyStatus"/>,
///   <see cref="TryBroadcastLimbStatus"/> and the remove overloads commit the
///   authoritative value and fan it out to every member (including the host's
///   own local frame, which the handler consumes without re-applying).
/// - Guest only: <see cref="TryHandleStatusPayload"/> parses a host-originated
///   typed frame and applies it to the local mirror through
///   <see cref="IModStatusRuntime.TryApplyBodyStatus"/>/TryApplyLimb or the
///   corresponding remove methods.
///
/// The seam only supports published shared status facts. Local-only values
/// stay local and host-authoritative values have no guest mirror; those
/// scopes are refused by the broadcast helpers. A guest that needs to ask the
/// host to change a status still uses <see cref="IModCommands"/> — the host
/// command handler is the semantic validator and may then call one of the
/// broadcast helpers to publish the committed result.
/// </summary>
public interface IModStatusTransport
{
	/// <summary>
	/// Host only: commit a shared body status and broadcast it as a typed
	/// <see cref="ModStatusUpdate"/> to every member. Returns false when the
	/// status is not a declared shared slot, the host write is refused, or the
	/// call is outside a host session.
	/// </summary>
	bool TryBroadcastBodyStatus(string statusId, ulong playerSteamId, byte[] value);

	/// <summary>
	/// Host only: commit a shared limb status and broadcast it as a typed
	/// <see cref="ModStatusUpdate"/> to every member. Returns false when the
	/// status is not a declared shared slot, the host write is refused, or the
	/// call is outside a host session.
	/// </summary>
	bool TryBroadcastLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value);

	/// <summary>
	/// Host only: remove a shared body status from the host authority and
	/// broadcast the removal to every member. Returns false when the status is
	/// not a declared shared slot, the host removal is refused, or the call is
	/// outside a host session.
	/// </summary>
	bool TryBroadcastRemoveBodyStatus(string statusId, ulong playerSteamId);

	/// <summary>
	/// Host only: remove a shared limb status from the host authority and
	/// broadcast the removal to every member. Returns false when the status is
	/// not a declared shared slot, the host removal is refused, or the call is
	/// outside a host session.
	/// </summary>
	bool TryBroadcastRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot);

	/// <summary>
	/// Try to consume a payload as a typed <see cref="ModStatusUpdate"/>. On a
	/// guest this applies a host-originated set/removal to the local mirror. On
	/// the host this consumes the local echo of its own broadcast without
	/// re-applying. Returns true when the payload is a recognized status frame
	/// (even when the underlying runtime apply refused — those refusals are
	/// logged by the status surface); returns false for non-status payloads so
	/// a mod can continue routing its other mod-message traffic.
	/// </summary>
	bool TryHandleStatusPayload(ulong senderSteamId, byte[] payload);
}
