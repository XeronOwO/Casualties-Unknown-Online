using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The typed wire/command payload for one committed mod-status runtime value.
/// It is a plain data object in Abstractions: no Unity type, no game type, no
/// Runtime dependency. It carries the stable key (<c>status id + player SteamId
/// + optional limb slot</c>), the mod-owned schema version, the mod's own
/// <see cref="ModValue"/> plus an explicit remove flag. It is NOT a generic
/// snapshot and it does NOT interpret the value.
///
/// The frame travels over the existing <see cref="IModNetwork"/> channel (the
/// public mod-message frame); the helper surface in
/// <see cref="IModStatusTransport"/> converts between this DTO and the runtime
/// status table. Static status descriptors stay off the wire and are already
/// covered by the mod handshake.
///
/// As a value of the model this envelope is a MAP whose field names are its wire
/// contract: <c>id</c>, <c>scope</c>, <c>player</c>, <c>limb</c>,
/// <c>schema</c>, <c>remove</c> and — for a set frame — <c>value</c>, which is
/// the mod's own value, field for field. A receiver that does not know a field
/// ignores it; a frame missing a field it needs is refused by name.
/// </summary>
public sealed class ModStatusUpdate
{
	/// <summary>The status id field.</summary>
	public const string FieldId = "id";

	/// <summary>The body/limb scope field.</summary>
	public const string FieldScope = "scope";

	/// <summary>The player SteamId field.</summary>
	public const string FieldPlayer = "player";

	/// <summary>The limb slot field.</summary>
	public const string FieldLimb = "limb";

	/// <summary>The mod-owned schema version field.</summary>
	public const string FieldSchema = "schema";

	/// <summary>The removal flag field.</summary>
	public const string FieldRemove = "remove";

	/// <summary>The mod's own value field — present on a set frame, absent on a removal.</summary>
	public const string FieldValue = "value";

	/// <summary>The mod-scoped status id declared through <see cref="IModStatusRuntime.TryDeclare"/>.</summary>
	public string StatusId { get; set; } = string.Empty;

	/// <summary>Body-level or limb-level — required so the receiver knows which table to touch.</summary>
	public ModStatusScope Scope { get; set; } = ModStatusScope.Body;

	/// <summary>The player whose body/limb carries this status.</summary>
	public ulong PlayerSteamId { get; set; }

	/// <summary>
	/// The limb slot for <see cref="ModStatusScope.Limb"/> updates. Body
	/// updates use <c>-1</c> as the sentinel (never a valid limb slot).
	/// </summary>
	public int LimbSlot { get; set; } = -1;

	/// <summary>The mod-owned schema version declared for this status slot.</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>The mod-owned value. Null on a removal frame, which carries none.</summary>
	public ModValue? Value { get; set; }

	/// <summary>True when this frame clears the status value on the receiver; false when it writes <see cref="Value"/>.</summary>
	public bool Remove { get; set; }

	/// <summary>Creates a body status set frame.</summary>
	public static ModStatusUpdate ForBody(string statusId, ulong playerSteamId, int schemaVersion, ModValue value) =>
		new()
		{
			StatusId = statusId,
			Scope = ModStatusScope.Body,
			PlayerSteamId = playerSteamId,
			LimbSlot = -1,
			SchemaVersion = schemaVersion,
			Value = value,
			Remove = false,
		};

	/// <summary>Creates a limb status set frame.</summary>
	public static ModStatusUpdate ForLimb(string statusId, ulong playerSteamId, int limbSlot, int schemaVersion, ModValue value) =>
		new()
		{
			StatusId = statusId,
			Scope = ModStatusScope.Limb,
			PlayerSteamId = playerSteamId,
			LimbSlot = limbSlot,
			SchemaVersion = schemaVersion,
			Value = value,
			Remove = false,
		};

	/// <summary>Creates a body status removal frame.</summary>
	public static ModStatusUpdate RemoveBody(string statusId, ulong playerSteamId, int schemaVersion) =>
		new()
		{
			StatusId = statusId,
			Scope = ModStatusScope.Body,
			PlayerSteamId = playerSteamId,
			LimbSlot = -1,
			SchemaVersion = schemaVersion,
			Value = null,
			Remove = true,
		};

	/// <summary>Creates a limb status removal frame.</summary>
	public static ModStatusUpdate RemoveLimb(string statusId, ulong playerSteamId, int limbSlot, int schemaVersion) =>
		new()
		{
			StatusId = statusId,
			Scope = ModStatusScope.Limb,
			PlayerSteamId = playerSteamId,
			LimbSlot = limbSlot,
			SchemaVersion = schemaVersion,
			Value = null,
			Remove = true,
		};

	/// <summary>Render this update as the model value that travels.</summary>
	public ModValue ToValue()
	{
		var fields = new List<(string Key, ModValue Value)>
		{
			(FieldId, ModValue.Text(StatusId)),
			(FieldScope, ModValue.Integer((long)Scope)),
			(FieldPlayer, ModValue.Integer(unchecked((long)PlayerSteamId))),
			(FieldLimb, ModValue.Integer(LimbSlot)),
			(FieldSchema, ModValue.Integer(SchemaVersion)),
			(FieldRemove, ModValue.Boolean(Remove)),
		};

		if (Value is not null)
		{
			fields.Add((FieldValue, Value));
		}

		return ModValue.Map([.. fields]);
	}

	/// <summary>
	/// Read one update out of a value. Returns false for a value that is not a
	/// status frame — a shape a mod's own message may legitimately have, which is
	/// why the transport's consumer answers false rather than throwing — and for
	/// a frame that is missing a field it needs or carries a set without a value.
	/// </summary>
	public static bool TryFromValue(ModValue? value, out ModStatusUpdate update)
	{
		update = null!;
		if (value is null || value.Kind != ModValueKind.Map
			|| !value.TryGetField(FieldId, out var id) || !id.TryGetText(out var statusId)
			|| !value.TryGetField(FieldScope, out var scope) || !scope.TryGetInteger(out var scopeValue)
			|| !value.TryGetField(FieldPlayer, out var player) || !player.TryGetInteger(out var playerSteamId)
			|| !value.TryGetField(FieldLimb, out var limb) || !limb.TryGetInteger(out var limbSlot)
			|| !value.TryGetField(FieldSchema, out var schema) || !schema.TryGetInteger(out var schemaVersion)
			|| !value.TryGetField(FieldRemove, out var remove) || !remove.TryGetBoolean(out var isRemove))
		{
			return false;
		}

		if (scopeValue != (long)ModStatusScope.Body && scopeValue != (long)ModStatusScope.Limb)
		{
			return false;
		}

		ModValue? payload = null;
		if (value.TryGetField(FieldValue, out var carried))
		{
			payload = carried;
		}

		if (!isRemove && payload is null)
		{
			return false;
		}

		update = new ModStatusUpdate
		{
			StatusId = statusId,
			Scope = (ModStatusScope)scopeValue,
			PlayerSteamId = unchecked((ulong)playerSteamId),
			LimbSlot = (int)limbSlot,
			SchemaVersion = (int)schemaVersion,
			Value = payload,
			Remove = isRemove,
		};

		return true;
	}
}
